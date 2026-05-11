using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;
using UnityEngine;

namespace NiumaSave.Service
{
    /// <summary>
    /// 本地文件存档服务。
    /// 只负责 SavePayload 的本地落盘、读取、删除和槽位列表，不负责 Provider 收集、Checksum 计算或云同步。
    /// </summary>
    public sealed class LocalFileSaveService : ILocalSaveService
    {
        private const string OfflineUserId = "offline";
        private const string SaveFileName = "save.nmsave";
        private const string MetaFileName = "meta.nmsavemeta";
        private const string TempSuffix = ".tmp";
        private const string BackupSuffix = ".bak";

        private readonly string _rootDirectory;

        /// <summary>
        /// 使用 Application.persistentDataPath 下的默认 NiumaSave 目录。
        /// </summary>
        public LocalFileSaveService()
            : this(Path.Combine(Application.persistentDataPath, "NiumaSave"))
        {
        }

        /// <summary>
        /// 使用指定根目录。
        /// 测试或不同平台适配时可以传入自定义目录。
        /// </summary>
        public LocalFileSaveService(string rootDirectory)
        {
            _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "NiumaSave")
                : rootDirectory;
        }

        public Task<bool> ExistsAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(slotId))
                {
                    return false;
                }

                var findResult = FindSlotDirectory(slotId);
                return findResult.Found
                       && !findResult.IsAmbiguous
                       && File.Exists(Path.Combine(findResult.SlotDirectory, SaveFileName));
            }, cancellationToken);
        }

        public Task<SaveOperationResult> SaveAsync(SavePayload payload, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var validationMessage = ValidatePayload(payload);
                    if (!string.IsNullOrEmpty(validationMessage))
                    {
                        return SaveOperationResult.Fail(SaveSyncState.Failed, validationMessage);
                    }

                    var userId = GetUserDirectoryName(payload.Metadata.UserId);
                    var slotId = payload.Metadata.SlotId;
                    var slotDirectory = GetSlotDirectory(userId, slotId);
                    Directory.CreateDirectory(slotDirectory);

                    var manifest = new LocalSaveManifest
                    {
                        Metadata = payload.Metadata,
                        Format = payload.Format
                    };

                    var savePath = Path.Combine(slotDirectory, SaveFileName);
                    var metaPath = Path.Combine(slotDirectory, MetaFileName);
                    WriteFileAtomically(savePath, payload.Data, cancellationToken);

                    var manifestJson = JsonUtility.ToJson(manifest, true);
                    var manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestJson);
                    WriteFileAtomically(metaPath, manifestBytes, cancellationToken);

                    return SaveOperationResult.LocalSaved(payload.Metadata);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return SaveOperationResult.Fail(SaveSyncState.Failed, $"保存本地存档失败：{ex.Message}");
                }
            }, cancellationToken);
        }

        public Task<SaveLoadResult> LoadAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(slotId))
                {
                    return SaveLoadResult.Fail("存档槽 ID 为空。");
                }

                var findResult = FindSlotDirectory(slotId);
                if (findResult.IsAmbiguous)
                {
                    return SaveLoadResult.Fail($"本地存在多个同名存档槽，无法确定读取目标：{slotId}");
                }

                if (!findResult.Found)
                {
                    return SaveLoadResult.Fail($"本地存档槽不存在：{slotId}");
                }

                var slotDirectory = findResult.SlotDirectory;
                var savePath = Path.Combine(slotDirectory, SaveFileName);
                var metaPath = Path.Combine(slotDirectory, MetaFileName);
                if (!File.Exists(savePath))
                {
                    return SaveLoadResult.Fail($"本地存档文件不存在：{slotId}");
                }

                if (!File.Exists(metaPath))
                {
                    return SaveLoadResult.Fail($"本地存档元数据不存在：{slotId}");
                }

                try
                {
                    var manifestJson = File.ReadAllText(metaPath);
                    var manifest = JsonUtility.FromJson<LocalSaveManifest>(manifestJson);
                    if (manifest == null || manifest.Metadata == null)
                    {
                        return SaveLoadResult.Fail($"本地存档元数据损坏：{slotId}");
                    }

                    var data = File.ReadAllBytes(savePath);
                    var payload = new SavePayload
                    {
                        Metadata = manifest.Metadata,
                        Format = manifest.Format,
                        Data = data
                    };

                    return SaveLoadResult.Success(payload);
                }
                catch (Exception ex)
                {
                    return SaveLoadResult.Fail($"读取本地存档失败：{ex.Message}");
                }
            }, cancellationToken);
        }

        public Task<bool> DeleteAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(slotId))
                {
                    return false;
                }

                var findResult = FindSlotDirectory(slotId);
                if (!findResult.Found || findResult.IsAmbiguous)
                {
                    return false;
                }

                var slotDirectory = findResult.SlotDirectory;
                if (!Directory.Exists(slotDirectory))
                {
                    return false;
                }

                Directory.Delete(slotDirectory, true);
                return true;
            }, cancellationToken);
        }

        public Task<bool> CopyAsync(
            string sourceSlotId,
            string destinationSlotId,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(sourceSlotId)
                    || string.IsNullOrWhiteSpace(destinationSlotId)
                    || string.Equals(sourceSlotId, destinationSlotId, StringComparison.Ordinal))
                {
                    return false;
                }

                var sourceFindResult = FindSlotDirectory(sourceSlotId);
                if (!sourceFindResult.Found || sourceFindResult.IsAmbiguous)
                {
                    return false;
                }

                var sourceDirectory = sourceFindResult.SlotDirectory;
                var sourceParentDirectory = Directory.GetParent(sourceDirectory)?.FullName;
                if (string.IsNullOrWhiteSpace(sourceParentDirectory) || !Directory.Exists(sourceDirectory))
                {
                    return false;
                }

                var destinationDirectory = Path.Combine(sourceParentDirectory, SanitizePathPart(destinationSlotId));
                if (Directory.Exists(destinationDirectory) && !overwrite)
                {
                    return false;
                }

                var tempDirectory = destinationDirectory + TempSuffix;
                var backupDirectory = destinationDirectory + BackupSuffix;
                DeleteDirectoryIfExists(tempDirectory);
                DeleteDirectoryIfExists(backupDirectory);

                try
                {
                    CopyDirectory(sourceDirectory, tempDirectory, cancellationToken);
                    RewriteCopiedManifest(tempDirectory, destinationSlotId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Directory.Exists(destinationDirectory))
                    {
                        Directory.Move(destinationDirectory, backupDirectory);
                    }

                    Directory.Move(tempDirectory, destinationDirectory);
                    DeleteDirectoryIfExists(backupDirectory);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    DeleteDirectoryIfExists(tempDirectory);
                    if (!Directory.Exists(destinationDirectory) && Directory.Exists(backupDirectory))
                    {
                        Directory.Move(backupDirectory, destinationDirectory);
                    }

                    return false;
                }
            }, cancellationToken);
        }

        public Task<IReadOnlyList<SaveSlotMetadata>> ListSlotsAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run<IReadOnlyList<SaveSlotMetadata>>(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new List<SaveSlotMetadata>();
                if (!Directory.Exists(_rootDirectory))
                {
                    return result;
                }

                var metaFiles = Directory.GetFiles(_rootDirectory, MetaFileName, SearchOption.AllDirectories);
                for (var i = 0; i < metaFiles.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var metadata = TryReadMetadata(metaFiles[i]);
                    if (metadata != null)
                    {
                        result.Add(metadata);
                    }
                }

                result.Sort((left, right) => right.UpdatedAtUnixSeconds.CompareTo(left.UpdatedAtUnixSeconds));
                return result;
            }, cancellationToken);
        }

        private static string ValidatePayload(SavePayload payload)
        {
            if (payload == null)
            {
                return "SavePayload 不能为空。";
            }

            if (payload.Metadata == null)
            {
                return "SavePayload.Metadata 不能为空。";
            }

            if (string.IsNullOrWhiteSpace(payload.Metadata.SlotId))
            {
                return "SavePayload.Metadata.SlotId 不能为空。";
            }

            if (payload.Data == null)
            {
                return "SavePayload.Data 不能为空。";
            }

            return null;
        }

        private static SaveSlotMetadata TryReadMetadata(string metaPath)
        {
            try
            {
                var manifestJson = File.ReadAllText(metaPath);
                var manifest = JsonUtility.FromJson<LocalSaveManifest>(manifestJson);
                return manifest?.Metadata;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteFileAtomically(string path, byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + TempSuffix;
            var backupPath = path + BackupSuffix;
            File.WriteAllBytes(tempPath, data ?? Array.Empty<byte>());
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceByMove(tempPath, path);
                }
                catch (IOException)
                {
                    ReplaceByMove(tempPath, path);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }

        private static void ReplaceByMove(string tempPath, string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(destinationDirectory);

            var files = Directory.GetFiles(sourceDirectory);
            for (var i = 0; i < files.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(files[i]);
                File.Copy(files[i], Path.Combine(destinationDirectory, fileName), true);
            }

            var directories = Directory.GetDirectories(sourceDirectory);
            for (var i = 0; i < directories.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directoryName = Path.GetFileName(directories[i]);
                CopyDirectory(directories[i], Path.Combine(destinationDirectory, directoryName), cancellationToken);
            }
        }

        private static void DeleteDirectoryIfExists(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static void RewriteCopiedManifest(string slotDirectory, string destinationSlotId, CancellationToken cancellationToken)
        {
            var metaPath = Path.Combine(slotDirectory, MetaFileName);
            if (!File.Exists(metaPath))
            {
                return;
            }

            var manifestJson = File.ReadAllText(metaPath);
            var manifest = JsonUtility.FromJson<LocalSaveManifest>(manifestJson);
            if (manifest?.Metadata == null)
            {
                return;
            }

            manifest.Metadata.SlotId = destinationSlotId;
            var manifestBytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest, true));
            WriteFileAtomically(metaPath, manifestBytes, cancellationToken);
        }

        private string GetSlotDirectory(string userId, string slotId)
        {
            return Path.Combine(_rootDirectory, SanitizePathPart(userId), SanitizePathPart(slotId));
        }

        private SlotDirectoryFindResult FindSlotDirectory(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return SlotDirectoryFindResult.CreateNotFound();
            }

            var sanitizedSlotId = SanitizePathPart(slotId);
            var offlineDirectory = GetSlotDirectory(OfflineUserId, slotId);
            var foundDirectory = default(string);
            var foundCount = 0;
            if (Directory.Exists(offlineDirectory))
            {
                foundDirectory = offlineDirectory;
                foundCount++;
            }

            if (Directory.Exists(_rootDirectory))
            {
                var userDirectories = Directory.GetDirectories(_rootDirectory);
                for (var i = 0; i < userDirectories.Length; i++)
                {
                    var candidate = Path.Combine(userDirectories[i], sanitizedSlotId);
                    if (!Directory.Exists(candidate) || string.Equals(candidate, offlineDirectory, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foundDirectory = candidate;
                    foundCount++;
                    if (foundCount > 1)
                    {
                        return SlotDirectoryFindResult.CreateAmbiguous();
                    }
                }
            }

            return foundCount == 1
                ? SlotDirectoryFindResult.CreateFound(foundDirectory)
                : SlotDirectoryFindResult.CreateNotFound();
        }

        private static string GetUserDirectoryName(string userId)
        {
            return string.IsNullOrWhiteSpace(userId) ? OfflineUserId : userId;
        }

        private static string SanitizePathPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalidChars, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        [Serializable]
        private sealed class LocalSaveManifest
        {
            public SaveSlotMetadata Metadata;
            public string Format;
        }

        private readonly struct SlotDirectoryFindResult
        {
            public readonly bool Found;
            public readonly bool IsAmbiguous;
            public readonly string SlotDirectory;

            private SlotDirectoryFindResult(bool found, bool isAmbiguous, string slotDirectory)
            {
                Found = found;
                IsAmbiguous = isAmbiguous;
                SlotDirectory = slotDirectory;
            }

            public static SlotDirectoryFindResult CreateFound(string slotDirectory)
            {
                return new SlotDirectoryFindResult(true, false, slotDirectory);
            }

            public static SlotDirectoryFindResult CreateNotFound()
            {
                return new SlotDirectoryFindResult(false, false, null);
            }

            public static SlotDirectoryFindResult CreateAmbiguous()
            {
                return new SlotDirectoryFindResult(false, true, null);
            }
        }
    }
}
