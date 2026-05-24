namespace NiumaSave.Bridge
{
    /// <summary>
    /// 存档 UI 更新类型。
    /// </summary>
    public enum SaveUIUpdateType
    {
        /// <summary>
        /// 清空显示。
        /// </summary>
        Cleared = 0,

        /// <summary>
        /// 刷新完整存档面板。
        /// </summary>
        Refresh = 1,

        /// <summary>
        /// 存档操作开始。
        /// </summary>
        OperationStarted = 2,

        /// <summary>
        /// 存档操作结束。
        /// </summary>
        OperationFinished = 3
    }
}
