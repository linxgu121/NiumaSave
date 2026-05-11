namespace NiumaSave.Serialization
{
    /// <summary>
    /// 序列化和反序列化错误码。
    /// 用于让调用方根据错误类型做分支处理，Message 只用于日志和调试。
    /// </summary>
    public enum SaveSerializationErrorCode
    {
        /// <summary>
        /// 没有错误。
        /// </summary>
        None = 0,

        /// <summary>
        /// 输入对象为空。
        /// </summary>
        NullInput = 1,

        /// <summary>
        /// 输入字节为空。
        /// </summary>
        EmptyData = 2,

        /// <summary>
        /// 当前序列化器不支持该格式。
        /// </summary>
        UnsupportedFormat = 3,

        /// <summary>
        /// 序列化失败。
        /// </summary>
        SerializeFailed = 4,

        /// <summary>
        /// 反序列化失败。
        /// </summary>
        DeserializeFailed = 5,

        /// <summary>
        /// 反序列化后文档结构无效。
        /// </summary>
        InvalidDocument = 6,

        /// <summary>
        /// 未知错误。
        /// </summary>
        Unknown = 999
    }
}
