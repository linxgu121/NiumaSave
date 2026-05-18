using System;
using System.Collections.Generic;
using NiumaCore.Save;
using NiumaSave.Checksum;
using NiumaSave.Data;
using NiumaSave.Provider;
using NiumaSave.Serialization;

namespace NiumaSave.Service
{
    /// <summary>
    /// 游戏存档协调器。
    /// 负责收集 Provider、组装 SaveGameDocument、序列化为 SavePayload，以及把读取到的 Payload 导入回 Provider。
    /// </summary>
    public sealed class SaveGameCoordinator
    {
        private readonly SaveDataProviderRegistry _providerRegistry;
        private readonly ISaveSerializer _serializer;

        public SaveGameCoordinator(SaveDataProviderRegistry providerRegistry, ISaveSerializer serializer)
        {
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 根据当前 Provider 快照构建完整 SavePayload。
        /// </summary>
        public SaveGameBuildResult BuildPayload(SaveSlotMetadata metadata)
        {
            var metadataError = ValidateMetadata(metadata);
            if (!string.IsNullOrEmpty(metadataError))
            {
                return SaveGameBuildResult.Fail(metadataError);
            }

            var exportResult = _providerRegistry.ExportAllSectionsWithResult();
            if (!exportResult.Succeeded)
            {
                return SaveGameBuildResult.Fail(exportResult.Message);
            }

            var document = new SaveGameDocument
            {
                Header = CreateHeader(metadata),
                Sections = exportResult.Sections ?? Array.Empty<SaveSectionData>()
            };

            var structureError = ValidateDocumentForSave(document);
            if (!string.IsNullOrEmpty(structureError))
            {
                return SaveGameBuildResult.Fail(structureError);
            }

            var checksumResult = SaveChecksumUtility.ApplyChecksum(document, _serializer);
            if (!checksumResult.Succeeded)
            {
                return SaveGameBuildResult.Fail(checksumResult.Message);
            }

            var serializeResult = _serializer.SerializeDocument(document);
            if (!serializeResult.Succeeded)
            {
                return SaveGameBuildResult.Fail(serializeResult.Message);
            }

            metadata.Checksum = document.Header.Checksum;
            var payload = new SavePayload
            {
                Metadata = metadata,
                Format = _serializer.Format,
                Data = serializeResult.Data
            };

            return SaveGameBuildResult.Success(payload);
        }

        /// <summary>
        /// 将读取到的 SavePayload 导入到已注册 Provider。
        /// </summary>
        public SaveGameImportResult ImportPayload(SavePayload payload)
        {
            if (payload == null)
            {
                return SaveGameImportResult.Fail("SavePayload 为空。");
            }

            if (payload.Data == null || payload.Data.Length == 0)
            {
                return SaveGameImportResult.Fail("SavePayload.Data 为空。");
            }

            if (!string.IsNullOrWhiteSpace(payload.Format)
                && !string.Equals(payload.Format, _serializer.Format, StringComparison.Ordinal))
            {
                return SaveGameImportResult.Fail($"存档格式不匹配：payload={payload.Format}, serializer={_serializer.Format}");
            }

            var deserializeResult = _serializer.DeserializeDocument(payload.Data);
            if (!deserializeResult.Succeeded)
            {
                return SaveGameImportResult.Fail(deserializeResult.Message);
            }

            var document = deserializeResult.Value;
            var structureError = ValidateDocumentForLoad(document);
            if (!string.IsNullOrEmpty(structureError))
            {
                return SaveGameImportResult.Fail(structureError);
            }

            var checksumResult = SaveChecksumUtility.VerifyChecksum(document, _serializer);
            if (!checksumResult.Succeeded)
            {
                return SaveGameImportResult.Fail(checksumResult.Message);
            }

            var importedProviders = new List<ISaveDataProvider>(document.Sections.Length);
            for (var i = 0; i < document.Sections.Length; i++)
            {
                var section = document.Sections[i];
                if (!_providerRegistry.TryGetProvider(section.SectionId, out var provider))
                {
                    return SaveGameImportResult.Fail($"没有找到 SectionId={section.SectionId} 的存档 Provider。");
                }

                var importResult = provider.ImportSection(section);
                if (!importResult.Succeeded)
                {
                    return SaveGameImportResult.Fail($"导入 SectionId={section.SectionId} 失败：{importResult.Message}");
                }
                importedProviders.Add(provider);
            }

            return RunPostImportHooks(importedProviders);
        }

        private static SaveGameImportResult RunPostImportHooks(IReadOnlyList<ISaveDataProvider> importedProviders)
        {
            if (importedProviders == null || importedProviders.Count == 0)
            {
                return SaveGameImportResult.Success();
            }

            for (var i = 0; i < importedProviders.Count; i++)
            {
                if (importedProviders[i] is not ISavePostImportHook hook)
                {
                    continue;
                }

                SaveSectionImportResult hookResult;
                try
                {
                    hookResult = hook.OnAllSectionsImported();
                }
                catch (Exception ex)
                {
                    return SaveGameImportResult.Fail(
                        $"SectionId={importedProviders[i].SectionId} 的导入后处理发生异常：{ex.Message}");
                }

                if (!hookResult.Succeeded)
                {
                    return SaveGameImportResult.Fail(
                        $"SectionId={importedProviders[i].SectionId} 的导入后处理失败：{hookResult.Message}");
                }
            }

            return SaveGameImportResult.Success();
        }

        private static SaveGameHeader CreateHeader(SaveSlotMetadata metadata)
        {
            return new SaveGameHeader
            {
                SaveFormatVersion = "1",
                GameVersion = UnityEngine.Application.version,
                SlotId = metadata.SlotId,
                DisplayName = metadata.DisplayName,
                UserId = metadata.UserId,
                Revision = metadata.Revision,
                SavedAtUnixSeconds = metadata.UpdatedAtUnixSeconds,
                PlayTimeMilliseconds = (long)Math.Max(0, metadata.PlayTimeSeconds * 1000d),
                DeviceId = metadata.DeviceId,
                Checksum = null
            };
        }

        private static string ValidateMetadata(SaveSlotMetadata metadata)
        {
            if (metadata == null)
            {
                return "SaveSlotMetadata 为空。";
            }

            if (string.IsNullOrWhiteSpace(metadata.SlotId))
            {
                return "SaveSlotMetadata.SlotId 为空。";
            }

            return null;
        }

        private static string ValidateDocumentForSave(SaveGameDocument document)
        {
            var commonError = ValidateDocumentCommon(document);
            if (!string.IsNullOrEmpty(commonError))
            {
                return commonError;
            }

            return ValidateSections(document.Sections);
        }

        private static string ValidateDocumentForLoad(SaveGameDocument document)
        {
            var commonError = ValidateDocumentCommon(document);
            if (!string.IsNullOrEmpty(commonError))
            {
                return commonError;
            }

            if (string.IsNullOrWhiteSpace(document.Header.Checksum))
            {
                return "SaveGameDocument.Header.Checksum 为空。";
            }

            return ValidateSections(document.Sections);
        }

        private static string ValidateDocumentCommon(SaveGameDocument document)
        {
            if (document == null)
            {
                return "SaveGameDocument 为空。";
            }

            if (document.Header == null)
            {
                return "SaveGameDocument.Header 为空。";
            }

            if (string.IsNullOrWhiteSpace(document.Header.SlotId))
            {
                return "SaveGameDocument.Header.SlotId 为空。";
            }

            if (string.IsNullOrWhiteSpace(document.Header.SaveFormatVersion))
            {
                return "SaveGameDocument.Header.SaveFormatVersion 为空。";
            }

            if (document.Sections == null)
            {
                document.Sections = Array.Empty<SaveSectionData>();
            }

            return null;
        }

        private static string ValidateSections(SaveSectionData[] sections)
        {
            var sectionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    return $"Sections[{i}] 为空。";
                }

                if (string.IsNullOrWhiteSpace(section.SectionId))
                {
                    return $"Sections[{i}].SectionId 为空。";
                }

                if (!sectionIds.Add(section.SectionId))
                {
                    return $"重复的 SectionId：{section.SectionId}";
                }

                if (!string.Equals(section.DataEncoding, SaveDataEncoding.Base64, StringComparison.Ordinal))
                {
                    return $"SectionId={section.SectionId} 的 DataEncoding 不支持：{section.DataEncoding}";
                }

                if (string.IsNullOrEmpty(section.EncodedData))
                {
                    return $"SectionId={section.SectionId} 的 EncodedData 为空。";
                }
            }

            return null;
        }
    }
}
