# SCIE 旧 API 与 SCNET x25.01.17 差异

## 0.7.0 静态模型家具接口

六件工业家具未复用旧 `ItemBlock.Items` 动态数组，而是对真实 x25.01.17 程序集编译绑定：
`Block.Initialize`、`ContentManager.Get<Model>`、`Model.FindMesh`、`BlockMesh.AppendModelMeshPart`、
`BlockMesh.CalculateBoundingBox`、`TerrainGeometry.GetGeometry(Texture2D)`、
`BlockGeometryGenerator.GenerateShadedMeshVertices`、`Block.GetPlacementValue`、
`Terrain.MakeBlockValue` 与 `Block.GetDropValues`。Release 0 warning/0 error 只确认这些签名可用；
模型渲染、碰撞和存档仍需 Windows/Android 运行验证。

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

XDB 合并还有一项已由目标 `ModsManager.CombineDataBase` 反编译确认的格式约束：该例程读取 Mod xdb
后，遍历其根节点的**直接子元素**（`Folder` / `ProjectTemplate`），逐个合并进原版 `<DatabaseObjects>`
树。这一步与根元素叫什么名字无关——用自定义容器根（`<SCIEPortWatercraft>`）或以 `<DatabaseObjects>`
为根都能被正确处理（正常运行的 Builder Assistant `Speedboat.xdb` 就是以 `<DatabaseObjects>` 为根）。
因此“根节点导致嵌套 `DatabaseObjects` 才反序列化失败”是早期误诊，已推翻。真正、且已由反编译确认的
约束是：**代表某个原版文件夹的 `<Folder>` 必须复用游戏的原版文件夹 GUID**，因为合并按
`Name + Guid` 匹配；GUID 不一致时合并不会并入原版文件夹，而是追加一个同名但不同 GUID 的游离
`<Folder>`，使内容落到非标准容器（这正是 0.3.1 的问题，现由 `scripts/Test-Database.ps1` 拦截）。

## 飞艇接口差异

旧 `ComponentAirship` 继承 `ComponentBoatI`，依赖旧 `ComponentEngineA`、全局 `Utils`、
隐藏式 `new Update` 和旧版 `Quaternion.ToForwardVector()` 扩展。x25 中不能原样复用。
0.4.0 改为独立 `ComponentAirshipDrive : Component, IUpdateable`：通过目标
`ComponentMount.Rider` 确认驾驶者，直接读取 `ComponentInput.PlayerInput.Move`，以
`Matrix.CreateFromQuaternion(EyeRotation).Forward` 计算升降方向，并使用目标
`ComponentBody.IsGravityEnabled`、`IsGroundDragEnabled`、`Velocity` 和 `Rotation`。
自定义推进仅在 `WorkType.Local` 执行，多人权威仍为不支持。

目标 `ComponentRider.FindNearestMount()` 只搜索身体中心周围 2.5 格，并要求玩家眼睛到
`ComponentMount.MountOffset` 的距离小于 2.5 格。0.5.0 飞艇沿用了旧 `BoxSize=9,7.5,9`，
玩家会先被碰撞体挡住，因此座位不可达。0.5.1 将物理体限定到驾驶舱 `3,2,6`，并使用
可达座位 `0,1,-0.8`；这是按实际反编译逻辑修复，不是扩大或替换原生骑乘协议。

这些结论只覆盖程序集表面与编译。`SCIEPortSteamBoat` XDB 模板能否在游戏中
实例化、SCIE `Models/SteamBoat` 与纹理是否按预期装载，以及触屏骑乘输入的实际行为仍为待验证。

## 铁轨和轨道载具接口差异

