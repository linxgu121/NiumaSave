using System;
using System.Threading;
using System.Threading.Tasks;
using NiumaCore.Module;
using NiumaCore.Save;
using NiumaSave.Dirty;
using NiumaSave.Policy;
using NiumaSave.Provider;
using NiumaSave.Serialization;
using NiumaSave.Service;
using UnityEngine;

namespace NiumaSave.Controller
{
    /// <summary>
    /// NiumaSave 模块根控制器。
    /// 只负责模块生命周期、服务装配和对外入口，具体存档策略交给 Service 与 Coordinator。
    /// </summary>
    public sealed class NiumaSaveController : MonoBehaviour, IGameModule
    {
        [Header("本地存档")]
        [Tooltip("本地存档根目录。为空时使用 Application.persistentDataPath/NiumaSave。")]
        [SerializeField] private string rootDirectory;

        [Tooltip("默认存档槽 ID。调试保存和自动保存未指定槽位时使用。")]
        [SerializeField] private string defaultSlotId = "auto_01";

        [Tooltip("默认存档显示名称。用于存档列表显示。")]
        [SerializeField] private string defaultDisplayName = "Auto Save";

        [Header("模块行为")]
        [Tooltip("启用时是否自动启动模块。")]
        [SerializeField] private bool startOnEnable = true;

        [Tooltip("是否在 Tick 中根据 Provider Revision 自动标记脏状态。")]
        [SerializeField] private bool trackProviderRevision = true;

        private GameContext _context;
        private bool _isRunning;

        private SaveDataProviderRegistry _providerRegistry;
        private ISaveSerializer _serializer;
        private ILocalSaveService _localSaveService;
        private SaveDirtyTracker _dirtyTracker;
        private SaveSlotRegistry _slotRegistry;
        private ISaveSlotPolicy _slotPolicy;
        private NiumaSaveService _saveService;
        private SaveGameCoordinator _coordinator;
        private string _activeSlotId;
        private double _loadedPlayTimeSeconds;
        private float _sessionStartRealtime;
        private bool _isMarkingDirty;

        public string ModuleName => "NiumaSave";

        public bool IsRunning => _isRunning;
        public SaveDataProviderRegistry ProviderRegistry => _providerRegistry;
        public ISaveService SaveService => _saveService;
        public SaveGameCoordinator Coordinator => _coordinator;
        public ISaveSlotPolicy SlotPolicy => _slotPolicy;
        public SaveManualSlotSelection LastManualSlotSelection { get; private set; }

        private void Awake()
        {
            EnsureServices();
            ResetSessionClock(defaultSlotId, 0d);
        }

        private void OnEnable()
        {
            if (startOnEnable)
            {
                StartModule();
            }
        }

        private void OnDisable()
        {
            StopModule();
        }

        private void Update()
        {
            if (_isRunning)
            {
                Tick(Time.deltaTime);
            }
        }

        public void Initialize(GameContext context)
        {
            _context = context;
            EnsureServices(false);
            RebuildSaveService();
            _context?.RegisterService<ISaveService>(_saveService);
            _context?.RegisterService<ILocalSaveService>(_localSaveService);
        }

        public void StartModule()
        {
            EnsureServices();
            _isRunning = true;
            _dirtyTracker.CaptureProviderBaseline(_providerRegistry);
        }

        public void StopModule()
        {
            _isRunning = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_isRunning || !trackProviderRevision || _dirtyTracker == null || _providerRegistry == null)
            {
                return;
            }

            if (_dirtyTracker.HasProviderRevisionChanged(_providerRegistry))
            {
                MarkDefaultSlotDirtySafely();
            }
        }

        public bool RegisterProvider(ISaveDataProvider provider, bool replaceExisting = false)
        {
            EnsureServices();
            var registered = _providerRegistry.Register(provider, replaceExisting);
            if (registered)
            {
                _dirtyTracker.CaptureProviderBaseline(_providerRegistry);
            }

            return registered;
        }

        public bool UnregisterProvider(string sectionId)
        {
            EnsureServices();
            var removed = _providerRegistry.Unregister(sectionId);
            if (removed)
            {
                _dirtyTracker.CaptureProviderBaseline(_providerRegistry);
            }

            return removed;
        }

