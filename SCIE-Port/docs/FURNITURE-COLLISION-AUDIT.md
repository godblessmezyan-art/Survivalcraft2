# SCIE-Port 模型显示尺寸与实际碰撞体一致性审计

目标环境：SCNET `Net_x25.01.17`，Android 7723 客户端。
本次专项范围：仅修复/审计「模型可见表面」与「玩家实际站立、行走的物理表面」不一致（悬空、无法靠近、穿模）的问题。不改模型外观，不改全局玩家移动参数，不触碰 Builder Assistant 移动堡垒相关文件。

## 1. 问题根因清单

1. 工业家具方块（`IndustrialFurnitureBlocks.cs`，ID 917-922）此前用整模型外接盒作为碰撞体：
   `m_collisionBoxes[rotation] = new[] { target.CalculateBoundingBox() }`。
   单个 `CalculateBoundingBox()` 返回的是整块网格的最小/最大坐标包围盒，会把齿轮孔、模具凹槽、支腿之间的空隙一并填满，等价于一整格实心砖。视觉上有洞、能穿过去的地方，物理上却是实心，于是产生「贴不到、被空气墙挡住、踩上去像整格」的悬空/穿模体感。这是本次的核心缺陷。

2. 对引擎碰撞取用的错误假设（早期怀疑）：曾以为引擎只有在 `Block.HasCollisionBehavior == true` 时才会查询 `GetCustomCollisionBoxes`，否则会退回整格立方体。反编译目标程序集后证伪（见第 2 节），因此本次并未新增该开关，而是移除了这行投机代码。

3. 比例与资产异常（背景信息，未改）：`IndustrialCylinderFurnitureBlock`（钢瓶，ID 922）使用 `ModelScale = 55f`，其 `Cylinder.dae` 并不在 `Assets/Models/`，而是 `Build-NetMod.ps1` 第 226-227 行从 gitignore 的 `upstream/SCIE/IndustrialMod/Models/` 授权注入打包。这是既有事实：源树里没有该文件属正常，最终 `.netmod` 会带上它。本次不触碰其外观/比例，只让碰撞盒同样由几何派生。

4. 朝向一致性（已具备，本次保持并验证）：`Initialize()` 针对 4 个朝向分别烘焙 `m_worldMeshes[rotation]` 与 `m_collisionBoxes[rotation]`，`GetCustomCollisionBoxes` 依据 `Terrain.ExtractData(value) & 3` 选取对应朝向的盒集合。碰撞盒随模型一起旋转，无需额外旋转逻辑。

5. 车辆碰撞体（审计，未改）：车辆不走 `GetCustomCollisionBoxes`，而是单个 `ComponentBody.BoxSize`（`.xdb` 作者数据）。其碰撞是「按数据声明的实心盒」，与网格无关，属不同子系统。本次仅新增只读诊断，不改任何物理数值（见第 5 节）。

## 2. 引擎事实（来自目标程序集反编译，非猜测）

用 Mono.Cecil 读取 `D:\SCNET\Survivalcraft.exe` / `Engine.dll`（版本 2.4.0.0）得到：

- `Game.Block.GetCustomCollisionBoxes(SubsystemTerrain, int)` 的基实现是 `return m_defaultCollisionBoxes;`（一个静态整格盒数组）。即：不重写就直接用整格；重写就用重写的盒。
- 站立/行走/撞墙的地形碰撞由 `Game.ComponentBody.FindTerrainCollisionBoxes` 计算，其门控字段是 `Block.IsCollidable`（家具 CSV 里为 `TRUE`），随后**直接** `callvirt GetCustomCollisionBoxes(terrain, cellValue)`，并把返回的每个局部盒叠加到该格原点（世界坐标 = `Vector3(floor(cellX), floor(cellY), floor(cellZ)) + 局部盒`）。路径寻量高度 `SubsystemPathfinding/World::GetBlockWalkingHeight` 同样直接调用该方法。
- `Game.Block::HasCollisionBehavior_()`（带下划线的方法）在整个程序集里**零调用者**，是死代码。
- `Block::HasCollisionBehavior`（字段）真正的唯一读取点在 `ComponentBody::HandleAxisCollision`：它为真时只是多走一遍 `SubsystemBlockBehaviors.GetBlockBehaviors(...).OnCollide(...)` 回调，且该分支**落空后仍继续**执行原有的站位/推回/`StandingOnValue` 设定（IL 循环后无跳转，直落 `IL_0322`）。也就是说开启它不会破坏站立逻辑，只会为「注册了 BlockBehavior 的方块」额外派发 `OnCollide`。
- 结论：这些家具方块没有注册任何 `BlockBehavior`（其 CSV 无 `Behaviors` 列），开启 `HasCollisionBehavior` 只会带来一次空查找。故悬空的真因是「整模型外接盒」而非「开关没开」。修复靠几何派生盒，`HasCollisionBehavior` 保持默认 `false`。

