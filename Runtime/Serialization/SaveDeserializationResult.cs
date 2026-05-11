namespace NiumaSave.Serialization
{
    /// <summary>
    /// 存档反序列化结果。
    /// 成功时 Value 为有效对象；失败时 Message 记录调试信息。
    /// </summary>
    public readonly struct SaveDeserializationResult<T> where T : class
    {
        /// <summary>
        /// 是否反序列化成功。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 反序列化得到的对象。
        /// </summary>
        public readonly T Value;

        /// <summary>
        /// 结构化错误码。
        /// 成功时为 None，失败时调用方可用它做分支处理。
        /// </summary>
        public readonly SaveSerializationErrorCode ErrorCode;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveDeserializationResult(
            bool succeeded,
            T value,
            SaveSerializationErrorCode errorCode,
            string message)
        {
            Succeeded = succeeded;
            Value = value;
            ErrorCode = errorCode;
            Message = message;
        }

        public static SaveDeserializationResult<T> Success(T value)
        {
            return new SaveDeserializationResult<T>(true, value, SaveSerializationErrorCode.None, null);
        }

        public static SaveDeserializationResult<T> Fail(
            SaveSerializationErrorCode errorCode,
            string message)
        {
            return new SaveDeserializationResult<T>(false, null, errorCode, message);
        }
    }
}
