using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NiumaSave.Provider;
using UnityEngine;

namespace NiumaSave.Dirty
{
    /// <summary>
    /// 存档脏标记追踪器。
    /// 负责持久化 dirty.flag，并用 Provider Revision 记录内存基线。
    /// </summary>
    public sealed class SaveDirtyTracker
    {
        private const string DirtyDirectoryName = "dirty";
        private const string DirtyFileExtension = ".flag";

        private readonly string _dirtyDirectory;
        private readonly Dictionary<string, long> _providerRevisionBaseline = new(StringComparer.Ordinal);

        public SaveDirtyTracker()
            : this(Path.Combine(Application.persistentDataPath, "NiumaSave", DirtyDirectoryName))
        {
        }

        public SaveDirtyTracker(string dirtyDirectory)
        {
            _dirtyDirectory = string.IsNullOrWhiteSpace(dirtyDirectory)
                ? Path.Combine(Application.persistentDataPath, "NiumaSave", DirtyDirectoryName)
                : dirtyDirectory;
        }

        public Task MarkDirtyAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(slotId))
                {
                    return;
                }

                Directory.CreateDirectory(_dirtyDirectory);
                File.WriteAllText(GetDirtyPath(slotId), DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            }, cancellationToken);
        }

        public Task ClearDirtyAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(slotId))
                {
                    return;
                }

                var path = GetDirtyPath(slotId);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }, cancellationToken);
        }

        public Task<bool> IsDirtyAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return !string.IsNullOrWhiteSpace(slotId) && File.Exists(GetDirtyPath(slotId));
            }, cancellationToken);
        }

        /// <summary>
        /// 记录当前 Provider Revision 作为已保存基线。
        /// </summary>
        public void CaptureProviderBaseline(SaveDataProviderRegistry registry)
        {
            _providerRevisionBaseline.Clear();
            if (registry == null)
            {
                return;
            }

            var providers = registry.GetProviders();
            for (var i = 0; i < providers.Count; i++)
            {
                var provider = providers[i];
                if (provider == null || string.IsNullOrWhiteSpace(provider.SectionId))
                {
                    continue;
                }

                _providerRevisionBaseline[provider.SectionId] = provider.Revision;
            }
        }

        /// <summary>
        /// 检查 Provider Revision 是否相对基线发生变化。
        /// </summary>
        public bool HasProviderRevisionChanged(SaveDataProviderRegistry registry)
        {
            if (registry == null)
            {
                return false;
            }

            var providers = registry.GetProviders();
            for (var i = 0; i < providers.Count; i++)
            {
                var provider = providers[i];
                if (provider == null || string.IsNullOrWhiteSpace(provider.SectionId))
                {
                    continue;
                }

                if (!_providerRevisionBaseline.TryGetValue(provider.SectionId, out var baseline)
                    || baseline != provider.Revision)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetDirtyPath(string slotId)
        {
            return Path.Combine(_dirtyDirectory, SanitizePathPart(slotId) + DirtyFileExtension);
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
    }
}
