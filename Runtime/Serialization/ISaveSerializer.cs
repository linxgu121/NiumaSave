using NiumaSave.Data;

namespace NiumaSave.Serialization
{
    /// <summary>
    /// 存档序列化接口。
    /// 只负责对象和字节之间的转换，不负责写文件、算 Checksum、云同步或模块快照收集。
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// 序列化格式标识，例如 json、binary、compressed-json。
        /// 该值会写入 SavePayload.Format 或 SaveSectionData.Format。
        /// </summary>
        string Format { get; }

        /// <summary>
        /// 序列化完整存档文档。
        /// </summary>
        SaveSerializationResult SerializeDocument(SaveGameDocument document);

        /// <summary>
        /// 反序列化完整存档文档。
        /// </summary>
        SaveDeserializationResult<SaveGameDocument> DeserializeDocument(byte[] data);

        /// <summary>
        /// 序列化模块快照 DTO。
        /// 模块适配器应传入显式字段映射后的纯数据对象，不要传运行时对象或 Unity 对象引用。
        /// </summary>
        SaveSerializationResult SerializeObject<T>(T value) where T : class;

        /// <summary>
        /// 反序列化模块快照 DTO。
        /// </summary>
        SaveDeserializationResult<T> DeserializeObject<T>(byte[] data) where T : class;
    }
}