局部坐标约定：`GetCustomCollisionBoxes` 返回的盒是「格内 [0,1] 局部坐标」，原点为该格最小角，+Y 向上一个方块。这与 `RailBlock`（0.25/0.5/0.75 台阶盒）、`KellyPlantBlocks`（如果实 `(0.33,0,0.33)-(0.67,0.42,0.67)`）的既有写法一致，本次沿用它，故低于一格的真实高度确实会影响踩踏，且是逐级可站立的。

## 3. 实际修复

新增纯逻辑几何内核（无 Engine 依赖，可离线单测）：

- `FurnitureCollisionModel.cs`：`BuildBoxes(positions, triangles, gridX, gridY, gridZ[, out topY])`。
  1. `MarkOccupancy`：逐三角形求 AABB，按 [0,1] 体素网格打点占用（退化轴保护、`HasVolume` 阈值 `1e-4`）。
  2. `FloodFillEnclosedCavities`：从 6 个边界面的空格做 BFS。能从边界连通到的空格=真实开口（环孔、模具腔、腿间空隙），保持可通行；被几何包围、连不到边界的空格=实心内部，置为占用。这一步修正了「三角面只打外壳、把实心盒掏成空心壳」的错误，是单格外接盒永远做错的关键区别。
  3. `MergeOccupancy`：贪心 3D 行程合并（先 +X，再整 X 段升 +Y，再整 X×Y 面升 +Z），输出更少、更大的盒；按 `(MinY, MinZ, MinX)` 确定性排序，钳制到 [0,1]。
  4. `RotateVertices90`（`x'=1-z, z'=x`）/`RotateBox90`：离线复现引擎 90° 朝向，用于验证碰撞随模型旋转。
  - 两参数重载与 `out topY` 重载都先做 `grid<1 → ArgumentOutOfRangeException` 校验。
- `FurnitureCollisionBoxes.cs`（`internal`，Engine 适配）：`Build(BlockMesh, out topY)` 把 `mesh.Vertices[].Position` 摊平为 `float[]`、`mesh.Indices` 摊平为 `int[]`，调用内核，`CollisionBox → Engine.BoundingBox`（带 `max>=min` 保护），网格不可用时回退 `ClampToCell(CalculateBoundingBox())`。
- `FurnitureCollisionOptions.cs`：`Diagnostics` 开关（默认 OFF）、`GridX=8 / GridY=20 / GridZ=8`（0.125×0.05×0.125 格），`HeightQuantum = 1/GridY = 0.05`。
- `IndustrialFurnitureBlocks.cs`：`Initialize()` 内逐朝向改为 `FurnitureCollisionBoxes.Build(target, out m_collisionTopY[rotation])`；删除投机 `HasCollisionBehavior = true`，代之以反编译依据的注释；新增 `DumpCollisionDiagnostics(value,x,y,z)` 记录每盒绝对世界包围（`worldMin=(x+Min.X, y+Min.Y, z+Min.Z)` 等）与高度。

