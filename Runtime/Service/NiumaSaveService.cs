using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;
using NiumaSave.Dirty;

namespace NiumaSave.Service
{
    /// <summary>
    /// 高层存档服务。
    /// 只负责 SavePayload 的本地/云端读写策略协调，不遍历 Provider，也不组装 SaveGameDocument。
    /// </summary>
    public sealed class NiumaSaveService : ISaveService
    {
        private readonly ILocalSaveService _localSaveService;
        private readonly ICloudSaveService _cloudSaveService;
        private readonly SaveDirtyTracker _dirtyTracker;

        public NiumaSaveService(
            ILocalSaveService localSaveService,
            SaveDirtyTracker dirtyTracker,
            ICloudSaveService cloudSaveService = null)
        {
            _localSaveService = localSaveService;
            _dirtyTracker = dirtyTracker;
            _cloudSaveService = cloudSaveService;
        }

        public async Task<SaveOperationResult> SaveAsync(
            SavePayload payload,
            SaveWriteMode writeMode = SaveWriteMode.LocalOnly,
            CancellationToken cancellationToken = default)
        {
            if (_localSaveService == null)
            {
                return SaveOperationResult.Fail(SaveSyncState.Failed, "本地存档服务未配置。");
            }

            var localResult = await _localSaveService.SaveAsync(payload, cancellationToken);
            if (!localResult.Succeeded)
            {
                return localResult;
            }

            if (payload?.Metadata?.SlotId != null && _dirtyTracker != null)
            {
                await _dirtyTracker.ClearDirtyAsync(payload.Metadata.SlotId, cancellationToken);
            }

            if (writeMode == SaveWriteMode.LocalOnly)
            {
                return SaveOperationResult.LocalSaved(payload.Metadata);
            }

            if (_cloudSaveService == null)
            {
                return new SaveOperationResult(
                    true,
                    SaveSyncState.PendingUpload,
                    payload.Metadata,
                    null,
                    "云存档服务未配置，本地保存已完成，等待后续上传。");
            }

            var cloudResult = await _cloudSaveService.UploadAsync(payload, cancellationToken);
            if (cloudResult.Succeeded)
            {
                return SaveOperationResult.CloudSynced(cloudResult.Metadata ?? payload.Metadata);
            }

            if (cloudResult.State == SaveSyncState.Conflict)
            {
                return SaveOperationResult.ConflictDetected(cloudResult.Conflict);
            }

            return new SaveOperationResult(
                true,
                SaveSyncState.PendingUpload,
                payload.Metadata,
                null,
                cloudResult.Message);
        }

        public async Task<SaveLoadResult> LoadAsync(
            string slotId,
            SaveReadMode readMode = SaveReadMode.LocalFirst,
            CancellationToken cancellationToken = default)
        {
            if (readMode == SaveReadMode.CloudFirst)
            {
                var cloudFirst = await TryLoadCloudAsync(slotId, cancellationToken);
                if (cloudFirst.Succeeded)
                {
                    return cloudFirst;
                }

                return await TryLoadLocalAsync(slotId, cancellationToken);
            }

            if (readMode == SaveReadMode.LocalOnly)
            {
                return await TryLoadLocalAsync(slotId, cancellationToken);
            }

            var localFirst = await TryLoadLocalAsync(slotId, cancellationToken);
            if (localFirst.Succeeded)
            {
                return localFirst;
            }

            if (_cloudSaveService == null)
            {
                return localFirst;
            }

            return await TryLoadCloudAsync(slotId, cancellationToken);
        }

        public Task MarkDirtyAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return _dirtyTracker != null
                ? _dirtyTracker.MarkDirtyAsync(slotId, cancellationToken)
                : Task.CompletedTask;
        }

        public Task ClearDirtyAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return _dirtyTracker != null
                ? _dirtyTracker.ClearDirtyAsync(slotId, cancellationToken)
                : Task.CompletedTask;
        }

        public Task<bool> IsDirtyAsync(string slotId, CancellationToken cancellationToken = default)
        {
            return _dirtyTracker != null
                ? _dirtyTracker.IsDirtyAsync(slotId, cancellationToken)
                : Task.FromResult(false);
        }

        private async Task<SaveLoadResult> TryLoadLocalAsync(string slotId, CancellationToken cancellationToken)
        {
            return _localSaveService != null
                ? await _localSaveService.LoadAsync(slotId, cancellationToken)
                : SaveLoadResult.Fail("本地存档服务未配置。");
        }

        private async Task<SaveLoadResult> TryLoadCloudAsync(string slotId, CancellationToken cancellationToken)
        {
            if (_cloudSaveService == null)
            {
                return SaveLoadResult.Fail("云存档服务未配置。");
            }

            var cloudResult = await _cloudSaveService.DownloadAsync(slotId, cancellationToken);
            return cloudResult.Succeeded
                ? SaveLoadResult.Success(cloudResult.Payload)
                : SaveLoadResult.Fail(cloudResult.Message);
        }
    }
}
