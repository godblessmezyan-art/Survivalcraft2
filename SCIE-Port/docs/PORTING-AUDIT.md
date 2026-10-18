# SCIE → SCNET x25.01.17 移植审计

## Kelly Expansion 植物资源（0.6.0）

用户声明已取得移植许可。范围严格限定为 14 个植物/果实条目、植物图集和果实模型；原 DLL/PDB 及矿物、工具、服装、陷阱、生物等系统均未纳入。实现已针对 SCNET x25.01.17 重写并置于 `Features/KellyPlants`，物品名称保持来源原名且不附加括号移植标注。来源版本、资源哈希与公开分发注意事项见 `THIRD-PARTY-NOTICES.md`。

审计日期：2026-10-17。兼容性状态只使用“已确认 / 待验证 / 不支持”。本报告中的“已确认”只表示列出的证据成立，不自动代表 Windows、Android 或联机可运行。

## 基线、范围与许可边界

- 上游：`https://github.com/Lixue9jiu/SCIE`
- 固定提交：`4864d956793b6e670413019f1f6b1cc5c8bb0970`
- 提交时间：`2019-05-03T16:07:19+08:00`
- 本地只读研究副本：`upstream/SCIE`，已由 `.gitignore` 排除。
- 上游固定树中没有 `LICENSE`、`COPYING` 或等效授权文件。GitHub 仓库公开可读不等于获得复制、修改或再发布许可。因此上游源码、17 个 DAE、11 个 PNG、OGG、翻译、配方和 XDB 均只作为行为线索，不进入当前 `.netmod`。
- 当前 `src/SCIEPort` 的代码是面向目标 ABI 的独立移植实现；0.3.0 按用户声明的许可打包 SCIE 原蒸汽船模型与纹理，不直接编译旧源码。

SCIE 自述其基于 SCPluginAPI。SCPluginAPI 仓库采用 MIT，但 SCIE 没有记录其所依赖的 SCPluginAPI commit，也没有把 MIT 自动授予 SCIE 自身内容。许可细节见 `DEPENDENCIES.md`。

## 工程与规模

原 `SCIE.csproj` 是旧式 MSBuild 工程，目标 `.NET Framework 4.5`、AnyCPU、允许 unsafe、语言版本 7.1，输出目录及 `Engine.dll`、`EntitySystem.dll`、`Survivalcraft.exe` 引用硬编码到 `D:\Survivalcraft`。它还直接引用 `Common/Jint.dll`，未通过可复现的包清单管理依赖。

| 模块 | C# 文件 | 约代码行 | 审计结论 |
|---|---:|---:|---|
| `Blocks/` | 26 | 3612 | 数据模型可作规格；注册、渲染签名和旧 Hook 必须改造 |
| `Chemistry/` | 5 | 1063 | 纯解析/值对象可重写验证；方块与资源层必须重写 |
| `Circuit/` | 8 | 1531 | 算法概念可保留；调度、线程、方块实体和网络必须重写 |
| `Components/` | 28 | 4688 | 大部分直接依赖实体模板、库存、玩家和更新接口，必须逐组件移植 |
| `Subsystems/` | 19 | 1483 | 与世界、生成、交互和实体生命周期强耦合，必须重写适配 |
| `Widgets/` | 19 | 1140 | XML/控件接口与触屏适配待逐项验证 |
| `Common/` | 3 | 471 | `Utils` 使用大量静态全局子系统引用，不宜原样复用 |

## 旧加载机制

真实入口位于 `Blocks/ItemBlock.cs:14-25`：`[PluginLoader("IndustrialMod", "", 0u)]` 标记一个静态初始化类，并把自定义实现赋给旧版或经 SCPluginAPI 修改过的全局委托字段：

- `BlocksManager.DamageItem1`
- `BlocksManager.FindBlocksByCraftingId1`
- `CraftingRecipesManager.Initialize1`
- `CraftingRecipesManager.DecodeResult1`
- `CraftingRecipesManager.MatchRecipe1`
- `CraftingRecipesManager.TransformRecipe1`

