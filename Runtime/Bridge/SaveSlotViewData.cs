namespace NiumaSave.Bridge
{
    /// <summary>
    /// 单个存档槽的 UI 表现数据。
    /// UI 只读取该数据，不直接修改存档核心元数据。
    /// </summary>
    public sealed class SaveSlotViewData
    {
        public string SlotId;
        public string DisplayName;
        public string UserId;
        public long Revision;
        public long UpdatedAtUnixSeconds;
        public string UpdatedAtText;
        public double PlayTimeSeconds;
        public string PlayTimeText;
        public string Checksum;
        public string DeviceId;
        public SaveSlotKind SlotKind;
        public bool IsDirty;
        public bool IsActiveSlot;
        public bool IsSelected;
    }
}
