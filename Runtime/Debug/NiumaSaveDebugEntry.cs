using System.Threading;
using NiumaCore.Save;
using NiumaSave.Controller;
using UnityEngine;

namespace NiumaSave.Debug
{
    /// <summary>
    /// NiumaSave 开发期调试入口。
    /// 只用于 Inspector 右键测试，不作为正式 UI 或正式输入逻辑。
    /// </summary>
    public sealed class NiumaSaveDebugEntry : MonoBehaviour
    {
        [Tooltip("存档模块根控制器。请拖入场景中的 NiumaSaveController。")]
        [SerializeField] private NiumaSaveController saveController;

        [Tooltip("调试使用的存档槽 ID。")]
        [SerializeField] private string slotId = "debug_01";

        [Tooltip("调试保存时使用的显示名称。")]
        [SerializeField] private string displayName = "Debug Save";

        [ContextMenu("NiumaSave/保存调试槽")]
        private async void DebugSave()
        {
            if (!ResolveController())
            {
                return;
            }

            var result = await saveController.SaveGameAsync(slotId, displayName, SaveWriteMode.LocalOnly, CancellationToken.None);
            UnityEngine.Debug.Log($"[NiumaSaveDebugEntry] 保存结果：Succeeded={result.Succeeded}, State={result.State}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaSave/读取调试槽")]
        private async void DebugLoad()
        {
            if (!ResolveController())
            {
                return;
            }

            var result = await saveController.LoadGameAsync(slotId, SaveReadMode.LocalFirst, CancellationToken.None);
            UnityEngine.Debug.Log($"[NiumaSaveDebugEntry] 读取结果：Succeeded={result.Succeeded}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaSave/保存自动槽")]
        private async void DebugSaveAuto()
        {
            if (!ResolveController())
            {
                return;
            }

            var result = await saveController.SaveAutoAsync("Auto Save", SaveWriteMode.LocalOnly, CancellationToken.None);
            UnityEngine.Debug.Log($"[NiumaSaveDebugEntry] 自动保存结果：Succeeded={result.Succeeded}, State={result.State}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaSave/保存检查点")]
        private async void DebugSaveCheckpoint()
        {
            if (!ResolveController())
            {
                return;
            }

            var result = await saveController.SaveCheckpointAsync("Checkpoint", SaveWriteMode.LocalOnly, CancellationToken.None);
            UnityEngine.Debug.Log($"[NiumaSaveDebugEntry] 检查点保存结果：Succeeded={result.Succeeded}, State={result.State}, Message={result.Message}", this);
        }

        [ContextMenu("NiumaSave/保存手动槽")]
        private async void DebugSaveManual()
        {
            if (!ResolveController())
            {
                return;
            }

            var result = await saveController.SaveManualAsync("Manual Save", SaveWriteMode.LocalOnly, CancellationToken.None);
            var overwriteText = saveController.LastManualSlotSelection.WillOverwrite ? "，已覆盖最旧手动档" : string.Empty;
            UnityEngine.Debug.Log(
                $"[NiumaSaveDebugEntry] 手动保存结果：SlotId={saveController.LastManualSlotSelection.SlotId}{overwriteText}，Succeeded={result.Succeeded}, State={result.State}, Message={result.Message}",
                this);
        }

        private bool ResolveController()
        {
            if (saveController != null)
            {
                return true;
            }

#if UNITY_2023_1_OR_NEWER
            saveController = FindFirstObjectByType<NiumaSaveController>();
#else
            saveController = FindObjectOfType<NiumaSaveController>();
#endif
            if (saveController == null)
            {
                UnityEngine.Debug.LogWarning("[NiumaSaveDebugEntry] 未找到 NiumaSaveController，请在 Inspector 中绑定。", this);
                return false;
            }

            return true;
        }
    }
}