目标 x25.01.17 中不存在 `PluginLoaderAttribute`。实际加载方式是：程序集扫描 `Game.ModLoader` 子类并调用 `__ModInitialize()`；同时扫描带公开静态 `int Index` 的 `Block` 子类，再由 `ModEntity.LoadBlocksData()` 合并 `.csv`。目标仅保留了部分旧痕迹（例如 `CraftingRecipesManager.Initialize1`），不能把它当成稳定扩展契约。

结论：旧入口为“不支持”；使用 SCNET `ModLoader`、CSV、XDB 合并和明确 Hook 的新入口为“已确认”。

## 方块 ID 与数据布局

目标程序集的 `BlocksManager.m_blocks` 长度为 1024。实际反射读取到 260 个原生方块常量，最高原生 ID 为 263。

SCIE 声明的 ID：

| ID | SCIE 类型/用途 | x25.01.17 情况 | 结论 |
|---:|---|---|---|
| 67 | `BasaltBlock` | 原生 ID 已占用 | 不支持原样注册 |
| 109 | `GunpowderBlock` 扩展 | 原生 ID 已占用 | 必须改为扩展策略或新 ID |
| 214 | `BulletBlock` 扩展 | 原生 `BulletBlock` | 命名和 ID 同时冲突 |
| 240 | `OilBlock` | 原生 `RottenMeatBlock` | 严重存档冲突 |
| 246 | `ItemBlock` / `RottenEggBlock` | 原生 `RottenEggBlock` | SCIE 以数据值复用该槽，必须迁移映射 |
| 501–534 | 工业方块、机器、工具、电路、化学 | 目标原生表为空 | 可建立新注册表，但不能假设其他 Mod 未占用 |
| 1000 | 当前 clean-room 测试材料 | 目标原生表为空 | 仅作构建/加载探针，发布前仍需全 Mod 冲突策略 |

目标与旧源码都使用同一基础位布局：`contents = value & 0x3FF`，`light = (value & 0x3C00) >> 10`，`data` 从第 14 位开始。这只证明位移基础兼容，不证明各 SCIE 子类型编码可直接继承。

需要建立显式旧值迁移器的主要编码包括：

- `ItemBlock`：ID 246，`data` 直接作为 `Items[]` 下标；伤害使用 data 位 4–15，染色使用位 10–13。
- `ElementBlock`：ID 501，低 10 位设备号，方向从 data 位 15 起，染色另占高位。
- `ChemicalBlock`：ID 517，`data` 直接索引动态生成的化学物列表。
- `GunpowderBlock`：扩展到 2048 个地雷变体，类型为低 6 位、延时由更高位换算。
- `MetalBlock`、`DrillBlock`、多方向机器、铁轨、枪械各自复用不同 data 位。

这些布局必须先冻结为版本化清单，再为旧世界导入建立“旧内容 ID + 旧 data → 新稳定标识 → 新值”的显式转换；禁止直接打开主存档试错。

## `Items.csv`、XDB 与注册系统

上游根 `Items.csv` 只有两个条目：`RottenEggBlock` 与 `RottenMeatBlock`。大多数工业物品不是 CSV 独立方块，而是 `ItemBlock.Items[]`、`ElementBlock.Devices[]` 和 `ChemicalBlock.Items[]` 中由 data 下标区分的变体。其显示、配方和存档稳定性依赖数组顺序，插入项目会改变所有后续含义。

`工业mod.xdb` 包含大量实体和组件模板，并在文件开头通过固定 GUID 替换七个原组件类，其中包括：

- `Game.ComponentNPlayer`
- `Game.ComponentNGui`
- `Game.ComponentLargeCraftingTable`
- `Game.ComponentFurnaceN`
- 三个原版行为子系统替换

目标 SCNET 的 `ModEntity.LoadXdb()` / `ModsManager.CombineDataBase()` 仍能按 GUID 合并和处理 `new-*` 属性，接口存在已确认；但上游 GUID 来自旧数据库，尚未逐一对应 x25 数据库，所有旧 XDB 的直接加载状态为“不支持”。新移植必须只增加自己的实体/组件模板，不替换原版玩家、GUI 或基础炉子组件。

