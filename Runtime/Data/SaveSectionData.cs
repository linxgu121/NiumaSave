using System;

namespace NiumaSave.Data
{
    /// <summary>
    /// 单个模块的存档段。
    /// 该结构是模块快照进入 NiumaSave 的统一容器。
    /// </summary>
    [Serializable]
    public sealed class SaveSectionData
    {
        /// <summary>
        /// 模块存档段稳定 ID，例如 quest、gal、player、inventory。
        /// 不要使用资源名、场景名或可本地化文本作为 SectionId。
        /// </summary>
        public string SectionId;

        /// <summary>
        /// 模块自身的存档版本。
        /// 当某个模块快照结构变化时，用该字段进行模块内迁移。
        /// </summary>
        public string SectionVersion;

        /// <summary>
        /// Data 的序列化格式，例如 json、binary、compressed-json。
        /// 该字段只描述格式，不要求 NiumaSave 理解业务内容。
        /// </summary>
        public string Format;

        /// <summary>
        /// EncodedData 的编码方式。
        /// 第一版使用 base64，避免 JsonUtility 把 byte[] 展开成巨大的数字数组。
        /// </summary>
        public string DataEncoding = SaveDataEncoding.Base64;

        /// <summary>
        /// 模块快照序列化后再编码得到的文本数据。
        /// 不要把 Unity 对象引用、Dictionary、多态对象直接写入该数据。
        /// </summary>
        public string EncodedData;
    }
}
