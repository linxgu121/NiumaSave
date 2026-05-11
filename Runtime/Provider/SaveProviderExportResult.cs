using System;
using NiumaSave.Data;

namespace NiumaSave.Provider
{
    /// <summary>
    /// Provider 批量导出结果。
    /// 成功时 Sections 为有效快照数组；失败时 ErrorCode 和 Message 说明原因。
    /// </summary>
    public readonly struct SaveProviderExportResult
    {
        /// <summary>
        /// 是否导出成功。
        /// </summary>
        public readonly bool Succeeded;

        /// <summary>
        /// 导出的存档段数组。
        /// </summary>
        public readonly SaveSectionData[] Sections;

        /// <summary>
        /// 结构化错误码。
        /// </summary>
        public readonly SaveProviderExportErrorCode ErrorCode;

        /// <summary>
        /// 失败或调试提示信息。
        /// </summary>
        public readonly string Message;

        public SaveProviderExportResult(
            bool succeeded,
            SaveSectionData[] sections,
            SaveProviderExportErrorCode errorCode,
            string message)
        {
            Succeeded = succeeded;
            Sections = sections ?? Array.Empty<SaveSectionData>();
            ErrorCode = errorCode;
            Message = message;
        }

        public static SaveProviderExportResult Success(SaveSectionData[] sections)
        {
            return new SaveProviderExportResult(true, sections, SaveProviderExportErrorCode.None, null);
        }

        public static SaveProviderExportResult Fail(SaveProviderExportErrorCode errorCode, string message)
        {
            return new SaveProviderExportResult(false, Array.Empty<SaveSectionData>(), errorCode, message);
        }
    }
}
