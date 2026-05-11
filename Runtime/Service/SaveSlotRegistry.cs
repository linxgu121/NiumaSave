using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;

namespace NiumaSave.Service
{
    /// <summary>
    /// 存档槽注册表。
    /// 第一版只作为本地存档槽列表的轻量缓存，后续可扩展云端状态、脏标记、缩略图等信息。
    /// </summary>
    public sealed class SaveSlotRegistry
    {
        private readonly ILocalSaveService _localSaveService;
        private IReadOnlyList<SaveSlotMetadata> _cachedSlots = new List<SaveSlotMetadata>();

        public SaveSlotRegistry(ILocalSaveService localSaveService)
        {
            _localSaveService = localSaveService;
        }

        public IReadOnlyList<SaveSlotMetadata> CachedSlots => _cachedSlots;

        /// <summary>
        /// 从当前缓存中查找槽位元数据。
        /// 该方法不主动刷新磁盘，调用方需要在关键路径前先调用 RefreshAsync。
        /// </summary>
        public bool TryGetCachedSlot(string slotId, out SaveSlotMetadata metadata)
        {
            metadata = null;
            if (string.IsNullOrWhiteSpace(slotId) || _cachedSlots == null)
            {
                return false;
            }

            for (var i = 0; i < _cachedSlots.Count; i++)
            {
                var slot = _cachedSlots[i];
                if (slot != null && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                {
                    metadata = slot;
                    return true;
                }
            }

            return false;
        }

        public async Task<IReadOnlyList<SaveSlotMetadata>> RefreshAsync(CancellationToken cancellationToken = default)
        {
            _cachedSlots = _localSaveService != null
                ? await _localSaveService.ListSlotsAsync(cancellationToken)
                : new List<SaveSlotMetadata>();

            return _cachedSlots;
        }
    }
}