        public async Task<SaveOperationResult> SaveGameAsync(
            string slotId = null,
            string displayName = null,
            SaveWriteMode writeMode = SaveWriteMode.LocalOnly,
            CancellationToken cancellationToken = default)
        {
            EnsureServices();
            var resolvedSlotId = ResolveSlotId(slotId);
            await _slotRegistry.RefreshAsync(cancellationToken);
            var metadata = CreateMetadata(resolvedSlotId, displayName);
            var buildResult = _coordinator.BuildPayload(metadata);
            if (!buildResult.Succeeded)
            {
                return SaveOperationResult.Fail(SaveSyncState.Failed, buildResult.Message);
            }

            var saveResult = await _saveService.SaveAsync(buildResult.Payload, writeMode, cancellationToken);
            if (saveResult.Succeeded)
            {
                ResetSessionClock(resolvedSlotId, metadata.PlayTimeSeconds);
                _dirtyTracker.CaptureProviderBaseline(_providerRegistry);
                await _slotRegistry.RefreshAsync(cancellationToken);
            }

            return saveResult;
        }

        public async Task<SaveOperationResult> SaveAutoAsync(
            string displayName = null,
            SaveWriteMode writeMode = SaveWriteMode.LocalOnly,
            CancellationToken cancellationToken = default)
        {
            EnsureServices();
            var slotId = _slotPolicy.ResolveAutoSlot();
            await _slotPolicy.PrepareBackupBeforeOverwriteAsync(slotId, cancellationToken);
            return await SaveGameAsync(slotId, displayName ?? "Auto Save", writeMode, cancellationToken);
        }

        public async Task<SaveOperationResult> SaveCheckpointAsync(
            string displayName = null,
            SaveWriteMode writeMode = SaveWriteMode.LocalOnly,
            CancellationToken cancellationToken = default)
        {
            EnsureServices();
            var slotId = _slotPolicy.ResolveLatestCheckpointSlot();
            await _slotPolicy.PrepareBackupBeforeOverwriteAsync(slotId, cancellationToken);
            await _slotPolicy.RotateCheckpointBeforeSaveAsync(cancellationToken);
            return await SaveGameAsync(slotId, displayName ?? "Checkpoint", writeMode, cancellationToken);
        }

        public async Task<SaveOperationResult> SaveManualAsync(
            string displayName = null,
            SaveWriteMode writeMode = SaveWriteMode.LocalOnly,
            CancellationToken cancellationToken = default)
        {
            EnsureServices();
            await _slotRegistry.RefreshAsync(cancellationToken);
            LastManualSlotSelection = await _slotPolicy.ResolveManualSlotForWriteAsync(
                _slotRegistry.CachedSlots,
                cancellationToken);

            return await SaveGameAsync(
                LastManualSlotSelection.SlotId,
                displayName ?? "Manual Save",
                writeMode,
                cancellationToken);
        }

        public async Task<SaveGameImportResult> LoadGameAsync(
            string slotId = null,
            SaveReadMode readMode = SaveReadMode.LocalFirst,
            CancellationToken cancellationToken = default)
        {
            EnsureServices();
            var resolvedSlotId = ResolveSlotId(slotId);
            var loadResult = await _saveService.LoadAsync(resolvedSlotId, readMode, cancellationToken);
            if (!loadResult.Succeeded)
            {
                return SaveGameImportResult.Fail(loadResult.Message);
            }

            var importResult = _coordinator.ImportPayload(loadResult.Payload);
            if (importResult.Succeeded)
            {
                var metadata = loadResult.Payload?.Metadata;
                ResetSessionClock(
                    string.IsNullOrWhiteSpace(metadata?.SlotId) ? resolvedSlotId : metadata.SlotId,
                    metadata != null ? metadata.PlayTimeSeconds : 0d);
                _dirtyTracker.CaptureProviderBaseline(_providerRegistry);
                await _slotRegistry.RefreshAsync(cancellationToken);
            }

            return importResult;
        }

