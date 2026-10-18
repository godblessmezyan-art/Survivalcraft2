# Builder Assistant 架构说明（v0.8.0）

本文只描述 Builder Assistant 的自动建造基础，不涉及 SCIE-Port。目标是“可靠、可撤销的离线自动建造”，不做云、AI 或复杂 UI。v0.6.0 在不改动这条主线的前提下，专门处理“目标区块未加载或越界”这一 P0：把失败原因细分到每一格、允许主线程有限等待区块加载、并让回滚/撤销可追踪并重试未完成的部分。

## 设计原则

一条主线贯穿全部代码：**计划 → 校验 → 分批执行 → 记录撤销**，任何一步失败都在动世界之前停下。

三层严格分离，让安全规则可以脱离游戏被单元测试证明：

- `Core/`（纯 C#，零游戏程序集引用，`internal`）：坐标、蓝图、事务接口、计划器、任务、历史。
- `Features/Generators/`（纯 C#，`internal`）：把“造什么”表达成相对坐标蓝图，与地形无关。
- `Features/WorldEdit/`（唯一接触游戏的层，`internal`，除被游戏反射实例化的方块/行为类为 `public`）：把纯接口接到真实 `SubsystemTerrain/Terrain/BlocksManager`。

游戏反射只会实例化 `public` 的方块与行为类（通过 CSV 的 `Class Name` 与 XDB 的 `Class` 参数），其余保持 `internal`，因此开启 `GenerateDocumentationFile` + `TreatWarningsAsErrors` 时仍无缺失文档告警。

## 目录与职责

```
src/BuilderAssistant/
  Core/
    CellPos.cs                       整数格坐标（相对 & 绝对通用）
    Safety/WorldBounds.cs            世界高度上限 0..256
    Safety/BuildSafetyLimits.cs      单事务 1024 / 单批 32 / 单轴 64（保持不变）
    Blueprints/Blueprint.cs          相对坐标结构定义 + 旋转/镜像 Reorient
    Blueprints/BlueprintBuilder.cs   Set/FillBox/FillShell/Walls/Layer/Carve，确定性排序
    Blueprints/BlueprintCell.cs      (相对坐标 + 逻辑材料键)
    Blueprints/BuildingMaterials.cs  逻辑材料键常量
    Blueprints/BlueprintJson.cs      蓝图↔JSON(schemaVersion=1) 序列化/解析/校验，永不抛异常（v0.7.0，纯 Core）
    Blueprints/BlueprintLibraryBrowser.cs 蓝图库游标纯逻辑：把 (条目数, 当前游标) 映射为下一次点击要载入的索引，空库返回 -1、到末尾回绕；不含文件/游戏/UI 依赖，可离线单测（v0.8.0）
    Json/Json.cs                     零依赖严格 JSON 解析器 + 确定性写器（v0.7.0，net472 无 System.Text.Json）
    Transactions/ITerrainGateway.cs  唯一地形读写抽象（新增 InspectCell 诊断，测试用内存实现）
    Transactions/CellEditability.cs  每格不可编辑原因的枚举+结构（越界/缺失/状态/未加载，v0.6.0）
    Transactions/IBlockValueResolver.cs 材料键→打包方块值
    Transactions/BlockUndoRecord.cs  (绝对坐标, 写前旧值, 写入值)
    BuildEngine/BuildPlan.cs         计划结果（成功/失败原因/变更列表 + 不可编辑分类明细，v0.6.0）
    BuildEngine/BuildPlanner.cs      落地前全部安全校验；逐格分类不可编辑原因，任何失败都不写（v0.6.0 细化）
    BuildEngine/BuildTask.cs         分批执行、执行期复校、快照、失败回滚、取消、撤销；延迟回滚/撤销可重试（v0.6.0）
    BuildEngine/BuildTaskState.cs    Pending/Running/Completed/Failed/Cancelled/Undone
    BuildEngine/TransactionHistory.cs 至多 20 层撤销栈；部分撤销保留以便重试（v0.6.0）
  Features/
    Generators/IBuildingGenerator.cs / GenerationContext.cs / BuildingGeneratorRegistry.cs
    Generators/MedievalCottageGenerator.cs   首个可玩结构：9×10×11 小屋
    WorldEdit/GameTerrainGateway.cs  ITerrainGateway → 真实地形（ChangeCell 主线程写）；TypeNameForContents/DescribeContents 供复制读取（v0.7.0）
    WorldEdit/VanillaBlockPalette.cs 材料键 → FindBlockByTypeName → BlockIndex → 打包值；SupportedPlainCubes 保守白名单 + IsResolvableKey 预检（v0.7.0）
    WorldEdit/BlueprintCopier.cs     两角选区读实时方块→纯 Blueprint，仅复制白名单立方块，其余逐类跳过并报告（v0.7.0）
    WorldEdit/WorldEditRuntime.cs    主线程协调器：单人门控、双击确认、全范围预检+有限等待区块加载、分批 Tick、延迟回滚重试、撤销入口（v0.6.0）；RuntimeMode 小屋/复制/粘贴状态机 + 导入/导出/旋转入口（v0.7.0）；`OnBlueprintTool` + `CycleImportLibrary` 蓝图库逐次点击浏览/载入（v0.8.0）
    WorldEdit/BuilderToolBlock.cs(901) / UndoToolBlock.cs(902) / BlueprintToolBlock.cs(903，手持即浏览/导入蓝图，本身从不写地形)
    WorldEdit/SubsystemBuilderToolBlockBehavior.cs / SubsystemUndoToolBlockBehavior.cs / SubsystemBlueprintToolBlockBehavior.cs
  Infrastructure/
    BlueprintStorage.cs              蓝图 JSON 落盘：基目录取 ModsManager.UserDataPath + Mod 私有子目录，绝不硬编码设备路径；IO 全 try/catch（v0.7.0）
  Mod/BuilderAssistantModLoader.cs   注册 GuiUpdate + BlocksInitalized；快艇逻辑保持原样
```

