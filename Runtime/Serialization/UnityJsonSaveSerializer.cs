using System;
using System.Text;
using NiumaSave.Data;
using UnityEngine;

namespace NiumaSave.Serialization
{
    /// <summary>
    /// 基于 Unity JsonUtility 的存档序列化器。
    /// 只负责对象和 UTF8 JSON 字节之间的转换，不负责文件写入、Checksum、Provider 收集或 Base64 Section 编码。
    /// </summary>
    public sealed class UnityJsonSaveSerializer : ISaveSerializer
    {
        public const string FormatName = "json";

        /// <summary>
        /// 序列化格式标识。
        /// </summary>
        public string Format => FormatName;

        /// <summary>
        /// 序列化完整存档文档。
        /// </summary>
        public SaveSerializationResult SerializeDocument(SaveGameDocument document)
        {
            if (document == null)
            {
                return SaveSerializationResult.Fail(Format, SaveSerializationErrorCode.NullInput, "SaveGameDocument 为空，无法序列化。");
            }

            if (document.Header == null)
            {
                return SaveSerializationResult.Fail(Format, SaveSerializationErrorCode.InvalidDocument, "SaveGameDocument.Header 为空，无法序列化。");
            }

            if (document.Sections == null)
            {
                document.Sections = Array.Empty<SaveSectionData>();
            }

            return SerializeObject(document);
        }

        /// <summary>
        /// 反序列化完整存档文档。
        /// </summary>
        public SaveDeserializationResult<SaveGameDocument> DeserializeDocument(byte[] data)
        {
            var result = DeserializeObject<SaveGameDocument>(data);
            if (!result.Succeeded)
            {
                return result;
            }

            if (result.Value == null)
            {
                return SaveDeserializationResult<SaveGameDocument>.Fail(
                    SaveSerializationErrorCode.InvalidDocument,
                    "反序列化结果为空。");
            }

            if (result.Value.Header == null)
            {
                return SaveDeserializationResult<SaveGameDocument>.Fail(
                    SaveSerializationErrorCode.InvalidDocument,
                    "SaveGameDocument.Header 为空。");
            }

            if (result.Value.Sections == null)
            {
                result.Value.Sections = Array.Empty<SaveSectionData>();
            }

            return SaveDeserializationResult<SaveGameDocument>.Success(result.Value);
        }

        /// <summary>
        /// 序列化模块快照 DTO。
        /// </summary>
        public SaveSerializationResult SerializeObject<T>(T value) where T : class
        {
            if (value == null)
            {
                return SaveSerializationResult.Fail(Format, SaveSerializationErrorCode.NullInput, $"{typeof(T).Name} 为空，无法序列化。");
            }

            try
            {
                var json = JsonUtility.ToJson(value);
                if (string.IsNullOrEmpty(json))
                {
                    return SaveSerializationResult.Fail(Format, SaveSerializationErrorCode.SerializeFailed, $"{typeof(T).Name} 序列化结果为空。");
                }

                return SaveSerializationResult.Success(Format, Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                return SaveSerializationResult.Fail(
                    Format,
                    SaveSerializationErrorCode.SerializeFailed,
                    $"{typeof(T).Name} 序列化失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 反序列化模块快照 DTO。
        /// </summary>
        public SaveDeserializationResult<T> DeserializeObject<T>(byte[] data) where T : class
        {
            if (data == null || data.Length == 0)
            {
                return SaveDeserializationResult<T>.Fail(SaveSerializationErrorCode.EmptyData, $"{typeof(T).Name} 反序列化数据为空。");
            }

            try
            {
                var json = Encoding.UTF8.GetString(data);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return SaveDeserializationResult<T>.Fail(SaveSerializationErrorCode.EmptyData, $"{typeof(T).Name} JSON 文本为空。");
                }

                var value = JsonUtility.FromJson<T>(json);
                if (value == null)
                {
                    return SaveDeserializationResult<T>.Fail(SaveSerializationErrorCode.DeserializeFailed, $"{typeof(T).Name} 反序列化结果为空。");
                }

                return SaveDeserializationResult<T>.Success(value);
            }
            catch (Exception ex)
            {
                return SaveDeserializationResult<T>.Fail(
                    SaveSerializationErrorCode.DeserializeFailed,
                    $"{typeof(T).Name} 反序列化失败：{ex.Message}");
            }
        }
    }
}
