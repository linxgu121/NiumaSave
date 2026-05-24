using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Save;
using NiumaSave.Controller;
using NiumaSave.Service;
using UnityEngine;

namespace NiumaSave.Bridge
{
    /// <summary>
    /// NiumaSave 到 UI 的数据驱动桥接层。
    /// 只负责整理存档槽表现数据和转发按钮命令，不创建具体 UI，也不参与云冲突决策。
    /// </summary>
    public sealed class SaveUIViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("存档模块根控制器。请拖入场景中的 NiumaSaveController；为空时可按配置自动查找。")]
        [SerializeField] private NiumaSaveController saveController;

        [Tooltip("实现 ISaveUIReceiver 的 UI 组件。桥接层会把整理后的存档表现数据交给它显示。")]
        [SerializeField] private MonoBehaviour saveUIReceiverProvider;

        [Header("自动查找")]
        [Tooltip("没有手动绑定存档控制器时，是否在场景中自动查找 NiumaSaveController。正式多场景建议手动绑定。")]
        [SerializeField] private bool autoFindSaveController = true;

        [Header("刷新策略")]
        [Tooltip("启用桥接层时是否立即刷新一次存档槽列表。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("是否在 LateUpdate 中根据 SaveRevision 自动刷新 UI。关闭后需要外部手动调用 RefreshSavePanel。")]
        [SerializeField] private bool refreshInLateUpdate = true;

        [Tooltip("没有存档服务或没有可显示槽位时，是否发送 Cleared 更新给 UI 接收接口。")]
        [SerializeField] private bool notifyWhenCleared = true;

        [Header("选择")]
        [Tooltip("当前选中的存档槽 ID。为空时桥接层会自动选择第一个可显示槽位；保存按钮可使用该槽位。")]
        [SerializeField] private string selectedSlotId;

        [Header("日志")]
        [Tooltip("桥接层缺少必要引用、刷新异常或检测到 UI 回流时是否打印警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly List<SaveSlotViewData> _slotBuffer = new List<SaveSlotViewData>();
        private ISaveUIReceiver _receiver;
        private CancellationTokenSource _lifetimeCts;
        private int _observedRevision = -1;
        private SavePanelViewData _lastPanelData;
        private bool _hadPanelData;
        private bool _isApplyingUpdate;
        private bool _isRefreshing;
        private bool _refreshRequested;
        private bool _operationInProgress;
        private string _lastOperationName;
        private string _lastOperationMessage;
        private bool _lastOperationSucceeded;
        private int _lastBuildFailureRevision = int.MinValue;

        private void Reset()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            _lifetimeCts = new CancellationTokenSource();
            ResolveReferences(true);
            _observedRevision = -1;

            if (refreshOnEnable)
            {
                RefreshSavePanel();
            }
        }

        private void OnDisable()
        {
            CancelLifetimeToken();
            _isApplyingUpdate = false;
            _isRefreshing = false;
            _refreshRequested = false;
            _operationInProgress = false;
        }

        private void LateUpdate()
        {
            if (_refreshRequested)
            {
                _refreshRequested = false;
                RefreshSavePanel();
                return;
            }

            if (!refreshInLateUpdate || !EnsureController())
            {
                return;
            }

            if (_observedRevision == saveController.SaveRevision)
            {
                return;
            }

            RefreshSavePanel();
        }

        /// <summary>
        /// 手动刷新存档面板。
        /// 该方法会异步读取存档槽列表和脏标记，适合 UI 打开面板时调用。
        /// </summary>
        public void RefreshSavePanel()
        {
            if (_isRefreshing)
            {
                _refreshRequested = true;
                return;
            }

            _ = RefreshSavePanelAsync();
        }

        /// <summary>
        /// 设置当前选中的存档槽。
        /// </summary>
        public void SetSelectedSlot(string slotId)
        {
            selectedSlotId = slotId;
            RequestRefresh();
        }

        /// <summary>
        /// 保存到当前选中的槽位；未选择时使用 NiumaSaveController 的默认槽位。
        /// </summary>
        public void SaveSelectedSlot()
        {
            var slotId = selectedSlotId;
            _ = RunOperationAsync("保存选中槽位", token =>
                WrapSaveResult(saveController.SaveGameAsync(slotId, null, SaveWriteMode.LocalOnly, token)));
        }

        /// <summary>
        /// 读取当前选中的槽位；未选择时使用 NiumaSaveController 的默认槽位。
        /// </summary>
        public void LoadSelectedSlot()
        {
            var slotId = selectedSlotId;
            _ = RunOperationAsync("读取选中槽位", token =>
                WrapImportResult(saveController.LoadGameAsync(slotId, SaveReadMode.LocalFirst, token)));
        }

        /// <summary>
        /// 删除当前选中的槽位。
        /// </summary>
        public void DeleteSelectedSlot()
        {
            var slotId = selectedSlotId;
            _ = RunOperationAsync("删除选中槽位", token =>
                WrapBoolResult(saveController.DeleteSlotAsync(slotId, token), "删除完成。", "删除失败。"));
        }

        /// <summary>
        /// 执行自动存档。
        /// </summary>
        public void SaveAutoSlot()
        {
            _ = RunOperationAsync("自动存档", token =>
                WrapSaveResult(saveController.SaveAutoAsync(null, SaveWriteMode.LocalOnly, token)));
        }

        /// <summary>
        /// 执行检查点存档。
        /// </summary>
        public void SaveCheckpointSlot()
        {
            _ = RunOperationAsync("检查点存档", token =>
                WrapSaveResult(saveController.SaveCheckpointAsync(null, SaveWriteMode.LocalOnly, token)));
        }

        /// <summary>
        /// 执行手动存档。
        /// 具体写入哪个 manual 槽由 ISaveSlotPolicy 决定。
        /// </summary>
        public void SaveManualSlot()
        {
            _ = RunOperationAsync("手动存档", token =>
                WrapSaveResult(saveController.SaveManualAsync(null, SaveWriteMode.LocalOnly, token)));
        }

        private async Task RefreshSavePanelAsync()
        {
            if (!EnsureController())
            {
                ApplyClearUpdate();
                return;
            }

            _isRefreshing = true;
            var targetRevision = saveController.SaveRevision;
            try
            {
                var panelData = await BuildPanelViewDataAsync(targetRevision, GetLifetimeToken());
                _lastBuildFailureRevision = int.MinValue;
                _observedRevision = saveController.SaveRevision;

                if (panelData == null || panelData.Slots.Length == 0)
                {
                    ApplyClearUpdate();
                    return;
                }

                _hadPanelData = true;
                ApplyRawUpdate(new SaveUIUpdate(
                    SaveUIUpdateType.Refresh,
                    _observedRevision,
                    panelData,
                    _lastPanelData));
                _lastPanelData = panelData;
            }
            catch (OperationCanceledException)
            {
                _observedRevision = -1;
            }
            catch (Exception exception)
            {
                _observedRevision = -1;
                if (logWarnings && _lastBuildFailureRevision != targetRevision)
                {
                    UnityEngine.Debug.LogError($"[NiumaSaveUIBridge] 构建存档 UI 表现数据失败，桥接层会在下一次刷新时重试。Revision={targetRevision}, Error={exception.Message}", this);
                }

                _lastBuildFailureRevision = targetRevision;
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task<SavePanelViewData> BuildPanelViewDataAsync(int revision, CancellationToken cancellationToken)
        {
            _slotBuffer.Clear();
            var slots = await saveController.RefreshSlotListAsync(cancellationToken);
            for (var i = 0; slots != null && i < slots.Count; i++)
            {
                var metadata = slots[i];
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.SlotId))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                var isDirty = await saveController.IsSlotDirtyAsync(metadata.SlotId, cancellationToken);
                _slotBuffer.Add(BuildSlotViewData(metadata, isDirty));
            }

            _slotBuffer.Sort(CompareSlots);
            var selectedSlot = FindSlotViewData(selectedSlotId);
            if (_slotBuffer.Count == 0)
            {
                selectedSlotId = null;
            }

            if (selectedSlot == null && _slotBuffer.Count > 0)
            {
                selectedSlot = _slotBuffer[0];
                selectedSlotId = selectedSlot.SlotId;
            }

            for (var i = 0; i < _slotBuffer.Count; i++)
            {
                _slotBuffer[i].IsSelected = selectedSlot != null
                    && string.Equals(_slotBuffer[i].SlotId, selectedSlot.SlotId, StringComparison.Ordinal);
            }

            return new SavePanelViewData
            {
                Revision = revision,
                ActiveSlotId = saveController.ActiveSlotId,
                DefaultSlotId = saveController.DefaultSlotId,
                SelectedSlotId = selectedSlotId,
                SelectedSlot = selectedSlot,
                Slots = _slotBuffer.ToArray(),
                IsOperationInProgress = _operationInProgress,
                LastOperationSucceeded = _lastOperationSucceeded,
                LastOperationName = _lastOperationName,
                LastOperationMessage = _lastOperationMessage,
                LastManualSlotSelection = BuildManualSlotSelection()
            };
        }

        private SaveSlotViewData BuildSlotViewData(SaveSlotMetadata metadata, bool isDirty)
        {
            return new SaveSlotViewData
            {
                SlotId = metadata.SlotId,
                DisplayName = string.IsNullOrWhiteSpace(metadata.DisplayName) ? metadata.SlotId : metadata.DisplayName,
                UserId = metadata.UserId,
                Revision = metadata.Revision,
                UpdatedAtUnixSeconds = metadata.UpdatedAtUnixSeconds,
                UpdatedAtText = FormatUnixTime(metadata.UpdatedAtUnixSeconds),
                PlayTimeSeconds = metadata.PlayTimeSeconds,
                PlayTimeText = FormatPlayTime(metadata.PlayTimeSeconds),
                Checksum = metadata.Checksum,
                DeviceId = metadata.DeviceId,
                SlotKind = GuessSlotKind(metadata.SlotId),
                IsDirty = isDirty,
                IsActiveSlot = string.Equals(metadata.SlotId, saveController.ActiveSlotId, StringComparison.Ordinal)
            };
        }

        private SaveManualSlotSelectionViewData BuildManualSlotSelection()
        {
            var selection = saveController.LastManualSlotSelection;
            if (string.IsNullOrWhiteSpace(selection.SlotId))
            {
                return null;
            }

            return new SaveManualSlotSelectionViewData
            {
                SlotId = selection.SlotId,
                WillOverwrite = selection.WillOverwrite
            };
        }

        private async Task RunOperationAsync(string operationName, Func<CancellationToken, Task<SaveUIOperationOutcome>> operation)
        {
            if (_operationInProgress || !EnsureController())
            {
                return;
            }

            _operationInProgress = true;
            _lastOperationName = operationName;
            _lastOperationMessage = "操作进行中。";
            _lastOperationSucceeded = false;
            RequestRefresh(SaveUIUpdateType.OperationStarted);

            try
            {
                var outcome = await operation(GetLifetimeToken());
                _lastOperationSucceeded = outcome.Succeeded;
                _lastOperationMessage = string.IsNullOrWhiteSpace(outcome.Message)
                    ? (outcome.Succeeded ? "操作完成。" : "操作失败。")
                    : outcome.Message;
            }
            catch (OperationCanceledException)
            {
                _lastOperationSucceeded = false;
                _lastOperationMessage = "操作已取消。";
            }
            catch (Exception exception)
            {
                _lastOperationSucceeded = false;
                _lastOperationMessage = exception.Message;
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning($"[NiumaSaveUIBridge] {operationName} 失败：{exception.Message}", this);
                }
            }
            finally
            {
                _operationInProgress = false;
                _observedRevision = -1;
                RequestRefresh(SaveUIUpdateType.OperationFinished);
            }
        }

        private async Task<SaveUIOperationOutcome> WrapSaveResult(Task<SaveOperationResult> task)
        {
            var result = await task;
            return new SaveUIOperationOutcome(result.Succeeded, result.Message);
        }

        private async Task<SaveUIOperationOutcome> WrapImportResult(Task<SaveGameImportResult> task)
        {
            var result = await task;
            return new SaveUIOperationOutcome(result.Succeeded, result.Message);
        }

        private async Task<SaveUIOperationOutcome> WrapBoolResult(Task<bool> task, string successMessage, string failMessage)
        {
            var result = await task;
            return new SaveUIOperationOutcome(result, result ? successMessage : failMessage);
        }

        private void RequestRefresh(SaveUIUpdateType updateType = SaveUIUpdateType.Refresh)
        {
            if (updateType == SaveUIUpdateType.OperationStarted && _lastPanelData != null)
            {
                ApplyRawUpdate(new SaveUIUpdate(updateType, saveController != null ? saveController.SaveRevision : 0, _lastPanelData, _lastPanelData));
            }

            _refreshRequested = true;
        }

        private void ApplyClearUpdate()
        {
            _observedRevision = saveController != null ? saveController.SaveRevision : 0;
            if (!notifyWhenCleared || (!_hadPanelData && _lastPanelData == null))
            {
                return;
            }

            var previous = _lastPanelData;
            _hadPanelData = false;
            _lastPanelData = null;
            ApplyRawUpdate(new SaveUIUpdate(
                SaveUIUpdateType.Cleared,
                _observedRevision,
                null,
                previous));
        }

        private void ApplyRawUpdate(SaveUIUpdate update)
        {
            var receiver = ResolveReceiver(false);
            if (receiver == null)
            {
                return;
            }

            if (_isApplyingUpdate)
            {
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning("[NiumaSaveUIBridge] 检测到 UI 刷新重入，已跳过本次 ApplySaveUpdate。请不要在 ISaveUIReceiver.ApplySaveUpdate 中直接执行存档命令。", this);
                }

                return;
            }

            _isApplyingUpdate = true;
            var revisionBeforeApply = saveController != null ? saveController.SaveRevision : 0;
            try
            {
                receiver.ApplySaveUpdate(update);
            }
            finally
            {
                _isApplyingUpdate = false;
            }

            if (saveController != null && saveController.SaveRevision != revisionBeforeApply)
            {
                _observedRevision = -1;
                _refreshRequested = true;
                if (logWarnings)
                {
                    UnityEngine.Debug.LogWarning("[NiumaSaveUIBridge] ISaveUIReceiver.ApplySaveUpdate 内修改了存档数据，桥接层已请求下一帧重新刷新。请把存档命令放到按钮回调或业务管线中处理。", this);
                }
            }
        }

        private SaveSlotViewData FindSlotViewData(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return null;
            }

            for (var i = 0; i < _slotBuffer.Count; i++)
            {
                var slot = _slotBuffer[i];
                if (slot != null && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal))
                {
                    return slot;
                }
            }

            return null;
        }

        private static int CompareSlots(SaveSlotViewData left, SaveSlotViewData right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var kindCompare = left.SlotKind.CompareTo(right.SlotKind);
            return kindCompare != 0
                ? kindCompare
                : string.Compare(left.SlotId, right.SlotId, StringComparison.Ordinal);
        }

        private static SaveSlotKind GuessSlotKind(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return SaveSlotKind.Unknown;
            }

            if (slotId.StartsWith("auto_", StringComparison.OrdinalIgnoreCase))
            {
                return SaveSlotKind.Auto;
            }

            if (slotId.StartsWith("checkpoint_", StringComparison.OrdinalIgnoreCase))
            {
                return SaveSlotKind.Checkpoint;
            }

            if (slotId.StartsWith("manual_", StringComparison.OrdinalIgnoreCase))
            {
                return SaveSlotKind.Manual;
            }

            if (slotId.StartsWith("backup_", StringComparison.OrdinalIgnoreCase))
            {
                return SaveSlotKind.Backup;
            }

            return SaveSlotKind.Unknown;
        }

        private static string FormatUnixTime(long unixSeconds)
        {
            if (unixSeconds <= 0)
            {
                return string.Empty;
            }

            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string FormatPlayTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return time.TotalHours >= 1d
                ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes:00}:{time.Seconds:00}";
        }

        private void ResolveReferences(bool logMissing)
        {
            if (saveController == null && autoFindSaveController)
            {
#if UNITY_2023_1_OR_NEWER
                saveController = FindFirstObjectByType<NiumaSaveController>();
#else
                saveController = FindObjectOfType<NiumaSaveController>();
#endif
            }

            _receiver = ResolveReceiver(logMissing);
        }

        private bool EnsureController()
        {
            if (saveController == null)
            {
                ResolveReferences(true);
            }

            if (saveController == null && logWarnings)
            {
                UnityEngine.Debug.LogWarning("[NiumaSaveUIBridge] 未找到 NiumaSaveController，请在 Inspector 中绑定存档控制器。", this);
            }

            return saveController != null;
        }

        private ISaveUIReceiver ResolveReceiver(bool logMissing)
        {
            var receiver = saveUIReceiverProvider as ISaveUIReceiver;
            if (receiver == null && logWarnings && logMissing && saveUIReceiverProvider != null)
            {
                UnityEngine.Debug.LogWarning("[NiumaSaveUIBridge] Save UI Receiver Provider 没有实现 ISaveUIReceiver。", this);
            }

            return receiver;
        }

        private CancellationToken GetLifetimeToken()
        {
            if (_lifetimeCts == null || _lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts = new CancellationTokenSource();
            }

            return _lifetimeCts.Token;
        }

        private void CancelLifetimeToken()
        {
            if (_lifetimeCts == null)
            {
                return;
            }

            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();
            _lifetimeCts = null;
        }

        private readonly struct SaveUIOperationOutcome
        {
            public readonly bool Succeeded;
            public readonly string Message;

            public SaveUIOperationOutcome(bool succeeded, string message)
            {
                Succeeded = succeeded;
                Message = message;
            }
        }
    }
}
