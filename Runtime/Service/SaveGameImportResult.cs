namespace NiumaSave.Service
{
    /// <summary>
    /// 游戏存档导入结果。
    /// 表示 SavePayload 是否成功反序列化、校验并分发给所有 Provider。
    /// </summary>
    public readonly struct SaveGameImportResult
    {
        public readonly bool Succeeded;
        public readonly string Message;

        public SaveGameImportResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public static SaveGameImportResult Success()
        {
            return new SaveGameImportResult(true, null);
        }

        public static SaveGameImportResult Fail(string message)
        {
            return new SaveGameImportResult(false, message);
        }
    }
}
