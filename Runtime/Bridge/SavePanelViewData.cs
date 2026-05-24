namespace NiumaSave.Bridge
{
    /// <summary>
    /// 存档面板完整表现数据。
    /// 桥接层每次刷新都输出完整快照，方便 UI 做列表重建、差异动画或调试显示。
    /// </summary>
    public sealed class SavePanelViewData
    {
        public int Revision;
        public string ActiveSlotId;
        public string DefaultSlotId;
        public string SelectedSlotId;
        public SaveSlotViewData SelectedSlot;
        public SaveSlotViewData[] Slots;
        public bool IsOperationInProgress;
        public bool LastOperationSucceeded;
        public string LastOperationName;
        public string LastOperationMessage;
        public SaveManualSlotSelectionViewData LastManualSlotSelection;
    }

    /// <summary>
    /// 手动存档策略最近一次选择结果的表现数据。
    /// </summary>
    public sealed class SaveManualSlotSelectionViewData
    {
        public string SlotId;
        public bool WillOverwrite;
    }
}
