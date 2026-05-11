namespace NiumaSave.Data
{
    /// <summary>
    /// 存档数据编码常量。
    /// 用字符串而不是 enum，方便后续接入压缩、加密或平台格式时保持兼容。
    /// </summary>
    public static class SaveDataEncoding
    {
        /// <summary>
        /// Base64 编码。
        /// 用于把二进制模块快照安全放入 JsonUtility 可序列化的字符串字段中。
        /// </summary>
        public const string Base64 = "base64";

        /// <summary>
        /// 纯文本编码。
        /// 仅用于已经确认是可直接存入文本文档的内容。
        /// </summary>
        public const string PlainText = "plain-text";
    }
}