## 一次建造的生命周期

1. 玩家手持“建造工具(901)”点击 → `SubsystemBuilderToolBlockBehavior.OnUse` 在主线程被触发 → `WorldEditRuntime.OnBuildTool`。
2. 先校验 `Game.NetWork.CommonLib.WorkType == Local`（单人测试世界才允许）。
3. `Raycast<TerrainRaycastResult>(Interaction)` 取命中格 `CellFace`；第一次点击只“选定点位”（居中：`ox=x-(SizeX-1)/2, oz=z-(SizeZ-1)/2, oy=命中格Y+1`），第二次点击才“确认施工”。这是明确确认，避免误触。
4. `MedievalCottageGenerator` 生成相对蓝图；`GameTerrainGateway` 包住当前 `SubsystemTerrain`。
5. `BuildPlanner.Plan` 逐格校验：数量≤1024、单轴≤64、高度在 `WorldBounds` 内、默认非空气不覆盖、材料键可解析，并用 `InspectCell` 给每一格不可编辑的原因归类（地形未就绪 / 越出有效域 / 高度越界 / 区块缺失 / 区块状态未就绪 / 区块未加载）。任一失败都**不产生任何写入**，但会带出明细原因。若失败纯粹是“区块尚未就绪”（可恢复，`AllEditabilityFailuresRecoverable`），`WorldEditRuntime` 不立即报错，而是进入**主线程有限等待**：记住蓝图与落点，等待期间绝不写任何方块，每帧重新 `Plan`，区块全部就绪即开始施工；判定转为硬性错误（越界/高度）立即放弃；超过 `MaxPreflightFrames` 仍未就绪则自动放弃并说明最后诊断。
6. 计划成功 → 建 `BuildTask`，`GuiUpdate` 每帧调用 `WorldEditRuntime.Tick`，每次最多写 `32` 格（分批，不卡主线程），每格写前抓旧值；执行期若某格变为不可编辑/非空气 → 立即失败并把已写格回滚。**v0.6.0**：回滚时若个别已写格所在区块恰好变为不可编辑，该格不会被悄悄跳过，而是登记为“延迟回滚”条目；失败/取消状态会保留 `BuildTask`，由后续 `Tick` 在区块恢复后调用 `RetryRollback` 继续补齐，只有全部补齐才报告“已干净回滚”。
7. 完成后写入 `TransactionHistory`（≤20 层）。