诊断接线：`SCIEPortLoader.Save/LoadSettings` 现持久化 `SCIEPortFurnitureCollisionDiagnostics` 与 `SCIEPortVehicleAuditDiagnostics` 两个开关（此前 `FurnitureCollisionOptions.Diagnostics` 无人接线，真机上永远为 OFF）。

## 4. 模型尺寸 vs 碰撞尺寸对照

原始 `.dae` 顶点 Y 跨度（本次用 node 解析 `float_array` 实测，未含骨骼变换，仅供量级参考）：

| 方块 | ID | ModelScale | 原始 Y 跨度 | 缩放后大致顶高 | 旧碰撞（外接盒） | 新碰撞（几何派生） |
|---|---|---|---|---|---|---|
| SteelGear | 917 | 0.9 | ~[-0.98, 1.00] | ~0.90 | 单整盒，孔被填死 | 按三角面+腔体判定，孔可通行，顶高≤1 |
| SteelWheel | 918 | 0.9 | ~[-0.90, 1.00] | ~0.90 | 同上 | 同上 |
| GearMould | 919 | 0.9 | ~[-1.00, 1.00] | ~0.90 | 单整盒，凹槽被填死 | 凹槽连边界则保持可通行 |
| WheelMould | 920 | 0.9 | ~[-1.00, 1.00] | ~0.90 | 同上 | 同上 |
| IndustrialPiston | 921 | 0.75 | ~[-0.92, 0.98] | ~0.73 | 单整盒 | 按实际轮廓，偏移 (0.55,0,0.55) 一并烘焙 |
| IndustrialCylinder | 922 | 55 | 资产在 upstream（授权注入） | 需真机确认 | 单整盒 | 由注入后的网格几何派生 |

说明：这些模型原始 Y 跨度接近一整格（多为轮/齿轮盘类立放物），故对它们而言修复重点在「轮廓与腔体的正确性、杜绝整盒填死、顶高不超一格」，而非把高度缩到 0.35。低于一格的短装饰（用户描述的 0.35/0.5 类）通过同一几何体素化自然落到真实顶高。权威顶高由真机 `DumpCollisionDiagnostics` 逐格打印 `worldMax.Y` 得到，量化误差 ≤ `HeightQuantum = 0.05` 格。

## 5. 车辆 ComponentBody 审计（未改物理）

车辆碰撞 = 单个 `ComponentBody.BoxSize`（`.xdb` 声明），与网格无关：

| 载具 | BoxSize (x,y,z 格) | Y 高 | MaxSmoothRiseHeight |
|---|---|---|---|
| ATV | 1.1, 1.25, 2.6 | 1.25 | 1.05 |
| 越野车 | 2.4, 1.55, 4 | 1.55 | 1.05 |
| 坦克 | 3.6, 2.1, 5.9 | 2.1 | 1.05 |
| 直升机 | 2.4, 2.2, 5.4 | 2.2 | 0.3 |
| 汽艇 | 2.3, 1.75, 5.6 | 1.75 | 0.3 |
| 飞艇 | 3, 2, 6 | 2.0 | 0.3 |

观察：BoxSize 普遍高于可见甲板/可行走面（例如直升机 Y=2.2 含旋翼体积），意味着站在车旁的玩家可能撞到「看不见的高盒」；但这是按数据声明的整车碰撞，属独立设计问题，贸然改 `BoxSize` 会影响乘骑、上下车、碰撞与联机同步，风险高，故本次不改数值，只新增只读诊断（见下）。上下车站位由 `ComponentTwoSeatManager` 的 `ParentBodyPositionOffset` 决定，`ComponentBody.BoundingBox` 给出世界 AABB，均在原生组件内，本次未触碰。

只读诊断：新增 `VehicleCollisionAudit.cs` + `VehicleAuditOptions.cs`，在 7 个驾驶组件（ATV/越野/坦克/直升机/汽艇/飞艇/轨道车）`Load()` 末尾调用 `LogOnce`，当 `VehicleAuditOptions.Diagnostics` 为 ON 时打印「作者 BoxSize + 世界 AABB」对比「模型 `ModelMesh.BoundingBox`」。该方法不写任何物理量，默认静默。

