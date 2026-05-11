namespace NiumaSave.Provider
{
    /// <summary>
    /// 模块存档段导入错误码。
    /// 用于区分配置缺失、版本不兼容、数据损坏等不同失败原因。
    /// </summary>
    public enum SaveSectionImportErrorCode
    {
        /// <summary>
        /// 没有错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 存档段为空。
        /// </summary>
        NullSection = 1,

        /// <summary>
        /// 存档段 ID 与 Provider 声明不一致。
        /// </summary>
        SectionIdMismatch = 2,

        /// <summary>
        /// 存档段版本不支持。
        /// </summary>
        VersionUnsupported = 3,

        /// <summary>
        /// 存档段数据损坏或无法解析。
        /// </summary>
        DataCorrupted = 4,

        /// <summary>
        /// 当前项目配置缺失，导致无法恢复该存档段。
        /// </summary>
        ConfigMissing = 5,

        /// <summary>
        /// 导入逻辑执行失败。
        /// </summary>
        ImportFailed = 6,

        /// <summary>
        /// 未知错误。
        /// </summary>
        Unknown = 999
    }
}
