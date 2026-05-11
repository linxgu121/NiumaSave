using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;

namespace NiumaSave.Policy
{
    /// <summary>
    /// 默认存档槽策略。
    /// 固定使用 auto_01、checkpoint_01、checkpoint_02、backup_01 和 manual_01-05。
    /// </summary>
    public sealed class DefaultSaveSlotPolicy : ISaveSlotPolicy
    {
        private const string AutoSlotId = "auto_01";
        private const string LatestCheckpointSlotId = "checkpoint_01";
        private const string PreviousCheckpointSlotId = "checkpoint_02";
        private const string BackupSlotId = "backup_01";
        private const int ManualSlotCount = 5;

        private readonly ILocalSaveService _localSaveService;

        public DefaultSaveSlotPolicy(ILocalSaveService localSaveService)
        {
            _localSaveService = localSaveService;
        }

        public string ResolveAutoSlot()
        {
            return AutoSlotId;
        }

        public string ResolveLatestCheckpointSlot()
        {
            return LatestCheckpointSlotId;
        }

        public string ResolvePreviousCheckpointSlot()
        {
            return PreviousCheckpointSlotId;
        }

        public string ResolveBackupSlot()
        {
            return BackupSlotId;
        }

        public Task<SaveManualSlotSelection> ResolveManualSlotForWriteAsync(
            IReadOnlyList<SaveSlotMetadata> cachedSlots,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var i = 1; i <= ManualSlotCount; i++)
            {
                var slotId = BuildManualSlotId(i);
                if (!ContainsSlot(cachedSlots, slotId))
                {
                    return Task.FromResult(new SaveManualSlotSelection(slotId, false));
                }
            }

            var oldestSlotId = BuildManualSlotId(1);
            var oldestUpdatedAt = long.MaxValue;
            if (cachedSlots != null)
            {
                for (var i = 0; i < cachedSlots.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var metadata = cachedSlots[i];
                    if (metadata == null || !IsManualSlot(metadata.SlotId))
                    {
                        continue;
                    }

                    if (metadata.UpdatedAtUnixSeconds < oldestUpdatedAt)
                    {
                        oldestUpdatedAt = metadata.UpdatedAtUnixSeconds;
                        oldestSlotId = metadata.SlotId;
                    }
                }
            }

            return Task.FromResult(new SaveManualSlotSelection(oldestSlotId, true));
        }

        public async Task<bool> PrepareBackupBeforeOverwriteAsync(
            string slotId,
            CancellationToken cancellationToken = default)
        {
            if (_localSaveService == null
                || string.IsNullOrWhiteSpace(slotId)
                || string.Equals(slotId, BackupSlotId, StringComparison.Ordinal))
            {
                return false;
            }

            var exists = await _localSaveService.ExistsAsync(slotId, cancellationToken);
            return exists && await _localSaveService.CopyAsync(slotId, BackupSlotId, true, cancellationToken);
        }

        public async Task<bool> RotateCheckpointBeforeSaveAsync(CancellationToken cancellationToken = default)
        {
            if (_localSaveService == null)
            {
                return false;
            }

            await _localSaveService.DeleteAsync(PreviousCheckpointSlotId, cancellationToken);
            var latestExists = await _localSaveService.ExistsAsync(LatestCheckpointSlotId, cancellationToken);
            return !latestExists
                   || await _localSaveService.CopyAsync(
                       LatestCheckpointSlotId,
                       PreviousCheckpointSlotId,
                       true,
                       cancellationToken);
        }

        private static bool ContainsSlot(IReadOnlyList<SaveSlotMetadata> slots, string slotId)
        {
            if (slots == null)
            {
                return false;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var metadata = slots[i];
                if (metadata != null && string.Equals(metadata.SlotId, slotId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsManualSlot(string slotId)
        {
            return !string.IsNullOrWhiteSpace(slotId)
                   && slotId.StartsWith("manual_", StringComparison.Ordinal);
        }

        private static string BuildManualSlotId(int index)
        {
            return $"manual_{index:00}";
        }
    }
}