## 撤销语义

“撤销工具(902)”点击：若有正在进行的建造，先取消并回滚；否则撤销最近一次已完成事务。撤销只在“当前值仍等于本次建造写入值”时才恢复旧值，因此**不会覆盖玩家在建造之后手动改动过的方块**。**v0.6.0**：若某格因区块临时卸载而暂未恢复，事务不会被直接弹出——它会带着“延迟撤销”条目保留在栈上，待区块恢复后再次点击撤销即可续做；只有当没有待恢复项时才真正出栈。玩家在建造后改过的格子既不重试也不被覆盖。

## 区块未就绪的诊断与生存策略（v0.6.0）

P0 现象是点击建造后只回一句“目标区块未加载或越界”，既看不出到底哪里没就绪，也无法区分“等一等就好”和“落点本身越界、怎么等都没用”。根因有二：一是 `BuildPlanner` 过去把六种不同的“不可编辑”原因压成同一条消息；二是小屋占地 `9×11`，会横跨多个区块（chunk），准星命中的那一格已加载并不代表整片场地都已加载。

v0.6.0 用三件事应对，均不改“写前先全量校验”的铁律：

1. **逐格分类**：新增 `Core/Transactions/CellEditability.cs`，`ITerrainGateway.InspectCell` 返回 `地形未就绪 / 越出有效域 / 高度越界 / 区块缺失 / 区块状态未就绪 / 区块未加载` 之一，并附区块坐标、状态与 `IsLoaded`。`BuildPlan` 汇总这些明细，`Plan.FailureReason` 给出包围盒、分类统计与示例格，并判定是否“全部可恢复”。
2. **有限等待（预检）**：若失败纯因区块未就绪，`WorldEditRuntime` 在主线程做有界等待——记住落点与蓝图，每帧重新 `Plan`，等待期间不写任何方块，就绪即开工、转硬错误即停、超 `MaxPreflightFrames` 自动放弃。
3. **可追踪的部分回滚/撤销**：回滚或撤销时个别格暂时不可编辑，不再被静默跳过，而是登记为延迟条目，由后续帧或再次点击续做；如实报告“还有 N 格未恢复”，绝不谎报“已完成”。

## 蓝图 JSON 桥（v0.7.0 已实现）

这一层把“纯数据蓝图 ↔ JSON 文本 ↔ 磁盘 ↔ 真实世界”打通，是通向未来 AI 生成建筑的桥；核心承诺是**先能可靠地保存、重导入并复现同一建筑**，再接模型。

- 复制（读）：`RuntimeMode.Copy` 下两次点选对角 → `BlueprintCopier.Copy` 逐格读实时方块（只读，绝不损伤源结构）→ 只有 `VanillaBlockPalette.SupportedPlainCubes`（小屋已验证的纯立方方块）才纳入蓝图，其余按类型如实计入“暂不支持”并跳过 → 产出相对坐标 + 具体方块类型名作 material key 的纯 `Blueprint`，并 `BlueprintJson.Serialize` 落盘到 `ModsManager.UserDataPath` 下的 Mod 私有蓝图库。
- 存储（不硬编码路径）：`Infrastructure/BlueprintStorage` 的基目录来自游戏自带的 `ModsManager.UserDataPath`，再拼私有子目录；这也是第 10 步 Android 本地文件访问的答复——用游戏自己的路径 API，而不是写死 `/sdcard/...`。
- 导入 + 粘贴（写）：`ImportBlueprint` 读取并 `BlueprintJson.Decode`（永不抛异常，全量校验：整数坐标、无重复、材料 id 不悬空、数量/单轴上限、声明 size 与包围盒一致），再 `CollectUnresolvableKeys` 预检材料；`RuntimeMode.Paste` 定位后走 `BeginBuildBlueprint` → 与小屋**同一条** `PlanAndStart`：写前全量校验、有界区块等待、clear-soft、分批执行、延迟回滚/撤销、`TransactionHistory` 撤销，全部一致。旋转 `SetPasteRotation` 仅在蓝图空间 `Reorient`，落地不碰地形。
- 前向兼容但不造假：schema 预留 `o`/`d`/`props`（朝向/数据/复杂方块属性）与顶层 `buildOps` 字段位，但 v1 无法复现它们，故一律**响亮拒绝**而非硬凑劣质建筑（AGENTS：不写垃圾）。

