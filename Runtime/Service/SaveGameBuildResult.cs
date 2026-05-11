using NiumaCore.Save;

namespace NiumaSave.Service
{
    /// <summary>
    /// 游戏存档载荷构建结果。
    /// 成功时 Payload 是已经完成序列化和 Checksum 写回的完整载荷。
    /// </summary>
    public readonly struct SaveGameBuildResult
    {
        public readonly bool Succeeded;
        public readonly SavePayload Payload;
        public readonly string Message;

        public SaveGameBuildResult(bool succeeded, SavePayload payload, string message)
        {
            Succeeded = succeeded;
            Payload = payload;
            Message = message;
        }

        public static SaveGameBuildResult Success(SavePayload payload)
        {
            return new SaveGameBuildResult(true, payload, null);
        }

        public static SaveGameBuildResult Fail(string message)
        {
            return new SaveGameBuildResult(false, null, message);
        }
    }
}