旧 `RailBlock` 的几何签名使用已不存在的 `TerrainGeometrySubsets`；0.5.0 已改为目标
`TerrainGeometry` 和 `SubsetAlphaTest`。旧眼睛方向扩展改为
`Matrix.CreateFromQuaternion(EyeRotation).Forward`，`SubsystemTerrain.ChangeCell` 改用目标五参数签名。
旧 `ComponentTrain` 的 `int UpdateOrder` 改为目标 `Game.UpdateOrder`，实体创建统一使用已反射确认的
`SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)`。

旧火车推进依赖尚未迁移的 `ComponentEngine.HeatLevel`，车厢编组依赖延迟复制父实体位置、
`EntityReference` 存档和旧 GUI/库存体系。这些部分没有直接复制：0.5.0 先实现单人本地触屏推进、
轨道居中、转弯和坡轨姿态，车厢暂作可独立试乘实体。编组、燃料和多人权威状态为不支持，
实际游戏加载与轨道物理为待验证。

## 0.8.1 陆地 ATV 接口取证

ATV 没有复用 `ComponentBoat`：该组件的推进、浮力和水面转向语义不适用于陆地。
也没有复用 `ComponentRailVehicle`：它每帧强制把实体吸附到铁路中心线，会破坏自由地面碰撞。

对固定 Windows `Net_x25.01.17` 程序集反编译并由 net472 编译绑定确认：

| 接口 | 实际用途 | 结论 |
|---|---|---|
| `ComponentBody.Velocity`、`Rotation`、`IsGravityEnabled`、`IsGroundDragEnabled` | 只修改水平驱动速度，保留原版重力与碰撞 | 已确认 |
| `ComponentBody.StandingOnValue` / `StandingOnBody` | 仅在原生碰撞已判定接地后施加驱动力 | 已确认 |
| `ComponentBody.IsSmoothRiseEnabled` / `MaxSmoothRiseHeight` | 由原生 `HandleSmoothRise` 执行台阶、坡面和碰撞；ATV 限制为 `0.35` 格 | 已确认 |
| `ComponentMount.Rider` / `ComponentRider.StartDismounting` | 单座上车、下车及落水强制下车 | 已确认 |
| `ComponentRider` 的父 `ComponentBody` 关系 | 原生骑乘将骑手身体挂到载具身体，故沿用原生第三人称镜头与保存链 | 已确认；实际镜头待验证 |
| `SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)` | 物品只生成独立 ATV 实体，不写入或替换地形 | 已确认 |

`ComponentBody` 在目标程序集内会查询地形和其它 body 的碰撞盒、按轴碰撞移动，并把
`MaxSmoothRiseHeight` 作为原生光滑上台阶的上界。因此本模块不自行穿透地形、不直接改写
`Position`，也不复制任何船只水面物理。上述是程序集和静态编译证据；在 Android 7723 的
实际坡度、碰撞、下车位置、第三人称和存档恢复仍为待验证。

## 0.8.2 ATV 阻力与一格越障取证

目标 `ComponentBody.Update` 在自定义组件更新之后仍会执行空气阻力，并在
`IsGroundDragEnabled && StandingOnValue.HasValue` 时额外执行
`Velocity = Vector3.Lerp(Velocity, StandingOnVelocity, 6 * dt)`。ATV v0.1 在此前还应用自己的
纵向滑行和横向抓地，构成双重地面阻力；v0.2 在有驾驶者时关闭原生地面阻力，由
`ComponentAtvDrive` 统一计算制动、滑行和抓地，无驾驶者时立即恢复原生地面阻力。

真实 `MoveWithCollision` 每个物理子步依次执行 `HandleSmoothRise`、Y/X/Z 三轴碰撞；
`HandleSmoothRise` 的垂直上升速度限制为 `min(4.5 * dt, overlap)`。水平碰撞使用抬高
`MaxSmoothRiseHeight` 后的完整碰撞盒判断地形，但其它 `ComponentBody` 仍使用未抬高的正常碰撞盒；
因此 `1.05` 能覆盖完整一格及浮点容差，同时不会把实体当台阶。碰撞盒上界保持不变，顶部障碍仍会
阻止抬升。v0.2 没有调用 `ApplyDirectMove`、没有写 `Position`、没有改地形，也没有瞬移越障。

