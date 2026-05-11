using System;

namespace NiumaSave.Data
{
    /// <summary>
    /// 存档头信息。
    /// 只保存可展示、可校验、可迁移的轻量元数据，不保存业务模块内部状态。
    /// </summary>
    [Serializable]
    public sealed class SaveGameHeader
    {
        /// <summary>
        /// 存档结构版本。用于判断 SaveGameDocument 自身是否需要迁移。
        /// </summary>
        public string SaveFormatVersion;

        /// <summary>
        /// 游戏版本。用于 UI 提示和兼容性判断。
        /// </summary>
        public string GameVersion;

        /// <summary>
        /// 存档槽唯一 ID，例如 slot_01、auto_01。
        /// </summary>
        public string SlotId;

        /// <summary>
        /// 展示名称。用于存档列表 UI，不参与业务逻辑判断。
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 账号 ID。离线存档可以为空。
        /// </summary>
        public string UserId;

        /// <summary>
        /// 存档修订号。每次成功保存后递增，用于本地和云端版本比较。
        /// </summary>
        public long Revision;

        /// <summary>
        /// UTC Unix 秒级时间戳。
        /// 不直接保存 DateTime，避免 Unity JsonUtility 在不同版本下行为不一致。
        /// </summary>
        public long SavedAtUnixSeconds;

        /// <summary>
        /// 当前存档累计游玩时长，单位为毫秒。
        /// 使用整数避免浮点序列化在不同平台产生最后一位差异，影响 Checksum。
        /// </summary>
        public long PlayTimeMilliseconds;

        /// <summary>
        /// 最后写入该存档的设备 ID。用于云同步冲突提示。
        /// </summary>
        public string DeviceId;

        /// <summary>
        /// 完整存档内容校验值。
        /// 计算时需要先临时清空本字段，再对完整 SaveGameDocument 字节计算，不能只校验 Header。
        /// </summary>
        public string Checksum;
    }
}
