namespace NiumaSave.Provider
{
    /// <summary>
    /// 单个模块存档段导入结果。
    /// 用于表达导入成功、版本不兼容、配置缺失或数据损坏等情况。
    /// </summary>
    public readonly struct SaveSectionImportResult
    {
        /// <summary>
        /// 是否导入成功。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 结构化错误码。
        /// 成功时为 None，失败时调用方可用它做分支处理。
        /// </summary>
        public readonly SaveSectionImportErrorCode ErrorCode;

        /// <summary>
        /// 导入失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveSectionImportResult(
            bool succeeded,
            SaveSectionImportErrorCode errorCode,
            string message)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            Message = message;
        }

        public static SaveSectionImportResult Success()
        {
            return new SaveSectionImportResult(true, SaveSectionImportErrorCode.None, null);
        }

        public static SaveSectionImportResult Fail(SaveSectionImportErrorCode errorCode, string message)
        {
            return new SaveSectionImportResult(false, errorCode, message);
        }
    }
}
