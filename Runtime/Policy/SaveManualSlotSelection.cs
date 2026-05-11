namespace NiumaSave.Policy
{
    /// <summary>
    /// 手动存档槽选择结果。
    /// UI 可以根据 WillOverwrite 决定是否显示“已覆盖最旧存档”的轻提示。
    /// </summary>
    public readonly struct SaveManualSlotSelection
    {
        /// <summary>
        /// 本次应该写入的手动存档槽 ID。
        /// </summary>
        public readonly string SlotId;

        /// <summary>
        /// 是否会覆盖已有手动存档。
        /// </summary>
        public readonly bool WillOverwrite;

        public SaveManualSlotSelection(string slotId, bool willOverwrite)
        {
            SlotId = slotId;
            WillOverwrite = willOverwrite;
        }
    }
}
