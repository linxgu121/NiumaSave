using System;
using System.Collections.Generic;
using NiumaSave.Data;

namespace NiumaSave.Provider
{
    /// <summary>
    /// 存档数据提供者注册表。
    /// 负责管理各模块的 ISaveDataProvider，不负责序列化、写文件或云同步。
    /// </summary>
    public sealed class SaveDataProviderRegistry
    {
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, ISaveDataProvider> _providers = new(StringComparer.Ordinal);
        private readonly List<ISaveDataProvider> _providerCache = new();

        /// <summary>
        /// 当前已注册 Provider 数量。
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _providers.Count;
                }
            }
        }

        /// <summary>
        /// 注册模块存档提供者。
        /// replaceExisting 为 false 时，重复 SectionId 会注册失败，避免两个模块写入同一个存档段。
        /// </summary>
        public bool Register(ISaveDataProvider provider, bool replaceExisting = false)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.SectionId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (_providers.ContainsKey(provider.SectionId) && !replaceExisting)
                {
                    return false;
                }

                _providers[provider.SectionId] = provider;
                RebuildProviderCache();
                return true;
            }
        }

        /// <summary>
        /// 取消注册指定 SectionId 的 Provider。
        /// </summary>
        public bool Unregister(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                var removed = _providers.Remove(sectionId);
                if (removed)
                {
                    RebuildProviderCache();
                }

                return removed;
            }
        }

        /// <summary>
        /// 查询指定 SectionId 的 Provider。
        /// </summary>
        public bool TryGetProvider(string sectionId, out ISaveDataProvider provider)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
            {
                provider = null;
                return false;
            }

            lock (_syncRoot)
            {
                return _providers.TryGetValue(sectionId, out provider);
            }
        }

        /// <summary>
        /// 获取当前 Provider 快照。
        /// 返回的是 Provider 快照数组，调用方可以安全遍历，但不要把它当成实时列表。
        /// </summary>
        public IReadOnlyList<ISaveDataProvider> GetProviders()
        {
            lock (_syncRoot)
            {
                return _providerCache.ToArray();
            }
        }

        /// <summary>
        /// 导出所有 Provider 的存档段。
        /// 该方法只收集数据，不负责组装完整 SaveGameDocument。
        /// </summary>
        public SaveProviderExportResult ExportAllSectionsWithResult()
        {
            ISaveDataProvider[] providers;
            lock (_syncRoot)
            {
                if (_providerCache.Count == 0)
                {
                    return SaveProviderExportResult.Success(Array.Empty<SaveSectionData>());
                }

                providers = _providerCache.ToArray();
            }

            if (providers.Length == 0)
            {
                return SaveProviderExportResult.Success(Array.Empty<SaveSectionData>());
            }

            var sections = new List<SaveSectionData>(providers.Length);
            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                SaveSectionData section;
                try
                {
                    section = provider?.ExportSection();
                }
                catch (Exception ex)
                {
                    return SaveProviderExportResult.Fail(
                        SaveProviderExportErrorCode.ExportFailed,
                        $"Provider {provider?.SectionId ?? "<null>"} 导出存档段时发生异常：{ex.Message}");
                }

                if (section == null)
                {
                    return SaveProviderExportResult.Fail(
                        SaveProviderExportErrorCode.NullSection,
                        $"Provider {provider?.SectionId ?? "<null>"} 导出了空 Section。");
                }

                if (string.IsNullOrWhiteSpace(section.SectionId))
                {
                    return SaveProviderExportResult.Fail(
                        SaveProviderExportErrorCode.EmptySectionId,
                        $"Provider {provider.SectionId} 导出的 SectionId 为空。");
                }

                if (!string.Equals(section.SectionId, provider.SectionId, StringComparison.Ordinal))
                {
                    return SaveProviderExportResult.Fail(
                        SaveProviderExportErrorCode.SectionIdMismatch,
                        $"Provider SectionId={provider.SectionId}，但导出 SectionId={section.SectionId}。");
                }

                sections.Add(section);
            }

            return SaveProviderExportResult.Success(sections.Count == 0 ? Array.Empty<SaveSectionData>() : sections.ToArray());
        }

        /// <summary>
        /// 导出所有 Provider 的存档段。
        /// 保留该方法用于简单调用；正式保存流程应优先使用 ExportAllSectionsWithResult 获取错误码。
        /// </summary>
        public SaveSectionData[] ExportAllSections()
        {
            var result = ExportAllSectionsWithResult();
            return result.Succeeded ? result.Sections : Array.Empty<SaveSectionData>();
        }

        /// <summary>
        /// 清空所有 Provider 注册关系。
        /// 通常只在切换上下文或销毁存档模块时使用。
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                _providers.Clear();
                _providerCache.Clear();
            }
        }

        private void RebuildProviderCache()
        {
            _providerCache.Clear();
            foreach (var provider in _providers.Values)
            {
                _providerCache.Add(provider);
            }

            // 按 SectionId 固定导出顺序，避免同一批数据因为注册顺序不同导致存档字节和 Checksum 抖动。
            _providerCache.Sort((left, right) => string.CompareOrdinal(left.SectionId, right.SectionId));
        }
    }
}
