# 双人直升机 V0.1 源码审计与技术方案

日期：2026-10-25
目标：Android Survivalcraft 2.4 NET `Net_x25.01.17`
阶段：只完成源码审计和技术方案；尚未开始直升机实现

## 1. 状态边界

本文件只使用项目规定的三种兼容性状态：`已确认`、`待验证`、`不支持`。

- `已确认`：固定 x25.01.17 Windows 程序集哈希、反射/反编译证据、当前源码或离线检查能够证明。
- `待验证`：源码可编译或已有同类机制，但尚未在目标 Android 7723 客户端或真实双机环境运行。
- `不支持`：目标原生机制不能直接表达，且 V0.1 不计划替换该机制。

本阶段没有创建直升机方块、实体、GUID、模型、贴图或网络包，也没有修改世界存档。

## 2. 审计基线

`scripts/Test-Environment.ps1 -GameDir D:\SC` 已通过 Windows PowerShell 5.1：

| 项目 | 结果 | 状态 |
|---|---|---|
| `Survivalcraft.exe` SHA-256 | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` | 已确认 |
| `Engine.dll` SHA-256 | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` | 已确认 |
| `EntitySystem.dll` SHA-256 | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` | 已确认 |
| 游戏程序集版本 | `2.4.0.0` | 已确认 |
| SCNET/API 标记 | `2.4.10.6` / `1.42` | 已确认 |
| Target Framework | `.NET Framework 4.7.2` | 已确认 |
| 原生方块常量 | 260 个；最大 ID 263 | 已确认 |

上述证据只对应固定 Windows 发布程序集。7723 Android 重打包客户端的程序集哈希仍为`待验证`，不能自动继承 Windows 结论。

## 3. 现有载具注册与实体创建

### 3.1 物品注册

当前载具均采用“Mod DLL 内 `Block` 子类 + CSV 数据行”的既有路径：

| 载具 | 方块类 / ID | CSV |
|---|---|---|
| ATV | `Features/LandVehicles/AtvBlock.cs`，923 | `Assets/Blocks/LandVehicles.csv` |
| 坦克 | `Features/Tanks/TankBlock.cs`，924 | `Assets/Blocks/Tanks.csv` |
| 越野车 | `Features/OffroadVehicles/OffroadCarBlock.cs`，925 | `Assets/Blocks/OffroadVehicles.csv` |
| 飞艇 | `Features/Watercraft/AirshipBlock.cs`，998 | `Assets/Blocks/Watercraft.csv` |
| 蒸汽船 | `Features/Watercraft/SteamBoatBlock.cs`，999 | `Assets/Blocks/Watercraft.csv` |

`SCIEPort.csproj` 的 `Assets\**\*` 会复制到输出目录；`Block.DrawBlock` 使用 `BlockMesh` 和包内模型/贴图绘制创造栏物品。越野车和坦克会枚举全部 `model.Meshes`，适合多网格资源；旧飞艇/蒸汽船只按单个 mesh 名读取，不能原样作为直升机多部件物品绘制实现。

项目内 `public const int Index` 全量扫描显示 941 未占用；目标原生扫描最大 ID 为 263。因此 V0.1 可暂定使用 **941**。此结论只排除固定原生包和当前仓库内部冲突；SCNET 没有第三方 Mod 方块 ID 注册中心，与其它 Mod 同装冲突仍为`待验证`。

### 3.2 实体模板与生成

当前载具 XDB 都合并到原版文件夹 GUID：

- Entities：`5fcdf35f-7001-442c-883e-e0502b15d291`
- Gameplay：`d3d4b692-acc9-4128-9b99-a5acf1de1fbb`
- BlockBehaviors：`00c97f0f-731e-481c-9909-eae9cc5ee940`
- Project：`85023bf8-1c90-4dd1-9442-e6c13691d078`

实体模板组合已经稳定为：自定义 Drive、`ComponentSimpleModel`、`ComponentSpawn`、一个原生 `ComponentMount`、`ComponentBody`、可选 `ComponentDamage`，双座车辆再加 `ComponentTwoSeatManager`。证据分别位于：

- `Assets/Database/LandVehicles.xdb`：`SCIEPortATV`
- `Assets/Database/OffroadVehicles.xdb`：`SCIEPortOffroadCar`
- `Assets/Database/Watercraft.xdb`：`SCIEPortSteamBoat`、`SCIEPortAirship`
- `Assets/Database/Tanks.xdb`：`SCIEPortT34Tank`

生成行为不写地形，而是在 `SubsystemBlockBehavior.OnUse` 中射线取落点，再调用已验证签名 `SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)`。普通客户端先以 `WorkType.Client` 早退，生成物品只由 Local/Server 创建；成功生成后才 `RemoveActiveTool(1)`。越野车、坦克、蒸汽船和飞艇都使用这一路径。

结论：直升机应新增独立 Block/CSV、独立 XDB 和独立生成 Subsystem，复用生成方式，但不能改写任何现有载具模板或 GUID。

## 4. 移动、碰撞与保存机制

### 4.1 地面载具

`ComponentOffroadCarDrive` 与 `ComponentTankDrive` 都只在本机主玩家确为驾驶员时消费输入。它们写 `ComponentBody.Velocity` 和 `Rotation`，由原生 Body 继续负责重力、空气/水阻、地形及实体碰撞和保存。推进只在 `StandingOnValue` 接地且不站在其它 Body 上时启用；因此不能直接复制为飞行物理。

### 4.2 蒸汽船

`ComponentSteamBoatDrive` 同样只消费 `ComponentTwoSeatManager.DriverMove`，但以 `ImmersionFactor`、`StandingOnValue` 和 `StandingOnBody` 判定是否可推进。`ComponentBody` 负责浮力、水阻、碰撞、重力和持久化。该实现适合复用“Body 保持原生碰撞/保存、Drive 只写速度”的边界，不适合复制水面许可条件。

### 4.3 已有飞行实体

`Features/Watercraft/ComponentAirshipDrive.cs` 是现有唯一自定义飞行 Drive：

- 每帧关闭 `IsGravityEnabled` 和 `IsGroundDragEnabled`；
- 本机主玩家且确为 `m_mount.Rider` 时才读取触屏 `PlayerInput.Move`；
- X 控制 yaw，Z 沿骑手视线方向加速，所以视线俯仰同时决定升降；
- 总速度限制为 12；
- 深度浸水时仅 Local/Server 结算伤害和强制下车；
- 无驾驶员时只限速，不做重力、自动降落或安全悬停。

因此“飞行实体可以创建、由 `ComponentBody` 碰撞并由自定义 Drive 控速”是`已确认`；但飞艇没有双座、启动/停机、独立升降、悬停、高度限制、自动降落或旋翼动画，不能把它的物理逻辑直接复制为直升机。

### 4.4 碰撞和保存

`ComponentBody.BoxSize` 提供轴对齐碰撞体；模型网格不参与碰撞。`ComponentBody` 已有并经环境脚本验证的 `Velocity`、`Rotation`、`StandingOnValue`、`StandingOnBody`、`ImmersionFactor`、重力及阻力开关。现有实体以 `Spawn.AutoDespawn=False` 并依赖原生实体/Body 保存链恢复位置、旋转、速度和父子 Body 关系。

V0.1 应继续使用原生 Body 做地形/实体碰撞和保存，不逐帧传送、不直接修改世界存档、不发明第二套碰撞器。旋翼视觉不进入碰撞体；直升机碰撞盒只包络机身和必要安全范围。

## 5. 双座与驾驶权限

### 5.1 为什么不能放两个原生 Mount

目标 `ComponentMountPackage` 只携带 Rider 实体 ID、Mount 所在实体 ID及 Mount/Dismount/Request 类型，没有座位编号或组件名。接收端无法稳定寻址同一实体的第二个 `ComponentMount`。因此“同一实体放两个原生 Mount”在当前目标 ABI 上为`不支持`。

### 5.2 当前可复用双座机制

`Features/Vehicles/ComponentTwoSeatManager.cs` 已实现：

- 车辆仍只有一个原生 Mount；两名 Rider 都直接成为车辆 Body 的子 Body；
- Local/Server 决定第一个空位为 Driver、第二个为空位 Passenger，第三人被 `StartNetDismounting` 拒绝；
- Driver/Passenger 使用不同 `ParentBodyPositionOffset`，并在 Rider 更新后恢复；
- 只缓存 Driver 的移动输入，Passenger 的移动输入被清零；
- Driver Body 被稳定移到 `m_childBodies[0]`；
- 普通客户端乘客清除父车插值产生的 `SendPosition/Rotation/Velocity` 回声；
- 重载时依据已保存的父体偏移重建角色，不持久化玩家身份。

`TwoSeatAssignmentPackage` 使用包 ID 240，由服务器向客户端复制离散 Driver/Passenger 角色；`TwoSeatRules.CanControlVehicle` 只有 Driver 返回 true。当前 ATV、快艇和越野车都复用这一组件。

结论：直升机应复用一个原生 Mount + 一个 `ComponentTwoSeatManager`。`ComponentHelicopterDrive` 必须同时校验：当前 Driver 非空、该 Rider 对应本机 `ComponentPlayer`、`player.ComponentRider == Driver`、`PlayerData.IsMainPlayer`。Passenger 永远不进入 Drive 控制路径。

### 5.3 触屏升降输入缺口

现有双座管理器只缓存 `PlayerInput.Move`，并在输入重定向阶段清除 `Move`、`SneakMove`、`VrMove` 和 `Jump`。因此直升机不能在默认 Update 阶段再读取原始 Jump/Sneak 状态。

最小侵入方案是在共享管理器中**只追加只读缓存** `DriverJump` 与 `DriverSneakMove`，与现有 `DriverMove` 同一时刻捕获，再照旧清除骑手个人移动。现有地面/水上 Drive 不读取新增字段，行为不变。直升机使用：

- `DriverMove.Z`：前进/后退；
- `DriverMove.X`：左/右转向；
- `DriverJump`：上升；
- `DriverSneakMove` 非零：下降；
- 两者都没有：垂直速度趋近 0，进入悬停控制。

`PlayerInput.Jump` 和 `PlayerInput.SneakMove` 字段已由 `Test-Environment.ps1` 在目标程序集反射确认；Android 7723 上对应触屏按钮的可达性与手感仍为`待验证`。

## 6. 模型、资源与动态部件

现有模型/资源审计：

| 模型 | DAE 结构 | 目标用途 |
|---|---|---|
| `SteamBoat.dae` | Y_UP，1 node / 1 geometry | 静态授权船体 |
| `Airship.dae` | Y_UP，1 node / 1 geometry | 静态授权飞艇 |
| `OffroadCar.dae` | Y_UP，1 node / 24 geometries | 多网格、单骨骼静态车 |
| `ATV.dae` | Y_UP，1 node / 13 geometries | 多网格、单骨骼静态车 |
| `T34Tank.dae` | Y_UP，`TankHull > TankTurret > TankGun`，3 nodes / 20 geometries | 动态炮塔与炮管 |

目标 `ModLoader.GetMaxInstancesCount()` 默认世界模型预算为 7 骨骼。坦克已由目标 Engine 实际导入为 3 bones / 20 meshes，并在 `ComponentTankDrive.ApplyArticulation()` 中通过已反射确认的 `ComponentModel.SetBoneTransform(int, Matrix)` 驱动炮塔和炮管。因此动态独立部件渲染能力是`已确认`，无需额外渲染 Hook。

直升机最小模型应为 3 骨骼、低于 7 骨骼预算：

1. `HelicopterRoot`：机身、起落架、驾驶舱和尾梁静态网格；
2. `MainRotor`：以旋翼轴心为 pivot，绕本地 Y 轴旋转；
3. `TailRotor`：以尾桨轴心为 pivot，按模型朝向绕本地 X 或 Z 轴旋转。

Drive 每帧只更新两个旋翼骨骼。物品栏路径必须枚举全部 meshes 并烘焙 bind pose，世界路径由 XDB `ComponentSimpleModel` 加载；XDB 必须明确写 `ModelName` 和 `TextureOverride`。构建脚本必须用目标 `Engine.Media.ModelData.Load` 断言恰为 3 bones、预期 mesh 数、Y_UP、合法交错索引、模型包围盒和旋翼骨骼名。

若 Android 实测发现三骨骼旋翼路径失败，最小降级不是改引擎，而是保留 1 骨骼静态模型并停止旋翼骨骼更新，同时将旋翼动画标为`不支持`；不得用多个重叠实体或逐帧重建模型作为 V0.1 绕行。

直升机模型与贴图应由项目脚本 clean-room 生成或使用明确授权资源；不得复制现有上游未单独授权的飞艇、蒸汽船或其它第三方资产。

## 7. 联机同步审计

加载器当前注册：

- 240：双座角色分配；
- 241：坦克炮塔/开火；
- 242：载具激活授权。

目标原生包占用 0..38，以上 ID 对固定程序集无冲突；与第三方 Mod 同装的包 ID 冲突仍为`待验证`。

现有载具位置同步依赖目标原生机制，而非自建通用车辆位置包：骑乘玩家的 `ComponentPlayerPackage.BodyUpdate` 会发送父 Body 的 `SendPosition/SendRotation/SendVelocity`；`ComponentBody.Update` 以 `ChildBodies[0]` 是否为本机主玩家选择本地仿真或网络插值。双座管理器因此把 Driver 保持为首个子 Body，并抑制 Passenger 的父体回声。

直升机 V0.1 可复用相同位置/旋转/速度通道和 ID 240 座位角色包，不需要先新增飞行位置包。但该机制信任驾驶员客户端，不是严格服务器权威或抗作弊物理。更重要的是，当前七种载具的 Android 双机运行全部仍是`待验证`；因此直升机联机只能标记为：

- 代码接入/编译：实现后可验证；
- Android 双机位置、座位和运动同步：`待验证`；
- 严格服务器权威飞行：`不支持`。

旋翼角度不作为权威游戏状态发送。观察端根据驾驶员占用、Body 速度和本地引擎状态机驱动视觉旋转；若真实双机测试要求相位一致，再单独设计低频引擎状态包，V0.1 不预先扩张协议。

## 8. V0.1 最小实现方案

### 8.1 独立模块和资源

新增且仅新增到 `SCIE-Port`：

- `Features/AirVehicles/HelicopterBlock.cs`
- `Features/AirVehicles/SubsystemHelicopterBlockBehavior.cs`
- `Features/AirVehicles/ComponentHelicopterDrive.cs`
- `Features/AirVehicles/HelicopterFlightRules.cs`
- `Assets/Blocks/Helicopter.csv`
- `Assets/Database/Helicopter.xdb`
- `Assets/Models/Helicopter.dae`
- `Assets/Textures/Helicopter.png`
- 可复现资源生成脚本和独立纯规则测试项目

暂定方块 ID 941、实体模板名 `SCIEPortHelicopter`。所有新实体/组件/行为/member/parameter GUID 都必须新生成，并由数据库校验脚本证明不与仓库已有 GUID 冲突。不得改写已有 ID、实体模板或 GUID。

### 8.2 实体组成

`SCIEPortHelicopter` 计划包含：

- `TwoSeatManager`：驾驶员与乘客唯一角色；
- `HelicopterDrive`：独立飞行状态机和速度积分；
- `SimpleModel`：三骨骼模型与明确 PNG 贴图；
- `Spawn`：`AutoDespawn=False`；
- 一个原生 `Mount`：入口锚点及碰撞盒外安全下车点；
- `Body`：机身碰撞、实体/地形碰撞、父子 Body 和保存；
- `Damage`：有限耐久，具体数值在实现时配置并测试。

驾驶座和乘客座都必须在碰撞盒内部；`DismountOffset` 必须位于碰撞盒外。生成点应抬高到落点上方，避免初始碰撞嵌入地形；附近实体数量使用有界上限。

### 8.3 可配置飞行参数

参数放在 `SCIEPortHelicopterDrive` XDB ComponentTemplate，由 `Load(ValuesDictionary, ...)` 读取，纯规则层接受同一组值：

- 启动/停机旋翼加减速时间；
- 自定义重力加速度；
- 悬停补偿和垂直响应；
- 上升/下降最大速度；
- 水平加速度、制动/阻尼、前进/后退最大速度；
- yaw 转向速率；
- 最大世界 Y 高度及顶部缓冲；
- 无驾驶员自动降落速度；
- 每帧 `dt` 上限。

V0.1 不把这些参数写死在地面载具规则中，也不复用越野车/坦克的接地推进函数。

### 8.4 飞行状态机

计划状态：`Off`、`SpoolingUp`、`Running`、`AutoLanding`。

- 驾驶员首次给出移动/上升输入时启动并进入 `SpoolingUp`；旋翼达到安全转速前只允许受控减重，不瞬间弹射。
- `Running`：Jump 上升，Sneak 下降，无垂直输入时抵消自定义重力并把垂直速度平滑收敛到 0；Move.Z 前后加速，Move.X yaw 转向。
- 松开水平输入时施加有界空气阻尼，形成悬停而不是无限滑行。
- 达到最大高度时禁止继续正向垂直加速，并平滑消除向上速度；不直接改 Position。
- 驾驶员离开且仍离地时进入 `AutoLanding`，保持水平阻尼并以有限下降速度落地；接地后停机。
- 驾驶员离开但 Passenger 仍在时，Passenger 仍不能接管控制，车辆同样自动降落。
- 深水或异常状态只由 Local/Server 结算伤害/强制下车，普通 Client 不重复写权威伤害。

为保留真实重力语义且避免与原生重力重复，Drive 计划关闭 `ComponentBody.IsGravityEnabled`，然后在唯一飞行积分中显式施加可配置重力与升力；碰撞、移动解析、阻力边界和保存仍由原生 Body 负责。不得逐帧写 `Position` 绕过碰撞。

### 8.5 安全与权限

- Passenger 输入在共享双座层被抑制，Drive 再做 Driver 身份校验，形成双重权限边界。
- 第三名 Rider 由现有双座管理器安全拒绝。
- 上下机沿用原生 Mount/Rider 动画和碰撞检查，配合碰撞盒外下车点。
- 驾驶员空中离开后不让直升机继续无动力平飞或永久悬空，而是自动降落。
- 不直接修改地形或世界存档。
- 生成、伤害和其它权威副作用只允许 Local/Server；普通 Client 不本地制造实体。
- 现有统一载具激活闸需要把直升机作为第八种载具纳入生成和驾驶入口，但不得改变已有七种载具的 ID、实体或协议载荷。

## 9. 实现与验收顺序

1. 新增纯 `HelicopterFlightRules` 和离线测试，覆盖启动、旋翼加减速、重力、悬停、升降/水平限速、高度上限、自动降落和 Passenger 无控制权。
2. 生成三骨骼 clean-room 模型/贴图；用目标 Engine 导入断言和包围盒检查通过后，才接实体。
3. 新增 Block/CSV、Subsystem/XDB 与 Drive；只追加共享双座输入缓存，不改变现有 Drive 读取逻辑。
4. 更新环境/数据库/打包守卫，先运行 `Test-Environment.ps1`，再跑全部离线测试、Release 编译和 `.netmod` 内容检查。
5. 更新 `docs/COMPATIBILITY.md`、`docs/TEST-RESULTS.md`、README 和根目录 `docs/blueprint-block-reference.md`；直升机自定义方块不自动加入 Builder Assistant 蓝图支持清单。
6. Android 7723 新建可丢弃单人世界验收创造栏、生成、双座、起飞、悬停、移动、降落、碰撞、上下机和保存重载。
7. 单人通过后，使用两台安装相同 SHA-256 `.netmod` 的真实 Android 客户端测试房主/客户端互换驾驶与乘客、第三人拒绝、跨区块、断线重连、位置/座位/运动一致性。通过前联机保持`待验证`。

## 10. 审计阶段结论（实现前）

| 能力 | 当前结论 |
|---|---|
| 独立直升机物品/实体注册 | 可按既有 Block + CSV + XDB + Spawn 路径实现；尚未实现 |
| 双座唯一角色 | 可复用一个 Mount + `ComponentTwoSeatManager`；源码/ABI 已确认，Android 待验证 |
| Passenger 禁止驾驶 | 现有输入隔离与 Driver 校验可复用；源码/规则已确认，Android 待验证 |
| 简化飞行物理 | 已有飞艇证明 Body + 自定义 Drive 可行；直升机状态机尚未实现 |
| 地形/实体碰撞与保存 | 可复用原生 `ComponentBody`；ABI 已确认，直升机 Android 待验证 |
| 主旋翼/尾桨动画 | `SetBoneTransform` 与 3 骨骼坦克先例已确认；直升机资源/Android 待验证 |
| 联机位置/座位/运动 | 仅存在可复用代码路径；真实双机全部待验证，不得宣称已实现 |
| 严格服务器权威飞行 | V0.1 不支持 |

技术结论：可以进入最小实现阶段，但必须以独立 AirVehicles 模块、一个原生 Mount、三骨骼模型、可配置飞行状态机和原生 Body 碰撞/保存为边界；不得照搬越野车/坦克接地物理，也不得把现有未实测的父车体网络路径写成“联机已完成”。

## 11. 最小实现结果（0.25.0）

审计完成后已按上述边界实现，不再是“尚未实现”状态：

- `Features/AirVehicles/HelicopterBlock.cs` 与 `SubsystemHelicopterBlockBehavior.cs` 注册 ID 941，并以独立实体 `SCIEPortHelicopter` 生成；普通 Client 不创建实体，统一激活闸先于生成和物品扣除。
- `HelicopterFlightRules.cs` 隔离所有飞行积分；`ComponentHelicopterDrive.cs` 从 XDB 读取重力、旋翼、升降、水平加减速/限速、yaw、高度和自动降落参数，只写 Body 速度/旋转，不绕过原生碰撞。
- `ComponentTwoSeatManager` 新增 Driver 专属 Jump/Sneak 缓存；Passenger 输入不作为 Drive 控制源。实体仍只有一个原生 Mount，由包 240 同步唯一 Driver/Passenger 角色。
- clean-room 模型由 `scripts/New-HelicopterAssets.ps1` 生成。目标 Engine 已实际导入为 `HelicopterRoot/MainRotor/TailRotor` 三骨骼、17 meshes，主/尾旋翼用独立骨骼变换。
- `SCIEPort.AirVehicles.Tests` 28 断言、全工程 Release、12 个 XDB 数据库验证和 `.netmod` 包校验已通过；详细命令和最终哈希见 `TEST-RESULTS.md`。

状态边界不变：Android 7723 的创造栏、触屏实飞、双座、碰撞、存档重载均为`待验证`；两台真实客户端的位置、座位和运动同步均为`待验证`；严格服务器权威飞行为`不支持`。
