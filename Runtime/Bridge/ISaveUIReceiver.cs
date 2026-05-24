namespace NiumaSave.Bridge
{
    /// <summary>
    /// 存档 UI 接收接口。
    /// 具体 View 可以由 NiumaUI、UGUI、UI Toolkit 或其它框架实现，Save 模块只负责传递表现数据。
    /// </summary>
    public interface ISaveUIReceiver
    {
        /// <summary>
        /// 应用一次存档 UI 更新。
        /// 接收器内部不应直接触发保存、读取、删除等命令，避免刷新回流造成状态顺序混乱。
        /// </summary>
        void ApplySaveUpdate(SaveUIUpdate update);
    }
}