生成侧的可扩展性同样保留：`Blueprint.Reorient` 支持旋转/镜像，`GenerationContext` 留 `Seed` 与 `MaterialOverrideKey`，新增结构只需再写一个 `IBuildingGenerator`。

## 外部 AI → 导入 → 落成 闭环（v0.8.0 已实现）

v0.7.0 打通了“蓝图↔JSON↔磁盘↔世界”的数据桥，但**游戏内没有触屏入口**去调用导入——`ImportBlueprint` 只能被程序化调用。v0.8.0 补上这最后一环，让“外部 AI 生成蓝图 → 丢进手机 → 导入 → 落成建筑”无需任何桌面工具即可闭环。

关键约束：x25.01.17 的 ABI 只暴露“点方块 `OnUse`”+“一行小消息 `DisplaySmallMessage`”，**没有文本框、文件选择器或对话框**。所以导入必须做成触屏交互。方案是新增第三个手持工具——蓝图工具（item 903，`BlueprintToolBlock`）：

- 每次 `OnUse`（点任意方块面即触发，命中点不作为选块，只借主线程）经 `WorldEditRuntime.OnBlueprintTool` → `CycleImportLibrary`：先用 `BlueprintStorage.ListNames` 读一次蓝图库。
- **空库**：把真实基目录 `BlueprintStorage.BaseDirectory` 打印一次（`ModsManager.UserDataPath` 下的 Mod 私有子目录，绝不硬编码设备路径），提示玩家用安卓自带文件管理器把 AI 生成的 `.json` 放进去（文件名限字母/数字/下划线/连字符，≤48，纯 ASCII）。
- **非空**：用纯逻辑 `BlueprintLibraryBrowser.Advance` 让游标前进一位（到末尾回绕到 0），并**立即 `ImportBlueprint` 载入该条**——内部 `BlueprintJson.Decode` 全量校验 + 材料预检。成功则播报“已载入蓝图 [i/N]「name」 尺寸 X×Y×Z，共 n 方块”，并给出下一步指引；非法/AI 劣质文件（坏 JSON、超界、不支持方块、材料悬空）则**响亮拒绝、绝不改动剪贴板、绝不部分导入**，游标仍前进以跳过坏文件。
- 载入后**仍用建造工具(901)对准地面两次点击落成**，走与小屋**完全相同**的 `PlanAndStart`→`BuildPlanner`→`BuildTask`→撤销链，全部安全规则一致；蓝图工具本身只读文件+校验，**从不写地形**。

两处**待验证**（真机）：① 安卓 scoped storage 下系统文件管理器能否写入 `UserDataPath`；② item 903 是否与设备上其它 mod 的索引冲突。

## 仍为预留（未来阶段）

- 台阶/半砖/带朝向原木/栅栏/植物/流体/含实体方块等的复制与粘贴：需先在真机逐块验证渲染与朝向数据位，再扩 `SupportedPlainCubes`。
- 设备内的正式触屏面板（显式模式切换 / 旋转选择器）：当前以“蓝图工具(903)逐次点击浏览+载入 + 空闲点撤销工具循环模式 + 程序化 `SetPasteRotation`”过渡，正式触屏面板待做。
- AI 建筑生产者：导入侧已由蓝图工具打通——模型只需产出 `schemaVersion=1` 的蓝图 JSON 并丢进蓝图库，即可经同一导入→校验→粘贴链落建；模型负责“设计”，可靠复现与安全性由本桥与既有引擎保证。
