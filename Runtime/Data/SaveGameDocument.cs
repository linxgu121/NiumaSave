using System;

namespace NiumaSave.Data
{
    /// <summary>
    /// 完整存档文档。
    /// 该对象只作为 NiumaSave 内部组装和序列化使用，业务模块不要直接持有或修改它。
    /// </summary>
    [Serializable]
    public sealed class SaveGameDocument
    {
        /// <summary>
        /// 存档头信息，包含槽位、版本、时间、校验值等元数据。
        /// </summary>
        public SaveGameHeader Header = new SaveGameHeader();

        /// <summary>
        /// 各模块导出的存档段。
        /// 每个 Section 对应一个稳定模块 ID，例如 quest、gal、player、inventory。
        /// </summary>
        public SaveSectionData[] Sections = Array.Empty<SaveSectionData>();
    }
}
