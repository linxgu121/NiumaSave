using System;
using NiumaUI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace NiumaSave.Bridge
{
    public sealed class SaveToolkitBindingProvider : MonoBehaviour, IToolkitViewBindingProvider
    {
        [SerializeField, Tooltip("BindingProviderId，默认 SavePanel。需要和 UIToolkitViewRegistrySO 存档 View 的 BindingProviderId 一致。")] private string providerId = "SavePanel";
        [SerializeField] private string titleLabelName = "TitleText";
        [SerializeField] private string statusLabelName = "StatusText";
        [SerializeField] private string listRootName = "ListRoot";
        [SerializeField] private string detailLabelName = "DetailText";
        [SerializeField] private string resultLabelName = "ResultText";
        [SerializeField] private string emptyRootName = "EmptyRoot";
        [SerializeField] private int maxRows = 40;
        [SerializeField] private string rowClass = "niuma-save-row";

        public string ProviderId => string.IsNullOrWhiteSpace(providerId) ? "SavePanel" : providerId.Trim();
        public IToolkitViewBinding CreateBinding() => new SaveToolkitBinding(titleLabelName, statusLabelName, listRootName, detailLabelName, resultLabelName, emptyRootName, maxRows, rowClass);
    }

    public sealed class SaveToolkitBinding : ToolkitViewBindingBase
    {
        private readonly string _titleName, _statusName, _listName, _detailName, _resultName, _emptyName, _rowClass;
        private readonly int _maxRows;
        private Label _title, _status, _detail, _result;
        private VisualElement _list, _empty;

        public SaveToolkitBinding(string titleName, string statusName, string listName, string detailName, string resultName, string emptyName, int maxRows, string rowClass)
        {
            _titleName = titleName; _statusName = statusName; _listName = listName; _detailName = detailName; _resultName = resultName; _emptyName = emptyName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-save-row" : rowClass.Trim();
        }

        protected override void OnInitialize()
        {
            _title = QL(_titleName); _status = QL(_statusName); _list = QE(_listName); _detail = QL(_detailName); _result = QL(_resultName); _empty = QE(_emptyName);
            Apply(null, SaveUIUpdateType.Cleared, 0);
        }

        protected override void OnRefresh(object viewData)
        {
            if (viewData is SaveUIUpdate update) Apply(update.PanelData, update.UpdateType, update.Revision);
            else Apply(null, SaveUIUpdateType.Cleared, 0);
        }

        protected override void OnClose() => Apply(null, SaveUIUpdateType.Cleared, 0);

        private void Apply(SavePanelViewData panel, SaveUIUpdateType updateType, int revision)
        {
            Set(_title, "存档");
            Clear();
            var slots = panel?.Slots ?? Array.Empty<SaveSlotViewData>();
            SetVisible(_empty, panel == null || slots.Length == 0);
            Set(_status, panel == null ? $"状态：{updateType}" : $"Revision {panel.Revision} | 槽位 {slots.Length} | 当前 {Text(panel.ActiveSlotId, "无")}");

            if (panel == null)
            {
                Set(_detail, "暂无存档数据。");
                Set(_result, string.Empty);
                return;
            }

            Set(_detail, SlotDetail(panel.SelectedSlot));
            Set(_result, string.IsNullOrWhiteSpace(panel.LastOperationName) ? string.Empty : $"{panel.LastOperationName}：{(panel.LastOperationSucceeded ? "成功" : "失败")} {panel.LastOperationMessage}");

            for (var i = 0; i < slots.Length && i < _maxRows; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                Add($"{(slot.IsSelected ? "> " : string.Empty)}{Text(slot.DisplayName, slot.SlotId)} | {slot.SlotKind} | Rev {slot.Revision} | {slot.UpdatedAtText} | {slot.PlayTimeText}{(slot.IsDirty ? " | 脏" : string.Empty)}");
            }
        }

        private static string SlotDetail(SaveSlotViewData slot) => slot == null ? "未选择存档槽。" : $"选中：{Text(slot.DisplayName, slot.SlotId)}\nSlotId：{slot.SlotId}\n类型：{slot.SlotKind}\nRevision：{slot.Revision}\n更新时间：{slot.UpdatedAtText}\n游玩时间：{slot.PlayTimeText}";
        private Label QL(string name) => string.IsNullOrWhiteSpace(name) ? null : Query<Label>(name.Trim());
        private VisualElement QE(string name) => string.IsNullOrWhiteSpace(name) ? null : Root?.Q<VisualElement>(name.Trim());
        private void Clear() { if (_list != null) _list.Clear(); }
        private void Add(string text) { if (_list == null) return; var row = new Label(text ?? string.Empty); row.AddToClassList(_rowClass); _list.Add(row); }
        private static void Set(Label label, string text) { if (label != null) label.text = text ?? string.Empty; }
        private static void SetVisible(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
        private static string Text(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
    }
}