## 实体生命周期与存档

机器通常由方块行为在放置时创建实体，通过 `ComponentBlockEntity` 关联坐标，交互时查找库存组件；不少组件实现 `Load`，但只有一部分实现 `Save`。例如 `ComponentDriller`、`ComponentUnloader`、`ComponentMachine`、`ComponentTrain` 有显式 Save，而多种炉类只保存其基类库存或完全依赖基类行为。必须逐组件制作存档字段清单和往返测试，不能假定旧模板会自动恢复运行状态。

交通工具由 `SubsystemItemBlockBehavior` 调用 `DatabaseManager.CreateEntity(...)` 创建 `SteamBoat`、`Train`、`Carriage`、`Airship`、`Mine` 等实体再加入 Project。目标 `DatabaseManager.CreateEntity(Project,string,bool)` 与带 overrides 的重载仍存在，接口线索可复用；但实体模板、骑乘、输入、断线和权威同步必须重新设计。

## `ComponentNPlayer` 风险

`Components/ComponentPlayer.cs` 定义 `ComponentNPlayer : ComponentPlayer, IUpdateable` 并重新实现整段玩家更新逻辑；旧 XDB 把原玩家类替换为它。目标 `ComponentPlayer` 已包含 `PlayerGuid`、网络化的 aim/dig/interact 事件队列和触控输入处理。替换它会绕过 SCNET 联机与触屏逻辑，且 `new void Update` 不是安全扩展点。

结论：原实现“不支持”。车辆控制应从车辆组件、`ComponentRider`/`ComponentMount`、明确的 Mod Hook 或后续验证过的网络消息层接入，绝不覆盖玩家组件。

## 电路与线程风险

`Circuit/SubsystemCircuit.cs:36` 在 `Load` 中 `Task.Run(ThreadFunction)`，同时主线程更新路径和方块事件会读写静态 `Table`、`Path`、设备连接和地形。代码没有展示世界卸载取消、线程 join 或完整同步边界。`Blocks/Items.cs` 与 `ComponentVariant` 也使用后台任务创建游戏/图形相关对象。

结论：电路拓扑和元件公式可作为功能规格；旧线程模型“不支持”。新实现先使用主线程分帧预算队列；只有纯计算快照可送后台，结果必须回到主线程校验世界版本后提交。联机时由主机/服务端维护权威状态。

## 模块逐项结论

