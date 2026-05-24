namespace NiumaSave.Bridge
{
    /// <summary>
    /// 存档 UI 桥接层输出给接收器的一次更新。
    /// </summary>
    public readonly struct SaveUIUpdate
    {
        public readonly SaveUIUpdateType UpdateType;
        public readonly int Revision;
        public readonly SavePanelViewData PanelData;
        public readonly SavePanelViewData PreviousPanelData;

        public SaveUIUpdate(
            SaveUIUpdateType updateType,
            int revision,
            SavePanelViewData panelData,
            SavePanelViewData previousPanelData)
        {
            UpdateType = updateType;
            Revision = revision;
            PanelData = panelData;
            PreviousPanelData = previousPanelData;
        }
    }
}
