namespace NiumaSave.Checksum
{
    /// <summary>
    /// 存档校验错误码。
    /// Checksum 只用于发现落盘或传输损坏，不作为反作弊或逻辑版本判断依据。
    /// </summary>
    public enum SaveChecksumErrorCode
    {
        /// <summary>
        /// 没有错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 存档文档为空。
        /// </summary>
        NullDocument = 1,

        /// <summary>
        /// 存档头为空。
        /// </summary>
        NullHeader = 2,

        /// <summary>
        /// 序列化器为空。
        /// </summary>
        NullSerializer = 3,

        /// <summary>
        /// 序列化失败，无法计算校验值。
        /// </summary>
        SerializeFailed = 4,

        /// <summary>
        /// 文档中没有可验证的校验值。
        /// </summary>
        EmptyChecksum = 5,

        /// <summary>
        /// 重新计算得到的校验值与文档记录不一致。
        /// </summary>
        Mismatch = 6,

        /// <summary>
        /// 未知错误。
        /// </summary>
        Unknown = 999
    }
}