| 模块与原始路径 | 主要依赖 | x25 对应 API | 重写程度 | 依赖的工业模块 | 存档风险 | 状态 |
|---|---|---|---|---|---|---|
| 材料/物品 `Blocks/Items.cs`, `Materials.cs`, `CommonItems.cs` | ItemBlock 动态数组、模型、贴图、翻译 | Block 扫描、CSV、`GetCreativeValues` | 注册和稳定键重写；材料值对象可按规格重建 | 配方、机器、化学 | 数组下标漂移高 | 待验证 |
| 金属/矿物 `MetalBlock.cs`, `OresAndPowder.cs`, `SubsystemMineral.cs` | 地形生成、调色、掉落 | `Terrain`, `SubsystemTerrain`, 生成 Hook | 生成阶段与预算重写 | 材料、机器 | 世界生成与旧 ID 高 | 待验证 |
| 机器方块 `4DirectionalMachines.cs`, `6DirectionalMachines.cs` | 朝向/data、行为子系统 | `TerrainGeometry`, Block placement | 渲染签名和编码适配 | 材料、实体 | 中高 | 待验证 |
| 机器实体 `Components/Component*.cs`, `Subsystems/*Machine*` | XDB、库存、更新、UI | EntitySystem、DatabaseManager | 模板、生命周期、Save/Load 逐项重写 | 配方、电力、化学 | 高 | 待验证 |
| 配方 `*.cr`, `ItemBlock.CRInitialize` | 旧全局委托和动态 ID | `.cr` 合并、目标 recipe API | 删除全局接管，转换为增量配方 | 材料/机器 | 配方 ID 中 | 待验证 |
| 化学 `Chemistry/*.cs`, `ChemicalBlock.cs` | 公式解析、动态数组、反应舱 | 普通 C# + 新物品注册 | 解析核心可重建测试；注册/反应执行重写 | 材料、机器 | 化学数组下标高 | 待验证 |
| 电路 `Circuit/*.cs`, `ElementBlock.cs` | 自建图、后台线程、Jint、UI | IUpdateable、方块行为、网络层 | 核心调度与线程完全重写 | 材料、机器 | 拓扑恢复高 | 待验证 |
| 交通 `Vehicles.cs`, `ComponentBoatI.cs`, `ComponentAirShip.cs`, `ComponentTrain.cs` | 模型、实体、骑乘、玩家输入 | Mount/Rider/Body/Database | 输入、物理和联网重写 | 燃料、机器 | 实体位置/乘员高 | 待验证 |
| 玩家/GUI `ComponentPlayer.cs`, `ComponentNGui.cs` | 原玩家完整实现 | SCNET 网络化玩家组件 | 禁止替换，改为局部扩展 | 所有交互模块 | 玩家数据极高 | 不支持原样移植 |
| 生物变异 `ComponentVariant.cs` | 大量原生 AI 组件、后台任务 | 目标 AI 组件 | 延后，逐类型白名单重写 | 化学 | 生物实体高 | 待验证 |
| 资源 `IndustrialMod/**`, 语言/配方/XDB | ContentManager、旧命名 | `.netmod/Assets` | 名称和引用可重建；内容无授权不得发布 | 全模块 | 资源缺失导致加载失败 | 不支持公开再发布 |

## 资源审计

上游有 17 个 DAE、11 个 PNG、1 个 OGG，另有中英文 CSV/CR/CLO 和两个 XDB。17 个 DAE 均可被 XML 解析；除 `Shield.dae` 外均至少声明一个 `TEXCOORD` 输入。声明 TEXCOORD 只证明模型带 UV 通道，不证明它与实际 PNG 匹配。部分模型不内嵌图像路径，由代码提供贴图；`JellyFish.dae` 还引用了上游包内并不存在的 Blender 风格图像名。所有模型需在获得授权后逐网格、逐材质做渲染和 UV 截图验证。

0.5.1 包含固定上游提交的四种交通载具模型及蒸汽船/火车纹理；蒸汽船、飞艇和火车以 Y_UP 派生副本打包；不含上游音频、旧 DLL 或旧 XDB。

## 0.2 蒸汽船预览审计

固定上游提交中真正的水上载具只有 `SteamBoat`；`Airship` 是飞行器，
`Train`/`Carriage` 是轨道载具。蒸汽船线索分布于：

- `Blocks/Vehicles.cs`：蒸汽船物品方块。
- `Blocks/Items.cs`：物品注册。
- `Subsystems/SubsystemItemBlockBehavior.cs`：从物品生成 `SteamBoat` 实体。
- `Components/ComponentBoatI.cs`：浮力、推进、转向和损坏逻辑。
- `工业mod.xdb`：船体、骑乘点、19 格蒸汽机库存及组件组合。
- `IndustrialMod/Models/SteamBoat.dae` 及对应纹理：按用户声明的明确许可进入交付包。

SCIE 原逻辑以 `ComponentEngine.HeatLevel / 90f` 作为动力来源，并依赖工业燃料、
库存和 UI。0.3.0 没有伪造这些工业模块，而是建立了独立的 x25 移植预览：

- 新物品 ID 999，实体模板 `SCIEPortSteamBoat`。
- 复用 x25 原生 `ComponentBoat`、`ComponentMount`、`ComponentBody`、
  `ComponentDamage` 和 `SubsystemCreatureSpawn`。
- 使用固定提交中的 `Models/SteamBoat` 和 `Textures/SteamBoat`，保留原 `Cylinder` 网格节点。
- 单人 `WorkType.Local` 下增加有限推进，水平速度上限 15；附近 8 格最多生成 4 艘。
- 原生船组件继续负责浮力、转向、基础骑乘和损坏。

