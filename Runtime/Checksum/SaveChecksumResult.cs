namespace NiumaSave.Checksum
{
    /// <summary>
    /// 存档校验结果。
    /// 成功时 Checksum 为计算得到的 SHA-256 字符串。
    /// </summary>
    public readonly struct SaveChecksumResult
    {
        /// <summary>
        /// 本次操作是否成功。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 结构化错误码。
        /// </summary>
        public readonly SaveChecksumErrorCode ErrorCode;

        /// <summary>
        /// 计算得到的校验值。
        /// </summary>
        public readonly string Checksum;

        /// <summary>
        /// 文档原本记录的校验值。
        /// </summary>
        public readonly string ExpectedChecksum;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveChecksumResult(
            bool succeeded,
            SaveChecksumErrorCode errorCode,
            string checksum,
            string expectedChecksum,
            string message)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            Checksum = checksum;
            ExpectedChecksum = expectedChecksum;
            Message = message;
        }

        public static SaveChecksumResult Success(string checksum, string expectedChecksum = null)
        {
            return new SaveChecksumResult(true, SaveChecksumErrorCode.None, checksum, expectedChecksum, null);
        }

        public static SaveChecksumResult Fail(
            SaveChecksumErrorCode errorCode,
            string message,
            string checksum = null,
            string expectedChecksum = null)
        {
            return new SaveChecksumResult(false, errorCode, checksum, expectedChecksum, message);
        }
    }
}