        public async Task<bool> DeleteSlotAsync(string slotId = null, CancellationToken cancellationToken = default)
        {
            EnsureServices();
            var resolvedSlotId = ResolveSlotId(slotId);
            try
            {
                var deleted = await _localSaveService.DeleteAsync(resolvedSlotId, cancellationToken);
                if (deleted)
                {
                    await _dirtyTracker.ClearDirtyAsync(resolvedSlotId, cancellationToken);
                    await _slotRegistry.RefreshAsync(cancellationToken);

                    if (string.Equals(_activeSlotId, resolvedSlotId, StringComparison.Ordinal))
                    {
                        ResetSessionClock(defaultSlotId, 0d);
                    }
                }

                return deleted;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[NiumaSaveController] 删除存档槽失败：SlotId={resolvedSlotId}, Error={ex.Message}", this);
                return false;
            }
        }

        private void EnsureServices(bool rebuildSaveService = true)
        {
            if (_providerRegistry != null)
            {
                if (_slotPolicy == null && _localSaveService != null)
                {
                    _slotPolicy = new DefaultSaveSlotPolicy(_localSaveService);
                }

                if (rebuildSaveService && _saveService == null)
                {
                    RebuildSaveService();
                }

                return;
            }

            _providerRegistry = new SaveDataProviderRegistry();
            _serializer = new UnityJsonSaveSerializer();
            _localSaveService = string.IsNullOrWhiteSpace(rootDirectory)
                ? new LocalFileSaveService()
                : new LocalFileSaveService(rootDirectory);

            _dirtyTracker = string.IsNullOrWhiteSpace(rootDirectory)
                ? new SaveDirtyTracker()
                : new SaveDirtyTracker(System.IO.Path.Combine(rootDirectory, "dirty"));

            _slotRegistry = new SaveSlotRegistry(_localSaveService);
            _slotPolicy = new DefaultSaveSlotPolicy(_localSaveService);
            _coordinator = new SaveGameCoordinator(_providerRegistry, _serializer);
            if (rebuildSaveService)
            {
                RebuildSaveService();
            }
        }

        private void RebuildSaveService()
        {
            var cloudSaveService = _context?.GetService<ICloudSaveService>();
            _saveService = new NiumaSaveService(_localSaveService, _dirtyTracker, cloudSaveService);
        }

        private SaveSlotMetadata CreateMetadata(string slotId, string displayName)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new SaveSlotMetadata
            {
                SlotId = slotId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? defaultDisplayName : displayName,
                Revision = CalculateNextRevision(slotId),
                UpdatedAtUnixSeconds = now,
                PlayTimeSeconds = CalculateCurrentPlayTimeSeconds(slotId),
                DeviceId = SystemInfo.deviceUniqueIdentifier
            };
        }

        private string ResolveSlotId(string slotId)
        {
            return string.IsNullOrWhiteSpace(slotId) ? defaultSlotId : slotId;
        }

        private long CalculateNextRevision(string slotId)
        {
            return _slotRegistry.TryGetCachedSlot(slotId, out var metadata)
                ? Math.Max(0, metadata.Revision) + 1
                : 1;
        }

        private double CalculateCurrentPlayTimeSeconds(string slotId)
        {
            var basePlayTime = 0d;
            if (string.Equals(_activeSlotId, slotId, StringComparison.Ordinal))
            {
                basePlayTime = _loadedPlayTimeSeconds;
            }
            else if (_slotRegistry.TryGetCachedSlot(slotId, out var metadata))
            {
                basePlayTime = Math.Max(0d, metadata.PlayTimeSeconds);
            }

            var sessionSeconds = Math.Max(0d, Time.realtimeSinceStartup - _sessionStartRealtime);
            return basePlayTime + sessionSeconds;
        }

        private void ResetSessionClock(string slotId, double loadedPlayTimeSeconds)
        {
            _activeSlotId = ResolveSlotId(slotId);
            _loadedPlayTimeSeconds = Math.Max(0d, loadedPlayTimeSeconds);
            _sessionStartRealtime = Time.realtimeSinceStartup;
        }

        private async void MarkDefaultSlotDirtySafely()
        {
            if (_isMarkingDirty)
            {
                return;
            }

            _isMarkingDirty = true;
            try
            {
                await _dirtyTracker.MarkDirtyAsync(defaultSlotId);
                _dirtyTracker.CaptureProviderBaseline(_providerRegistry);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[NiumaSaveController] 自动标记脏状态失败：SlotId={defaultSlotId}, Error={ex.Message}", this);
            }
            finally
            {
                _isMarkingDirty = false;
            }
        }
    }
}
