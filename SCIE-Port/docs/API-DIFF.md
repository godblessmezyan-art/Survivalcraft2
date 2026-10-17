# SCIE 旧 API 与 SCNET x25.01.17 差异

## 取证目标

以下结论来自 `E:\SCNET\SCNETx25.01.17` 的实际程序集：

| 文件 | SHA-256 |
|---|---|
| `Survivalcraft.exe` | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` |
| `Engine.dll` | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` |
| `EntitySystem.dll` | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` |

程序集版本为 `2.4.0.0`，TargetFramework 为 `.NETFramework,Version=v4.7.2`；二进制包含 API `1.42` 和 SC `2.4.10.6` 标记。

## 对照表

| 关注点 | SCIE 旧源码 | x25.01.17 实际表面 | 移植判断 |
|---|---|---|---|
| Mod 入口 | `[PluginLoader(...)]` 静态类 | `class : Game.ModLoader`; `public virtual void __ModInitialize()`；程序集扫描自动实例化 | 必须重写 |
| Hook 注册 | 修改 Manager 的委托字段 | 全局 `ModsManager.RegisterHook(string, ModLoader)` + `ModLoader` 虚方法 | 使用明确 Hook；不得劫持管理器 |
| 方块发现 | 依赖旧插件 API/修改后的管理器 | ModEntity 扫描 Block 子类公开静态 `int Index` | 可直接使用 |
| CSV | 根 `Items.csv` | `ModEntity.LoadBlocksData()` 调 `BlocksManager.LoadBlocksData(string)`，包中所有 `.csv` 被处理 | 格式可迁移，字段逐项验证 |
| 方块容量 | 旧 ID 假设 | `Block[1024]`，contents 10 位 | 需 ID 注册表与冲突检查 |
| 几何参数 | `TerrainGeometrySubsets` | `TerrainGeometry`，有 `SubsetOpaque`、`OpaqueSubsetsByFace` 等 | 所有 override 必须改签名 |
| 临时配方 | `GetAdHocCraftingRecipe(terrain, ingredients, heat)` | `GetAdHocCraftingRecipe(terrain, ingredients, heat, ComponentPlayer)` | 增加玩家参数并重测权限 |
| 配方全局初始化 | `Initialize1` 加上整套自定义 Decode/Match/Transform 委托 | 仍可见 `Initialize1`，但其他 SCIE 依赖字段不构成可靠契约；SCNET 提供 recipe Hook | 不使用遗留委托，改为增量合并/Hook |
| IUpdateable | 多处 `int UpdateOrder => 0` | `Game.UpdateOrder UpdateOrder { get; }` | 必须逐组件修改并确定顺序 |
| OnUse | `OnUse(Vector3, Vector3, ComponentMiner)` | `bool OnUse(Ray3, ComponentMiner)` | 必须重写 |
| OnAim | `OnAim(Vector3, Vector3, ComponentMiner, AimState)` | `bool OnAim(Ray3, ComponentMiner, AimState)` | 必须重写并返回处理结果 |
| 数据库实体 | `DatabaseManager.CreateEntity(Project,string,bool)` / overrides | 两个对应重载仍存在 | 调用形态可用，模板必须新建 |
| XDB 合并 | 旧 SCPluginAPI 固定 GUID 修改 | `ModEntity.LoadXdb` → `ModsManager.CombineDataBase`；支持 GUID 与 `new-*` | 机制存在，旧 GUID 未验证，不得直用 |
| 玩家 | `ComponentNPlayer` 复制 Update 并替换类 | 目标玩家含 `PlayerGuid`、网络 aim/dig/interact 事件、触控逻辑 | 禁止替换 |
| 命名 | SCIE 自定义 `Game.Resistor`, `Game.BulletBlock`, `Game.BasaltBlock` 等 | 目标已有同名类型 | 改到 `SCIEPort.*` 命名空间并避免同名扫描歧义 |

## 编译探针证据

`audit/SCIE.Legacy.TargetProbe.csproj` 不复制源码，只把忽略提交的固定上游树作为 Compile 输入并引用实际目标程序集。2026-10-17 的第一次有效探针产生 56 个目标兼容错误（去除探针自身自动 AssemblyInfo 重复项后），首批阻断包括：

- `PluginLoaderAttribute` 不存在。
- `TerrainGeometrySubsets` 不存在，且 `RailBlock` 未实现新的抽象几何方法。
- 多个 `IUpdateable.UpdateOrder` 返回类型错误。
- `OnUse` / `OnAim` 和 ad-hoc recipe override 不再匹配。
- `Game.Resistor` 与目标类型冲突，partial 基类也不一致。
- `BasaltBlock` 等与目标程序集同名。

这只是编译器在当前阶段暴露的第一层错误；修复后还会出现更多成员、行为与资源差异，因此“56 个”不是完整工作量估算。

## 已确认的最小路径

当前 clean-room 工程已实际编译以下路径：

1. `SCIEPortLoader : ModLoader` 与 `__ModInitialize()`。
2. `PortingTestAlloyBlock : CubeBlock`，公开常量 `Index = 1000`。
3. `Assets/Blocks/SCIEPort.csv` 通过目标字段设置创造栏数据。
4. net472 引用实际 `Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll`，所有引用 `Private=false`。

“编译已确认”不等于“游戏加载已确认”。运行状态见 `TEST-RESULTS.md`。

## 蒸汽船预览所用 x25 接口

下列接口已经由 `Test-Environment.ps1` 对固定程序集进行反射检查，并由
SCIE-Port 0.3.0 Release 构建验证调用签名：

| 接口 | x25.01.17 实际形态 | 当前用途 | 状态 |
|---|---|---|---|
| `SubsystemCreatureSpawn.SpawnCreature` | `Entity SpawnCreature(string, Vector3, bool)` | 由物品生成独立蒸汽船实体 | 已确认 |
| `ComponentMount.Rider` | `ComponentRider` 属性 | 判断载具是否已有驾驶者 | 已确认 |
| `ComponentRider.Mount` | `ComponentMount` 属性 | 从玩家输入确认实际骑乘关系 | 已确认 |
| `ComponentBoat.MoveOrder` | `float` 属性 | 复用原生船推进输入 | 已确认 |
| `ComponentBoat.TurnOrder` | `float` 属性 | 复用原生船转向输入 | 已确认 |
| `ComponentBody` | `Position`、`Velocity`、`Density`、`IsSneaking` | 生成检查、限速和下船条件 | 已确认 |
| `CommonLib.WorkType` | 与 `WorkType.Local` 比较 | 将额外推进限制在本地单人工作模式 | 已确认 |

XDB 合并还有一项已由目标 `ModsManager.CombineDataBase` 反编译确认的格式约束：Mod XDB
必须使用自定义容器根节点（例如 `<SCIEPortWatercraft>`），其直接子节点才是要加入原版
`DatabaseObjects` 的 `Folder` / `ProjectTemplate`。若 Mod 文件自身以 `<DatabaseObjects>` 为根，
会产生嵌套同名节点并使数据库反序列化失败。

这些结论只覆盖程序集表面与编译。`SCIEPortSteamBoat` XDB 模板能否在游戏中
实例化、SCIE `Models/SteamBoat` 与纹理是否按预期装载，以及触屏骑乘输入的实际行为仍为待验证。
