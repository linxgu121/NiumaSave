namespace NiumaSave.Provider
{
    /// <summary>
    /// 存档段导入后的后处理钩子。
    /// 当所有 Provider 的 ImportSection 都成功后，SaveGameCoordinator 会同步调用该接口。
    /// 适合处理跨模块一致性修复，例如装备模块需要等背包段导入完成后再校验装备实例。
    /// </summary>
    public interface ISavePostImportHook
    {
        /// <summary>
        /// 所有存档段导入完成后执行后处理。
        /// 返回失败时，本次整体读档会被视为失败。
        /// </summary>
        SaveSectionImportResult OnAllSectionsImported();
    }
}