0.3.0 根据用户声明的明确授权加入固定提交中的 `SteamBoat.dae` 与 `SteamBoat.png`，
并恢复原模型路径、纹理覆盖、船体尺寸、座位偏移、质量和耐久参数。公开仓库中仍未发现
LICENSE/COPYING，因此公开发布前应把权利人的单独许可文本及修改、再分发范围归档。

仍需重写或补齐：19 格蒸汽机库存、燃料/热量、工业 UI、音效、
存档往返验证，以及服务器权威的驾驶输入同步。当前预览不得描述为完整 SCIE 蒸汽船。

## 0.4 飞艇预览审计

来源路径为 `Blocks/Vehicles.cs`、`Components/ComponentAirShip.cs`、
`Subsystems/SubsystemItemBlockBehavior.cs`、`工业mod.xdb` 与
`IndustrialMod/Models/Airship.dae`。原实现依赖 `ComponentBoatI`、`ComponentEngineA`
和被替换的玩家/GUI 链路，因此只保留功能规格，不直接编译旧源码。

0.4.0 已独立实现物品 ID 998、实体、骑乘、关闭重力、触屏转向、按观察方向推进、
速度上限、浸水损坏和生成数量限制。原模型 140,080 字节，要求 `Airship` 网格节点，
已通过 XML、哈希和包内容检查。航空燃料、10 格工作台、UI、多人同步和实机渲染仍未完成。

0.5.0 Android 实测暴露飞艇竖直且无法骑乘。审计确认 `Airship.dae` 为 Z_UP；旋转为 Y_UP 后
几何范围为 X `-4.628..4.628`、Y `0..11.162`、Z `-15.925..16.054`。旧 `BoxSize=9,7.5,9`
会把玩家挡在驾驶座 2.5 格搜索范围外。0.5.1 把物理体缩到驾驶舱 `3,2,6`，座位调整为
`0,1,-0.8`，生成高度降至地表上 0.1 格。文件、ABI 与构建已确认，Android 骑乘仍待复测。

## 0.5 轨道交通预览审计

固定上游的 `Blocks/RailBlock.cs`、`Subsystems/SubsystemRailBlockBehavior.cs`、
`Components/ComponentTrain.cs` 和 `Subsystems/SubsystemItemBlockBehavior.cs` 提供轨道 data 编码、
邻接成形、沿轨导向与实体生成线索。目标版重新实现 ID 997 铁轨、ID 996 火车和 ID 995 车厢，
并引用获授权的 `Train.dae`、`Minecart.dae`、`Train.png`。两模型都包含 UV0；要求的网格节点
`Cylinder` 和 `obj1` 已由打包脚本检查。

可复用的是十种轨道形状的低四位编码和转弯规则。必须重写的是几何 override、方块交互、
UpdateOrder、玩家输入以及实体创建。旧蒸汽机热量、车厢父体延迟复制、货箱 GUI 和多人同步没有直接迁移；
0.5.0 的车厢是可独立试乘的验证实体。Release 编译和资源检查为已确认，游戏内轨道运行仍为待验证。

## 当前可复用与必须重写

已确认可以作为“规格/算法线索”复用，但在获得 SCIE 授权前不能复制表达形式：

- 材料、化学式、机器流程和配方之间的领域关系。
- `DatabaseManager.CreateEntity` 驱动的“物品生成实体”总体模式。
- contents/light/data 的基础位布局，以及 501–534 可作为旧世界识别范围。
- 纯数据解析与化学计量思路，前提是独立重写并用单元测试验证。

必须重写：PluginLoader 和全局委托劫持、ItemBlock 动态数组注册、原版 ID 覆盖、XDB 固定 GUID 替换、`ComponentNPlayer`/`ComponentNGui`、电路后台线程、所有旧渲染签名、交互签名、UpdateOrder、交通工具输入/物理/同步、资源装载和稳定存档标识。
