using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;

namespace NiumaSave.Policy
{
    /// <summary>
    /// 存档槽策略。
    /// 只决定写入哪个槽位、是否轮替和是否备份，不负责 Provider 导出或文件序列化。
    /// </summary>
    public interface ISaveSlotPolicy
    {
        /// <summary>
        /// 普通自动存档槽。
        /// </summary>
        string ResolveAutoSlot();

        /// <summary>
        /// 最近检查点槽。
        /// </summary>
        string ResolveLatestCheckpointSlot();

        /// <summary>
        /// 上一个检查点槽。
        /// </summary>
        string ResolvePreviousCheckpointSlot();

        /// <summary>
        /// 逻辑防覆盖备份槽。
        /// </summary>
        string ResolveBackupSlot();

        /// <summary>
        /// 选择本次手动保存应该写入的槽位。
        /// </summary>
        Task<SaveManualSlotSelection> ResolveManualSlotForWriteAsync(
            IReadOnlyList<SaveSlotMetadata> cachedSlots,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 覆盖关键槽位前准备 backup_01。
        /// </summary>
        Task<bool> PrepareBackupBeforeOverwriteAsync(
            string slotId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 创建新检查点前轮替 checkpoint_01 和 checkpoint_02。
        /// </summary>
        Task<bool> RotateCheckpointBeforeSaveAsync(CancellationToken cancellationToken = default);
    }
}
