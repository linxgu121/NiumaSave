namespace NiumaSave.Bridge
{
    /// <summary>
    /// 存档槽表现分类。
    /// 该分类只给 UI 显示和筛选使用，真实写入规则仍由 ISaveSlotPolicy 决定。
    /// </summary>
    public enum SaveSlotKind
    {
        /// <summary>
        /// 无法识别的存档槽。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 自动存档槽，例如 auto_01。
        /// </summary>
        Auto = 1,

        /// <summary>
        /// 检查点存档槽，例如 checkpoint_01。
        /// </summary>
        Checkpoint = 2,

        /// <summary>
        /// 玩家手动存档槽，例如 manual_01。
        /// </summary>
        Manual = 3,

        /// <summary>
        /// 防覆盖备份槽，例如 backup_01。
        /// </summary>
        Backup = 4
    }
}
