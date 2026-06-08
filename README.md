# NiumaSave

## 模块定位
NiumaSave 是统一存档模块，负责收集各模块 SaveAdapter 提供的快照，组装 SaveGameDocument，序列化、校验、写入本地文件，并预留云同步策略。

## 框架设计思路
- 各模块只导出自己的 Section，不知道完整存档文件结构。
- SaveController 负责协调 Provider、Serializer、LocalSaveService 和 SlotPolicy。
- Section 数据采用 Base64 包装，避免 byte[] 被 JsonUtility 展开成数字数组。
- 自动、检查点、手动、备份槽位通过策略类管理，不散落到业务模块。

## 核心流程
1. 模块 SaveAdapter 注册到 SaveDataProviderRegistry。
2. SaveController.SaveGameAsync 收集所有 Section。
3. SaveGameCoordinator 组装 Header、Sections、Checksum。
4. Serializer 输出 SavePayload。
5. LocalFileSaveService 原子写入 tmp -> target。
6. Load 时反序列化 Document，再逐个 Provider 导入 Section，必要时执行 PostImportHook。

## 模块用法
- 新模块接入存档时，优先挂该模块提供的 SaveAdapter 脚本，例如 `NiumaInventorySaveAdapter`、`NiumaQuestSaveAdapter`、`NiumaGalSaveAdapter`。如果是新写模块，再由程序新增对应 SaveAdapter。
- 导出失败应抛清晰异常或返回结构化失败，调用方负责整批存档容错。
- 临时运行态不要写入 Section，如临时容器、瞬时 SFX、播放中 UI。

## 场景使用方法
推荐放置方式：`SaveRoot` 一个全局物体承载保存系统，所有 SaveAdapter 可集中放子物体方便检查。

- `SaveRoot`：挂 `NiumaSaveController`，配置存档根目录、SlotPolicy、Serializer、LocalFileSaveService 等。
- `SaveRoot/Providers`：集中挂各模块 SaveAdapter，例如 `NiumaInventorySaveAdapter`、`NiumaQuestSaveAdapter`、`NiumaGalSaveAdapter`。
- `SaveRoot/Debug`：开发阶段挂 `NiumaSaveDebugEntry`，验证保存、读取、删除和脏标记。
- `UIRoot/SavePanel`：挂存档面板 Receiver。
- `UIRoot/Bridges/SaveUIViewBridge`：挂 `SaveUIViewBridge`，把槽位快照转换成 UI 数据。
- `SceneRoot/CheckpointRequester`：若场景切换或检查点要触发保存，挂 `NiumaSceneSaveCheckpointRequester` 并绑定 SaveController。
- 模块 Controller 和 SaveAdapter 可以同物体挂载，但大型场景建议集中到 `SaveRoot/Providers`，更方便确认哪些 Section 已注册。

## 协作边界
NiumaSave 不理解任务、背包、装备的业务含义，只保存它们提供的快照。云同步冲突协调放在更高层策略，不让本地文件服务感知服务器状态。

## 场景挂载与 Inspector 配置
### NiumaSaveController
建议挂载位置：`CoreScene/BootstrapRoot/SaveRoot`。

用途：统一收集各模块 SaveAdapter，组装 SaveGameDocument，写入本地存档，并提供自动、检查点、手动槽位策略。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Local Save Root Directory` | 通常留空 | 可以 | 留空时使用 `Application.persistentDataPath/NiumaSave` |
| `User Id` | 单机可填 `local_user` | 不建议 | 为空时可能使用默认用户，后续云同步不清晰 |
| `Auto Save Interval Seconds` | 按需求设置 | 可以为 0 | 为 0 或关闭自动保存时不会定时存档 |
| `Provider Registry` | 通常由 Controller 内部维护 | 可以 | SaveAdapter 注册后才有 Provider |
| `Slot Policy` | 默认策略即可 | 可以 | 没有特殊策略时使用默认槽位规则 |

### 各模块 SaveAdapter
建议挂载位置：`CoreScene/BootstrapRoot/SaveRoot/SaveAdapters`。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Module Controller` | 拖对应模块 Controller | 不建议 | 该模块不会导出/导入存档 |
| `Save Controller` | 拖 `NiumaSaveController` | 不建议 | 无法注册到 ProviderRegistry |
| `Auto Find References` | 测试可开，正式建议手动绑定 | 可以 | 关闭且未绑定时适配器不工作 |

### SaveUIViewBridge
建议挂载位置：存档列表 UI 面板。

| 字段 | 怎么填 | 可否留空 | 不填会怎样 |
| --- | --- | --- | --- |
| `Save Controller` | 拖 `NiumaSaveController` | 不建议 | UI 无法显示槽位 |
| `Receiver Provider` | 拖存档 UI 接收脚本 | 不可以 | 存档列表无处显示 |
| `Selected Slot Id` | 默认选中槽位 | 可以 | 留空时桥接层尝试选第一个槽位 |