`StandingOnValue` 表示地形支撑，`StandingOnBody` 表示实体支撑。v0.2 只有前者且后者为空时才施加
驱动力；两者都为空时保留水平惯性，由原生重力和空气阻力处理。这样短暂离地不再触发 v0.1 的
`7 * dt` 强制水平刹停，也不会借站在其它实体上获得越障推进。
## 0.8.1 坦克地面载具接口审计

坦克没有移植 SCIE 的船舶浮力、`ComponentBoat.MoveOrder`/`TurnOrder`，也没有采用 Rail
模块的强制轨道居中或姿态覆盖。对固定目标程序集 `Net_x25.01.17`（本文件开头的三份
SHA-256）实际反射，并以 net472 Release 绑定验证的接口如下：

| 目标接口 | 实际用途 | 状态 |
|---|---|---|
| `SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)` | 放置物品生成 `SCIEPortT34Tank` 实体 | 已确认 |
| `ComponentMount.Rider` / `ComponentRider.Mount` | 识别唯一原生驾驶者与原生骑乘关系 | 已确认 |
| `ComponentRider.StartDismounting()` | 深度浸水时调用原生下车流程 | 已确认 |
| `ComponentPlayer.ComponentInput` → `ComponentInput.PlayerInput.Move` | 读取 Android 触屏映射后的前后/左右输入 | 已确认 |
| `ComponentBody.Velocity`、`Rotation`、`Matrix` | 只施加水平驱动与车体转向，不直接写位置 | 已确认 |
| `ComponentBody.IsGravityEnabled`、`IsGroundDragEnabled` | 交给原生物理保持重力、阻尼和地形/实体碰撞 | 已确认 |
| `ComponentBody.IsSmoothRiseEnabled`、`MaxSmoothRiseHeight`、`StandingOnValue`、`StandingOnBody` | v0.3 使用 1.05 格原生连续平滑抬升并保留碰撞/顶部净空检查；离地时不施加推力 | 已确认（ABI/编译），Android 一格越障待验证 |
| `PlayerInput.Hit`、`SubsystemProjectiles.FireProjectile`、`ProjectileRemoved`、`SubsystemExplosions.AddExplosion` | 单人坦克主炮使用触屏攻击射线、原版弹体和命中后有限压力爆炸 | 已确认（ABI/编译），Android 待验证 |
| `ComponentModel.SetBoneTransform`、`Model.FindBone` | 分别控制 `TankTurret` 水平旋转和 `TankGun` 俯仰；不旋转车体 | 已确认（ABI/模型导入），Android 待验证 |
| `ComponentBody.ImmersionFactor` | 断开水面驾驶，深度浸水时下车 | 已确认 |
| `ComponentCreatureModel.EyeRotation` | 以玩家视线初始方向放置车体 | 已确认 |
| `ComponentModel.TextureOverride` | 引用包内 PNG，避免世界实体无纹理 | 已确认 |

`ComponentBody` 是目标程序集自己的碰撞/地形解算器；本阶段不直接更新 `Position`，因此
没有通过穿墙移动、船只水面推进或 Rail 轨道对齐来伪造地面驾驶。原生碰撞、实际坡面、
第三人称镜头和 Android 触控操作均仍须游戏实测，不能由上述 ABI 证据推出。

模板 `SCIEPortT34Tank` 使用原版 `SimpleModel`、`Spawn`、`Mount`、`Body`、`Damage` 父模板，
并以新的 `SCIEPortTankDrive` 组件只保存 `TurretYaw`、`GunPitch`。炮塔/炮管目前只是独立
模型节点和独立持久化状态，尚没有输入绑定或逐节点动画；这避免在第一阶段引入未经验证的
模型骨骼、网络或武器接口。