## 6. NoSmoothRise / NoAutoJump 与低于一格台阶

`Game.Block` 公开字段 `NoSmoothRise`、`NoAutoJump`、`HasCollisionBehavior` 均可用（反编译确认）。语义（待真机细化）：`SmoothRise` 是引擎把低于阈值的台阶「平滑抬升」而非判为墙的路径；`MaxSmoothRiseHeight` 为抬升阈值（车辆侧 1.05 / 0.3）。对低于一格的家具顶面，只要 `GetCustomCollisionBoxes` 返回真实的矮盒（本次几何派生即产出矮盒），玩家理论上可平滑走上其顶面，而不是被整格隐墙挡住。家具的 `NoSmoothRise`/`NoAutoJump` 在精简版 CSV（21 列）中不存在，取引擎默认值；本次未改这些默认，是否在特定矮方块上显式设 `NoSmoothRise=false` 以强制台阶抬升，列入第 8 节待验证。

## 7. 自动测试

`tests/SCIEPort.FurnitureCollision.Tests`（net8，`TreatWarningsAsErrors`，仅链接纯 `FurnitureCollisionModel.cs`），已注册进 `Build-NetMod.ps1`。8 个用例 40 断言全绿，覆盖验收高度 0.25/0.35/0.5/0.75/1.0（`TestAcceptanceHeights` 断言派生盒顶面在 `HeightQuantum` 容差内匹配可见顶、且不超过一格）、超限不越一格、框形腔体保持可通行、实心立方填满整格、空网格产出零盒、体素化随模型旋转、非法网格抛异常。整包 `Build-NetMod.ps1` EXIT=0，14 个离线套件全通过，`DATABASE VALIDATION PASSED`，产物 SHA-256 `A80A5043...71968`。

## 8. Android 7723 真机实测步骤

1. 先在新建单人可丢弃世界里验证，勿用主存档。
2. 在 mod 设置里开启 `SCIEPortFurnitureCollisionDiagnostics`（及需要时 `SCIEPortVehicleAuditDiagnostics`），进世界。
3. 依次放置 917-922 六个家具，绕每格四个朝向各放一次。
4. 用手指走到方块旁：确认「能贴到模型可见边缘」不再有空气墙；尝试站上去：脚应落在可见顶面高度，而非悬在整格顶。
5. 对齿轮/模具：确认孔洞/凹槽处可穿过或贴合，不被隐形势墙填满。
6. 重新进世界、旋转后再次进出，复测同一格踩踏高度是否稳定一致。
7. 用日志里 `DumpCollisionDiagnostics`（或初始化行）打印的 `worldMax.Y` 对照肉眼可见顶面，误差应 ≤ 0.05 格。
8. 联机：房主(Host)与普通客户端分别重复 3-6，确认踩踏/穿模一致；客户端不应写世界。

## 9. 尚未解决的限制

- 本环境仅能编译与打包，无法自动运行游戏。站立/行走/跨越/旋转/重进的真实手感、流体交互、越障帧耗时、联机同步、Android 渲染，均须第 8 节真机确认后才能标为「已确认」；在此之前整体状态为「待验证」。
- 碰撞盒由「三角面 AABB 体素化 + 腔体填充 + 贪心合并」派生，高度量化到 `HeightQuantum=0.05` 格，是对真实网格的近似，不是逐三角形精确碰撞。
- `DumpCollisionDiagnostics` 目前是可被调用的公共诊断 API，但未自动挂到每次放置（家具无 BlockBehavior，挂它需新建子系统，超出本次最小改动范围）；主要自动证据为 `Initialize` 期日志，逐格世界包围需外部触发或后续接入放置回调。
- 车辆 `BoxSize` 高于可见面属数据层设计，本次只诊断未整改；若要贴合，应作为独立的车辆碰撞专项，连带评估乘骑/上下车/联机同步。
- 第 6 节 `NoSmoothRise`/`NoAutoJump` 对矮方块的确切影响未经真机量化，暂不下「已确认」结论。
