namespace NiumaSave.Provider
{
    /// <summary>
    /// Provider 导出错误码。
    /// 用于在收集模块快照时定位具体失败原因。
    /// </summary>
    public enum SaveProviderExportErrorCode
    {
        /// <summary>
        /// 没有错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 没有可导出的 Provider。
        /// </summary>
        NoProvider = 1,

        /// <summary>
        /// Provider 导出了空 Section。
        /// </summary>
        NullSection = 2,

        /// <summary>
        /// SectionId 为空。
        /// </summary>
        EmptySectionId = 3,

        /// <summary>
        /// Provider 声明的 SectionId 与导出的 SectionId 不一致。
        /// </summary>
        SectionIdMismatch = 4,

        /// <summary>
        /// Provider 导出过程中抛出异常。
        /// </summary>
        ExportFailed = 5,

        /// <summary>
        /// 未知错误。
        /// </summary>
        Unknown = 999
    }
}
