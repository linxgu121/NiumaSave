using NiumaSave.Data;

namespace NiumaSave.Provider
{
    /// <summary>
    /// 模块存档数据提供者。
    /// 每个业务模块通过实现该接口，把自己的运行时事实导出为 SaveSectionData，并能从 SaveSectionData 恢复。
    /// </summary>
    public interface ISaveDataProvider
    {
        /// <summary>
        /// 存档段稳定 ID，例如 quest、gal、player、inventory。
        /// </summary>
        string SectionId { get; }

        /// <summary>
        /// 当前模块快照结构版本。
        /// 用于模块内迁移，不等同于游戏版本。
        /// </summary>
        string SectionVersion { get; }

        /// <summary>
        /// 模块运行时数据修订号。
        /// NiumaSave 可以用它轻量判断模块是否发生变化，而不是每帧导出完整快照。
        /// </summary>
        int Revision { get; }

        /// <summary>
        /// 导出模块存档段。
        /// 实现层必须显式字段映射，不要直接序列化运行时对象或 Unity 对象引用。
        /// </summary>
        SaveSectionData ExportSection();

        /// <summary>
        /// 导入模块存档段。
        /// 失败时返回失败结果，方便存档流程记录错误并阻止静默坏档。
        /// </summary>
        SaveSectionImportResult ImportSection(SaveSectionData section);
    }
}
