using System;
using System.Security.Cryptography;
using System.Text;
using NiumaSave.Data;
using NiumaSave.Serialization;

namespace NiumaSave.Checksum
{
    /// <summary>
    /// 存档校验工具。
    /// 计算时会临时清空 Header.Checksum，让校验值覆盖除自身字段外的完整 SaveGameDocument 字节。
    /// </summary>
    public static class SaveChecksumUtility
    {
        /// <summary>
        /// 计算文档校验值。
        /// 该方法不会永久修改 document.Header.Checksum。
        /// </summary>
        public static SaveChecksumResult CalculateChecksum(
            SaveGameDocument document,
            ISaveSerializer serializer)
        {
            var validation = ValidateInput(document, serializer);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var originalChecksum = document.Header.Checksum;
            try
            {
                document.Header.Checksum = null;
                var serializeResult = serializer.SerializeDocument(document);
                if (!serializeResult.Succeeded)
                {
                    return SaveChecksumResult.Fail(
                        SaveChecksumErrorCode.SerializeFailed,
                        serializeResult.Message,
                        expectedChecksum: originalChecksum);
                }

                var checksum = ComputeSha256Hex(serializeResult.Data);
                return SaveChecksumResult.Success(checksum, originalChecksum);
            }
            catch (Exception ex)
            {
                return SaveChecksumResult.Fail(
                    SaveChecksumErrorCode.Unknown,
                    $"计算存档校验值失败：{ex.Message}",
                    expectedChecksum: originalChecksum);
            }
            finally
            {
                document.Header.Checksum = originalChecksum;
            }
        }

        /// <summary>
        /// 计算并写回文档校验值。
        /// 保存流程在序列化最终 SavePayload 前调用该方法。
        /// </summary>
        public static SaveChecksumResult ApplyChecksum(
            SaveGameDocument document,
            ISaveSerializer serializer)
        {
            var result = CalculateChecksum(document, serializer);
            if (!result.Succeeded)
            {
                return result;
            }

            document.Header.Checksum = result.Checksum;
            return result;
        }

        /// <summary>
        /// 验证文档中记录的校验值是否与重新计算结果一致。
        /// 读取流程应先完成基础结构校验，再调用该方法。
        /// </summary>
        public static SaveChecksumResult VerifyChecksum(
            SaveGameDocument document,
            ISaveSerializer serializer)
        {
            var validation = ValidateInput(document, serializer);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var expectedChecksum = document.Header.Checksum;
            if (string.IsNullOrWhiteSpace(expectedChecksum))
            {
                return SaveChecksumResult.Fail(
                    SaveChecksumErrorCode.EmptyChecksum,
                    "文档 Header.Checksum 为空，无法验证。",
                    expectedChecksum: expectedChecksum);
            }

            var calculated = CalculateChecksum(document, serializer);
            if (!calculated.Succeeded)
            {
                return calculated;
            }

            if (!string.Equals(calculated.Checksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                return SaveChecksumResult.Fail(
                    SaveChecksumErrorCode.Mismatch,
                    "文档 Checksum 与重新计算结果不一致。",
                    calculated.Checksum,
                    expectedChecksum);
            }

            return SaveChecksumResult.Success(calculated.Checksum, expectedChecksum);
        }

        private static SaveChecksumResult ValidateInput(
            SaveGameDocument document,
            ISaveSerializer serializer)
        {
            if (document == null)
            {
                return SaveChecksumResult.Fail(SaveChecksumErrorCode.NullDocument, "SaveGameDocument 为空。");
            }

            if (document.Header == null)
            {
                return SaveChecksumResult.Fail(SaveChecksumErrorCode.NullHeader, "SaveGameDocument.Header 为空。");
            }

            if (serializer == null)
            {
                return SaveChecksumResult.Fail(SaveChecksumErrorCode.NullSerializer, "ISaveSerializer 为空。");
            }

            return SaveChecksumResult.Success(null, document.Header.Checksum);
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(data ?? Array.Empty<byte>());
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
