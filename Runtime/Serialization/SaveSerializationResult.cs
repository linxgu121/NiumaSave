using System;

namespace NiumaSave.Serialization
{
    /// <summary>
    /// 存档序列化结果。
    /// 成功时 Data 为有效字节；失败时 Message 记录调试信息。
    /// </summary>
    public readonly struct SaveSerializationResult
    {
        /// <summary>
        /// 是否序列化成功。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 序列化格式标识，例如 json、binary、compressed-json。
        /// </summary>
        public readonly string Format;

        /// <summary>
        /// 结构化错误码。
        /// 成功时为 None，失败时调用方可用它做分支处理。
        /// </summary>
        public readonly SaveSerializationErrorCode ErrorCode;

        /// <summary>
        /// 序列化后的字节。
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveSerializationResult(
            bool succeeded,
            string format,
            SaveSerializationErrorCode errorCode,
            byte[] data,
            string message)
        {
            Succeeded = succeeded;
            Format = format;
            ErrorCode = errorCode;
            Data = data;
            Message = message;
        }

        public static SaveSerializationResult Success(string format, byte[] data)
        {
            return new SaveSerializationResult(true, format, SaveSerializationErrorCode.None, data ?? Array.Empty<byte>(), null);
        }

        public static SaveSerializationResult Fail(
            string format,
            SaveSerializationErrorCode errorCode,
            string message)
        {
            return new SaveSerializationResult(false, format, errorCode, Array.Empty<byte>(), message);
        }
    }
}
