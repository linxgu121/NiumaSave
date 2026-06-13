using System;
using System.Collections.Generic;
using NiumaUI.Toolkit;
using NiumaUI.Toolkit.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace NiumaSave.Bridge
{
    public sealed class SaveToolkitBindingProvider : ToolkitViewBindingProviderBase
    {
        [Serializable] public sealed class SaveSlotEvent : UnityEvent<string> { }

        [Header("元素名称")]
        [SerializeField, Tooltip("标题 Label 的 name。默认 TitleText。")]
        private string titleLabelName = "TitleText";
        [SerializeField, Tooltip("状态 Label 的 name。默认 StatusText。")]
        private string statusLabelName = "StatusText";
        [SerializeField, Tooltip("存档槽列表 ListView 的 name。默认 ListRoot。")]
        private string listViewName = "ListRoot";
        [SerializeField, Tooltip("详情 Label 的 name。显示当前选中槽位。")]
        private string detailLabelName = "DetailText";
        [SerializeField, Tooltip("结果 Label 的 name。显示最近保存/读取/删除结果。")]
        private string resultLabelName = "ResultText";
        [SerializeField, Tooltip("空状态节点的 name。没有槽位时显示。")]
        private string emptyRootName = "EmptyRoot";

        [Header("按钮名称")]
        [SerializeField, Tooltip("保存按钮 name。点击时把当前选中 SlotId 传给 On Save Requested。")]
        private string saveButtonName = "SaveButton";
        [SerializeField, Tooltip("读取按钮 name。点击时把当前选中 SlotId 传给 On Load Requested。")]
        private string loadButtonName = "LoadButton";
        [SerializeField, Tooltip("删除按钮 name。点击时把当前选中 SlotId 传给 On Delete Requested。")]
        private string deleteButtonName = "DeleteButton";

        [Header("列表")]
        [SerializeField, Tooltip("最多显示多少个槽位。")]
        private int maxRows = 40;
        [SerializeField, Tooltip("列表行 USS class。")]
        private string rowClass = "niuma-save-row";
        [SerializeField, Tooltip("选中行 USS class。")]
        private string selectedRowClass = "is-selected";
        [SerializeField, Tooltip("禁用行 USS class。")]
        private string disabledRowClass = "is-disabled";

        [Header("交互事件")]
        [SerializeField, Tooltip("点击槽位行时触发。参数为 SlotId。")]
        private SaveSlotEvent onSlotSelected = new SaveSlotEvent();
        [SerializeField, Tooltip("点击 SaveButton 时触发。参数为 SlotId。")]
        private SaveSlotEvent onSaveRequested = new SaveSlotEvent();
        [SerializeField, Tooltip("点击 LoadButton 时触发。参数为 SlotId。")]
        private SaveSlotEvent onLoadRequested = new SaveSlotEvent();
        [SerializeField, Tooltip("点击 DeleteButton 时触发。参数为 SlotId。")]
        private SaveSlotEvent onDeleteRequested = new SaveSlotEvent();

        protected override string DefaultProviderId => "SavePanel";

        public override IToolkitViewBinding CreateBinding()
        {
            return new SaveToolkitBinding(
                titleLabelName,
                statusLabelName,
                listViewName,
                detailLabelName,
                resultLabelName,
                emptyRootName,
                saveButtonName,
                loadButtonName,
                deleteButtonName,
                maxRows,
                rowClass,
                selectedRowClass,
                disabledRowClass,
                id => onSlotSelected?.Invoke(id),
                id => onSaveRequested?.Invoke(id),
                id => onLoadRequested?.Invoke(id),
                id => onDeleteRequested?.Invoke(id));
        }
    }

    public sealed class SaveToolkitViewModel : UIPanelViewModelBase
    {
        public readonly List<ToolkitTextRowData> Rows = new List<ToolkitTextRowData>();
        public SavePanelViewData Panel { get; private set; }
        public SaveUIUpdateType UpdateType { get; private set; }
        public int Revision { get; private set; }
        public string SelectedSlotId { get; private set; }
        public int PageIndex { get; private set; }
        public string SearchKeyword { get; private set; }

        public void Apply(SaveUIUpdate update, int maxRows)
        {
            Panel = update.PanelData;
            UpdateType = update.UpdateType;
            Revision = update.Revision;
            SetContext(Panel?.ActiveSlotId);
            SelectedSlotId = NormalizeSelection(Panel, SelectedSlotId);
            RebuildRows(maxRows);
            MarkDirty();
        }

        public void Select(string slotId)
        {
            SelectedSlotId = string.IsNullOrWhiteSpace(slotId) ? null : slotId.Trim();
            RebuildRows(int.MaxValue);
            MarkDirty();
        }

        protected override void OnClear(UIViewModelClearReason reason)
        {
            Panel = null;
            UpdateType = SaveUIUpdateType.Cleared;
            Revision = 0;
            SelectedSlotId = null;
            PageIndex = 0;
            SearchKeyword = string.Empty;
            Rows.Clear();
        }

        private void RebuildRows(int maxRows)
        {
            Rows.Clear();
            var slots = Panel?.Slots ?? Array.Empty<SaveSlotViewData>();
            var limit = Math.Max(1, maxRows);
            for (var i = 0; i < slots.Length && i < limit; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                var id = string.IsNullOrWhiteSpace(slot.SlotId) ? $"slot:{i}" : slot.SlotId;
                var selected = slot.IsSelected || string.Equals(SelectedSlotId, id, StringComparison.Ordinal);
                Rows.Add(new ToolkitTextRowData(id, $"{(selected ? "> " : string.Empty)}{Text(slot.DisplayName, slot.SlotId)} | {slot.SlotKind} | Rev {slot.Revision} | {slot.UpdatedAtText} | {slot.PlayTimeText}{(slot.IsDirty ? " | 脏" : string.Empty)}", selected));
            }
        }

        private static string NormalizeSelection(SavePanelViewData panel, string previous)
        {
            if (!string.IsNullOrWhiteSpace(panel?.SelectedSlot?.SlotId))
                return panel.SelectedSlot.SlotId.Trim();
            if (!string.IsNullOrWhiteSpace(panel?.ActiveSlotId))
                return panel.ActiveSlotId.Trim();
            if (!string.IsNullOrWhiteSpace(previous))
                return previous.Trim();
            return null;
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }

    public sealed class SaveToolkitBinding : ToolkitViewBindingBase<SaveUIUpdate, SaveToolkitViewModel>
    {
        private readonly string _titleName;
        private readonly string _statusName;
        private readonly string _listName;
        private readonly string _detailName;
        private readonly string _resultName;
        private readonly string _emptyName;
        private readonly string _saveButtonName;
        private readonly string _loadButtonName;
        private readonly string _deleteButtonName;
        private readonly int _maxRows;
        private readonly string _rowClass;
        private readonly string _selectedClass;
        private readonly string _disabledClass;
        private readonly Action<string> _slotSelected;
        private readonly Action<string> _saveRequested;
        private readonly Action<string> _loadRequested;
        private readonly Action<string> _deleteRequested;
        private readonly ToolkitListBinding<ToolkitTextRowData> _listBinding = new ToolkitListBinding<ToolkitTextRowData>();
        private Label _title;
        private Label _status;
        private Label _detail;
        private Label _result;

        public SaveToolkitBinding(string titleName, string statusName, string listName, string detailName, string resultName, string emptyName, string saveButtonName, string loadButtonName, string deleteButtonName, int maxRows, string rowClass, string selectedClass, string disabledClass, Action<string> slotSelected, Action<string> saveRequested, Action<string> loadRequested, Action<string> deleteRequested)
        {
            _titleName = titleName;
            _statusName = statusName;
            _listName = listName;
            _detailName = detailName;
            _resultName = resultName;
            _emptyName = emptyName;
            _saveButtonName = saveButtonName;
            _loadButtonName = loadButtonName;
            _deleteButtonName = deleteButtonName;
            _maxRows = Mathf.Max(1, maxRows);
            _rowClass = string.IsNullOrWhiteSpace(rowClass) ? "niuma-save-row" : rowClass.Trim();
            _selectedClass = selectedClass;
            _disabledClass = disabledClass;
            _slotSelected = slotSelected;
            _saveRequested = saveRequested;
            _loadRequested = loadRequested;
            _deleteRequested = deleteRequested;
        }

        protected override void OnInitializeTyped()
        {
            _title = QLabel(_titleName);
            _status = QLabel(_statusName);
            _detail = QLabel(_detailName);
            _result = QLabel(_resultName);
            _listBinding.Bind(Root, _listName, new ToolkitTextRowItemBinder(_rowClass, _selectedClass, _disabledClass, HandleRowClicked), _emptyName);
            Callbacks.RegisterButton(Root, _saveButtonName, () => InvokeSelected(_saveRequested), HasSelection);
            Callbacks.RegisterButton(Root, _loadButtonName, () => InvokeSelected(_loadRequested), HasSelection);
            Callbacks.RegisterButton(Root, _deleteButtonName, () => InvokeSelected(_deleteRequested), HasSelection);
            ApplyVisualState(ViewModel);
        }

        protected override void OnRefreshTyped(SaveUIUpdate viewData, SaveToolkitViewModel viewModel)
        {
            viewModel.Apply(viewData, _maxRows);
            ApplyVisualState(viewModel);
        }

        protected override void OnClearTyped(UIViewModelClearReason reason)
        {
            _listBinding.Clear();
            ApplyVisualState(ViewModel);
        }

        protected override void OnDisposeTyped()
        {
            _listBinding.Dispose();
        }

        private void HandleRowClicked(ToolkitTextRowData row)
        {
            if (row == null)
                return;

            ViewModel.Select(row.Id);
            _slotSelected?.Invoke(row.Id);
            ApplyVisualState(ViewModel);
        }

        private void ApplyVisualState(SaveToolkitViewModel viewModel)
        {
            SetText(_title, "??");
            _listBinding.ReplaceAll(viewModel != null ? viewModel.Rows : Array.Empty<ToolkitTextRowData>());
            var panel = viewModel?.Panel;
            var slots = panel?.Slots ?? Array.Empty<SaveSlotViewData>();
            var updateType = viewModel != null ? viewModel.UpdateType : SaveUIUpdateType.Cleared;
            SetText(_status, panel == null ? $"???{updateType}" : $"Revision {panel.Revision} | ?? {slots.Length} | ?? {Text(panel.ActiveSlotId, "?")}");
            SetText(_detail, SlotDetail(panel?.SelectedSlot));
            SetText(_result, panel == null || string.IsNullOrWhiteSpace(panel.LastOperationName) ? string.Empty : $"{panel.LastOperationName}?{(panel.LastOperationSucceeded ? "??" : "??")} {panel.LastOperationMessage}");
        }

        private bool HasSelection()
        {
            return !string.IsNullOrWhiteSpace(ViewModel?.SelectedSlotId);
        }

        private void InvokeSelected(Action<string> action)
        {
            if (HasSelection())
                action?.Invoke(ViewModel.SelectedSlotId);
        }

        private static string SlotDetail(SaveSlotViewData slot)
        {
            return slot == null ? "未选择存档槽。" : $"选中：{Text(slot.DisplayName, slot.SlotId)}\nSlotId：{slot.SlotId}\n类型：{slot.SlotKind}\nRevision：{slot.Revision}\n更新时间：{slot.UpdatedAtText}\n游玩时间：{slot.PlayTimeText}";
        }

        private static string Text(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }
    }
}
