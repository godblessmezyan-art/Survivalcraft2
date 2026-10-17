# 快艇开源项目调研与 Net_x25.01.17 移植分析

调研日期：2026-10-17。目标运行环境为 Survivalcraft NET 2.4 `Net_x25.01.17`、Android、`.netmod`。本文只把公开项目当作设计证据；最终接口结论来自本机目标程序集，不以旧版 Mod、SCAPI 或当前 SCNET 主分支代替。

## 结论摘要

- 独立物品、实体模板、单座乘坐、原生触屏操纵和有限额生成在当前项目中已经有原型，且通过了 x25.01.17 Windows 程序集编译；尚未在 Windows 游戏进程、Android 快艇功能或多人联机中运行验证。
- 现有实现不是双座。`Speedboat.xdb` 只有一个 `ComponentMount`，`SpeedboatFeature` 读取当前本地玩家输入并直接改变船体速度。
- x25.01.17 原生骑乘协议只有“骑手实体 ID + Mount 实体 ID”，没有座位 ID。`ComponentRider.Mount` 也只从直接父 Body 的实体取一个 `ComponentMount`。在同一实体中再声明一个 Mount 不能可靠形成两个座位。
- x25.01.17 的载具同步与骑手父子 Body 关系强耦合：骑手的 `ComponentPlayerPackage.BodyUpdate` 会同步其直接父 Body；`ComponentBody` 又以 `ChildBodies[0]` 判断载具状态来源。直接把两名玩家都挂到船体会让副驾驶也可能上报船体状态，不能仅靠 UI 隐藏实现“主驾驶独占控制”。
- 推荐先保留当前单座最小原型并完成 Android 单机验证。双座采用“船体上的主驾驶原生 Mount + 随船体的副驾驶座锚实体”方案；副驾驶的直接父 Body 是座锚而不是船体，因而不会进入原生 `ComponentBoat` 控制链。该方案仍需在联机阶段验证座锚创建顺序、实体同步和断线清理。
- 当前 `Speedboat.dae` 只含 8 个位置、8 个 UV 和 13 个三角形，虽然存在代码要求的 `Speedboat` 网格节点，但不是按 1254×1254 细节贴图制作的 UV 展开。贴图能被采样不等于图案能正确落在船壳和甲板上。

## 1. 当前项目审查

### 1.1 已实现

| 项目 | 代码/资源 | 当前结论 |
| --- | --- | --- |
| 独立物品 | `Features/Speedboat/SpeedboatBlock.cs`、`Assets/Blocks/Speedboat.csv` | 内容编号 `900`；独立模型和贴图绘制；Windows 静态构建已确认 |
| 实体模板 | `Assets/Database/Speedboat.xdb` | 继承原生 Boat、SimpleModel、Spawn、OnFire、Mount、Body、Damage 模板 |
| 生成 | `SubsystemSpeedboatBlockBehavior.cs` | 点击表面生成 `Speedboat`；8 格内最多 4 艘；消耗一个物品 |
| 乘坐 | 原生 `ComponentMount` / `ComponentRider` | 一个 `MountOffset`：`0,0.75,-0.2`；一个下船偏移 |
| 动力 | 原生 `ComponentBoat` + `SpeedboatFeature.cs` | 原生 3 倍推进之外追加加速度 `9`，水平速度上限 `18` |
| 触屏 | `ComponentInput.PlayerInput.Move` | 复用原生移动摇杆，无键盘依赖 |
| 资源 | `Speedboat.dae`、`Speedboat.png` | 模型节点名正确；资源已进入 `.netmod`；实际渲染待验证 |

### 1.2 未实现或未证明

- 没有第二个座位、座位占用表、主副驾驶角色或座位选择 UI。
- 没有针对上船竞争、驾驶员离开、死亡、实体损坏、世界卸载和玩家断线的快艇状态机。
- 没有快艇专用输入包或服务器端驾驶权限验证。当前联机依赖 SCNET 原生父 Body 同步，服务端是否会回弹尚未实测。
- `SpeedboatFeature` 运行于 `GuiUpdate`，它是本地 GUI 更新路径，不是经过证明的服务器载具更新路径。
- 生成使用 `SubsystemCreatureSpawn.SpawnCreature`。该方法本身不检查 `WorkType`；联机生成是否正确依赖原生交互事件在客户端/服务端的复现和 `ProjectNet` 实体广播，必须实测。
- 没有 Windows 游戏内运行记录、Android 快艇功能记录或多人记录。

本次最小改动只给额外推进增加 `mount.Rider == player.ComponentRider` 检查，使其只作用于 x25.01.17 定义的首位 Rider；这不是完整双座驾驶权限，因为原生 `ComponentPlayer` 仍会向所在 `ComponentBoat` 写 `MoveOrder/TurnOrder`。

## 2. 参考项目、源码路径与许可证

调研固定到以下提交，避免主分支后续变化污染结论。

| 项目 | 固定提交与重点路径 | 许可证与复用边界 |
| --- | --- | --- |
| SCIE | [`4864d956`](https://github.com/Lixue9jiu/SCIE/tree/4864d956793b6e670413019f1f6b1cc5c8bb0970)：[`ComponentBoatI.cs`](https://github.com/Lixue9jiu/SCIE/blob/4864d956793b6e670413019f1f6b1cc5c8bb0970/Components/ComponentBoatI.cs)、[`ComponentAirShip.cs`](https://github.com/Lixue9jiu/SCIE/blob/4864d956793b6e670413019f1f6b1cc5c8bb0970/Components/ComponentAirShip.cs)、[`ComponentTrain.cs`](https://github.com/Lixue9jiu/SCIE/blob/4864d956793b6e670413019f1f6b1cc5c8bb0970/Components/ComponentTrain.cs)、`工业mod.xdb` | 仓库没有 LICENSE。默认没有复制、修改或分发授权；仅用于理解思路和识别待验证接口，不复制源码、模型、贴图、音频或数据库 GUID。 |
| ZanJhatCraft | [`756d849d`](https://github.com/ZanJhat/ZanJhatCraft/tree/756d849df4a8ecf0e6e0c57324b0e110aa99ebd8)：`ZanJhatCraft/Game/Moto/ComponentMoto.cs`、`ComponentMotoInfo.cs`、`SubsystemMotoBlockBehavior.cs`、`SubsystemOpenMotoBehavior.cs`、`MotoInfoWidget.cs`、`437 - MotoBlock.cs`，以及 `Content/New - Database.xdb` | [MIT](https://github.com/ZanJhat/ZanJhatCraft/blob/756d849df4a8ecf0e6e0c57324b0e110aa99ebd8/LICENSE)。允许使用、修改和分发，但复制实质代码时必须保留版权与许可文本。本次只复用架构思路，没有复制代码或资源。 |
| SurvivalcraftRuins | [`53425779`](https://github.com/zh667/SurvivalcraftRuins/tree/53425779dc499c6f08b94f4b973f329217f92765)：`tools/package.sh`、`SurvivalcraftRuins.csproj`、`RuntimeCompat.cs`、`modinfo.json` | [木兰宽松许可证 v2](https://github.com/zh667/SurvivalcraftRuins/blob/53425779dc499c6f08b94f4b973f329217f92765/LICENSE)。可复制、修改和分发，但分发时须附许可证并保留声明。本次只借鉴“双运行时分别编译、分别生成元数据和包”的原则。 |
| SCNET | [Gitee 仓库](https://gitee.com/SC-SPM/SurvivalcraftNet)，当前公开目录包含 `Component/`、`NetWork/`、`ModsManager/` 等；网页当前提交显示 `b7010b0`，另有 `x25.01.17` 标签 | 仓库页面明确显示未指定许可证，不能把源码直接复制进本项目。当前主分支晚于目标版本，只作为导航；本文的 x25 结论来自目标程序集反编译与签名检查。 |
| Survivalcraft API 示例 | [`80a9e325`](https://github.com/XiaofengdiZhu/SurvivalcraftTemplateModForAPI/tree/80a9e325ec8b68fa5d7632b36bed1fdf008f3690)：`TemplateBlock.cs`、`TemplateModLoader.cs`、`Assets/TemplateDatabase.xdb`、项目文件 | [MIT](https://github.com/XiaofengdiZhu/SurvivalcraftTemplateModForAPI/blob/80a9e325ec8b68fa5d7632b36bed1fdf008f3690/LICENSE)。可借鉴 SCAPI 的资源组织与示例写法，但其 net10、SCAPI 1.9.3、`.scmod` ABI 不适用于 x25.01.17 `.netmod`。 |

当前 Builder Assistant 仓库自身没有 LICENSE。若后续实质采用 MIT 或木兰许可代码，发布前必须先确定本项目许可证并附上相应声明；无许可证的 SCIE/SCNET 代码继续只作行为参考。

## 3. 参考实现的技术价值

### 3.1 SCIE

- `ComponentBoatI` 继承 `ComponentBoat`，沿用沉没阈值、转向平滑、浮水有效状态和每帧清零控制量；动力大小由自定义引擎热量提供。
- `ComponentAirship` 展示了如何复用 Boat 控制输入但替换重力、介质和推进方向。
- `ComponentTrain` 展示父子实体引用、编组跟随、方向约束、碰撞伤害和保存引用。
- 值得复用的是“组件化拆分、控制量每帧消费、状态保存”的设计原则。其 `new Update` 隐藏基类方法、直接字段访问和旧版模板 GUID 都不能直接移植。

### 3.2 ZanJhatCraft Moto

- `MotoBlock`、XDB 实体、`SubsystemMotoBlockBehavior` 形成完整的“独立物品 → 射线选点 → 创建实体 → 消耗物品”链路。
- `ComponentMoto` 复用 `ComponentBoat.MoveOrder/TurnOrder`，按接地状态驱动，并处理能量、碰撞和入水损坏。
- `SubsystemOpenMotoBehavior` 与 `MotoInfoWidget` 展示空手点击载具打开触屏面板的方式。
- 这些模式适合参考，但项目元数据为 API 1.50 / `ScVersion 2.2.10.4`，不是 SCNET x25.01.17；其单 Mount 设计也没有解决双座或驾驶权限。

### 3.3 SurvivalcraftRuins 与 API 示例

- Ruins 正确强调 `.scmod` 与 `.netmod` 必须针对各自运行时分别编译，不能只改扩展名；打包前还按运行时生成不同 `modinfo.json`。
- 其当前 `net10.0`、API 1.44、SC 2.4.40.6 配置比 x25.01.17 新，不能照搬 TargetFramework 或元数据。
- API 示例证明 SCAPI 的块、XDB、资源和 Hook 组织方式，但其 Harmony、net10 与 `.scmod` 打包不属于本项目支持面。

## 4. x25.01.17 实际 ABI 对照

检查对象：

- `Survivalcraft.exe` 2.4.0.0，SHA-256 `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`
- `Engine.dll` SHA-256 `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`
- `EntitySystem.dll` SHA-256 `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`

`scripts/Test-SpeedboatAbi.ps1` 对需要编译调用的公开类型、字段、属性、方法和网络包构造函数进行可重复签名检查。行为结论另由反编译目标程序集取得，不能仅由“成员存在”推出。

| 能力 | 参考项目接口 | x25.01.17 实际接口/行为 | 状态 |
| --- | --- | --- | --- |
| 船动力 | SCIE/Moto 继承 `ComponentBoat` | `MoveOrder`、`TurnOrder`、公开 `m_componentBody/m_componentMount/m_subsystemTime`；浮水时原生推进系数 3 | 已确认 |
| 转向 | `m_turnSpeed` 平滑并修改 Y 轴旋转 | 字段和算法存在；订单在 `ComponentBoat.Update` 末尾清零 | 已确认 |
| 浮力/阻力 | 依赖 `ComponentBody` 密度、浸水、WaterDrag | XDB 中 `Density/WaterDrag/WaterSwayAngle` 可由原生 Body 模板读取 | 已确认 |
| 独立实体 | `DatabaseManager.CreateEntity` 或 CreatureSpawn | `CreateEntity(Project,string,bool)` 与 `SpawnCreature(string,Vector3,bool)` 存在 | 已确认 |
| 单座 | `ComponentMount` | 只有单个 `MountOffset/DismountOffset`，`Rider` 返回其 Body 的第一个 Rider 子 Body | 已确认 |
| 上下船 | `ComponentRider.StartMounting/StartDismounting` | 本地/主机直接改变父 Body 并发包；客户端先发 Request | 已确认 |
| 骑乘同步 | 无 | `ComponentMountPackage` 只有 Rider ID、Mount ID、Mount/Dismount/Request 类型，无座位号 | 已确认 |
| 船体同步 | 参考项目未处理 SCNET | 骑手的 `ComponentPlayerPackage.BodyUpdate` 在骑乘时同步直接父 Body 的位置、旋转、速度 | 已确认 |
| 驾驶输入 | 参考项目读 `MoveOrder/TurnOrder` | 原生 `ComponentPlayer.Update` 在父实体找到 `ComponentBoat` 时写入移动摇杆 X/Z | 已确认 |
| 主副驾驶 | 参考项目均为单 Mount | 无原生座位角色与驾驶权限接口，需自定义 | 不支持 |
| 实体广播 | Ruins 主机写、客户端接收 | `ProjectNet` 在服务端 Add/Remove Entity 时广播 `EntityPackage` | 已确认 |
| 自定义网络包 | 无统一实现 | Mod 类型扫描会注册 `IPackage` 子类；包 ID 冲突、双方 Mod 一致性和 Android 行为未验证 | 待验证 |
| UI | Moto Widget | x25 有触屏 Widget 与 `GuiUpdate`，但座位选择挂载点尚未实机验证 | 待验证 |
| 包格式 | Ruins 双编译 | x25 为 net472 DLL + 根目录 `modinfo.json` + `Assets/` 的 ZIP `.netmod`；Android 功能待验证 | 已确认 |

重要差异：

1. SCIE 目标 `.NET Framework 4.5`，当前项目已验证目标是 net472；旧 DLL 不可复用。
2. ZanJhatCraft Moto 的方法调用来自旧 API；即使同名，也必须通过本项目 ABI 脚本和目标程序集重新确认。
3. API 示例与 Ruins 当前版本为 net10/新 API；其包组织原则可参考，二进制和元数据不可移植。
4. 当前公开 SCNET 主分支不是 x25.01.17。只有上述固定哈希的本机程序集结论可以标记“已确认”。

## 5. 可复用与必须重写

### 值得复用

- 保留当前 `SpeedboatBlock`、XDB 实体和资源路径，它们已按 x25.01.17 编译，不引入外部 DLL。
- 保留原生 Boat 的浮力、损坏、沉没、触屏输入和单座上/下船流程，减少一次改动的 ABI 面。
- 保留附近数量限制和速度硬上限。
- 借鉴 Moto 的独立物品、实体生成和触屏面板分层，但自行实现代码。
- 借鉴 Ruins 的“运行时分别编译、包内不带游戏 DLL、元数据按 ABI 生成/校验”。

### 必须重写

- 双座占用与角色状态机；原生 Mount 没有容量和座位 ID。
- 副驾驶座锚的生命周期、实体引用保存和重载恢复。
- 驾驶权限检查；不能只隐藏副驾驶 UI，也不能只检查本地玩家。
- 联机输入/状态同步策略、请求验证、包 ID 管理和 Mod 不一致时的拒绝/降级行为。
- 驾驶员死亡、离开、断线、船毁、世界卸载时的清理和可选换驾。
- 与正式贴图匹配的 UV 展开和足够细分的模型。

## 6. 推荐双座架构

### 6.1 实体组成

第一阶段继续使用当前单体实体：

```text
Speedboat entity
  ComponentBody
  ComponentBoat
  ComponentMount (driver)
  ComponentSpeedboatSeats (later)
```

双座阶段推荐混合结构：

```text
Speedboat entity
  Body + custom ComponentSpeedboat
  driver ComponentMount
  ComponentSpeedboatSeats
  passenger-seat anchor entity reference

PassengerSeatAnchor entity
  tiny/non-driving ComponentBody
  ComponentMount (passenger)
  ComponentSpeedboatSeatAnchor -> follows boat at passenger offset
```

主驾驶直接挂到船体，继续使用 x25 原生“骑手同步父 Body”的船体同步路径。副驾驶挂到没有 `ComponentBoat` 的座锚，原生 `ComponentPlayer` 因此不会把副驾驶摇杆写入船体。座锚应在主驾驶之后成为船体子 Body，或采用独立跟随；若采用子 Body，必须保证 `ChildBodies[0]` 始终是主驾驶，否则会改变 x25 的船体网络权威判定。

### 6.2 座位与权限

- 驾驶座偏移和副驾驶偏移分别存于 XDB 参数，例如 `DriverOffset`、`PassengerOffset`，不要散落硬编码。
- 服务端/Local 是座位分配真值来源。座位请求必须检查距离、船体存活、座位空闲、玩家未在其他载具、最大两人和请求节流。
- 只有当前驾驶座 Rider 可以产生推进/转向。副驾驶只允许相机、背包和下船输入。
- 第三个乘员请求必须拒绝并显示触屏可见消息，不能先挂载后长期依赖客户端纠正。
- 第一版不自动换驾：驾驶员离开、死亡或断线时，副驾驶安全下船，船体减速停止。自动换驾留到座位同步稳定以后。

### 6.3 状态机

| 事件 | 主驾驶 | 副驾驶 | 船体/座锚处理 |
| --- | --- | --- | --- |
| 进入 | 服务端确认驾驶座空闲后挂载 | 驾驶座占用后挂到副驾锚 | 广播确认；失败回滚 |
| 主驾驶主动离开 | 清空驾驶角色 | 第一版强制安全下船 | 清零输入，移除/解绑座锚 |
| 副驾驶主动离开 | 不变 | 清空并下船 | 座锚可保留到驾驶员离开或立即移除 |
| 玩家死亡 | 调用权威下船 | 调用权威下船 | 不继续处理该玩家输入 |
| 主驾驶断线 | 服务端清空 | 第一版安全下船 | 停船并广播最终状态 |
| 副驾驶断线 | 不变 | 服务端清空 | 移除孤立座锚 |
| 船毁/沉没 | 两人均下船 | 两人均下船 | 先解绑玩家，再移除座锚和船体 |
| 世界卸载/读档 | 保存引用或重新派生 | 保存引用或重新派生 | 禁止留下无船座锚；坏引用降级清理 |

### 6.4 联机策略

- 单人 `WorkType.Local`：直接读取主驾驶触屏输入，在游戏更新线程模拟。
- 主机玩家：在主机本地模拟，并让现有 ProjectNet 广播实体与 Body 状态。
- 普通客户端：短期可沿用 x25 原生“主驾驶客户端上报父 Body”路径，但必须验证回弹、延迟与副驾驶回声包。若不能稳定阻止副驾驶影响船体，则改用自定义 `SpeedboatInputPackage`：客户端只发油门/转向/序号，服务端验证驾驶身份并模拟。
- 自定义包必须同时安装在服务端与所有客户端，选择未占用包 ID，限制频率和数值范围，并拒绝非驾驶员、未知实体、过期序号和超距离请求。任何包 ID/格式都要写入兼容性文档。
- 不在 `GuiUpdate` 中承担服务端物理。最终动力组件应实现 `IUpdateable`，明确更新顺序并在主线程消费已验证输入。

## 7. 模型和贴图核查

### 静态结果

- `Speedboat.dae` 有且只有一个 `geometry` 和一个名为 `Speedboat` 的节点；`SpeedboatBlock.Initialize()` 正在按此名称查找，必须保留，除非同步修改并实机验证代码。
- 模型只有 8 个位置、6 个法线、8 个 UV、13 个三角形。UV 仅使用整图四角、中点和少量四分点。
- `Speedboat.png` 是 1254×1254、24 bpp RGB、非 2 次幂的细节材质图，包含明确的船壳、白条、甲板和警示色区域。
- 当前 8 个通用 UV 无法把这些区域分别精确分配给左右船壳、甲板和艉部；一个几何顶点也无法在不同面的 UV 接缝处拥有独立坐标。因此“资源能加载”是已确认，“UV 真正匹配”是待验证，静态分析已显示大概率错位/拉伸。

### 修正要求

1. 在 Blender 等工具中按实际贴图重新展开，UV 接缝处复制顶点，保留 `Speedboat` 网格名。
2. 确认船头方向与 `ComponentBody.Matrix.Forward` 一致；当前模型长轴船头看似朝 `+Z`，x25 实际前向需在游戏内确认，必要时整体旋转 180°。
3. 优先制作 1024×1024 兼容贴图版本并与原图目视比较；是否必须降为 2 次幂由目标 Android 渲染结果决定，不能仅凭桌面加载判断。
4. 在物品栏、第一人称手持、世界实体、阴影和不同视距分别截图；检查白条连续性、左右舷镜像、甲板区域、接缝和纹理方向。

## 8. 分阶段开发计划

### Phase A：当前单座最小闭环

- 保持一个 Speedboat 物品、一个实体、一个座位。
- 完成 x25 ABI 脚本、Release 构建和包内容检查。
- 在新建单人世界验证生成、物品消耗、乘坐、触屏前后/转向、下船、沉没、保存重载和普通 Boat 不受影响。
- 只有完成实际设备步骤后，才把 Android 单人状态改为“已确认”。

### Phase B：本地双座

- 新增 `ComponentSpeedboatSeats` 和副驾驶座锚，仅在 Local 启用。
- 增加触屏座位选择或明确的最近空位规则。
- 验证两名本地玩家/可用测试角色同时乘坐、独立偏移、第三人拒绝、主驾驶独占输入、两人分别下船。
- 不改联机包，不宣称联机支持。

### Phase C：主机与客户端同步

- 在服务端创建/删除座锚并验证 `EntityPackage` 广播。
- 先验证原生父 Body 同步；若副驾驶回声或权威冲突无法消除，再增加受验证的自定义输入/座位包。
- 分别记录主机驾驶、客户端驾驶、主机副驾、客户端副驾、断线重连和 Mod 不一致行为。

### Phase D：模型与发布收口

- 完成 UV 重展和 Android 多分辨率截图。
- 处理包 ID/方块 ID 冲突策略、迁移旧存档、损坏实体清理和性能预算。
- 更新 README、COMPATIBILITY、TESTING 和发布说明；不打包游戏 DLL、旧 DLL 或来源不明资源。

## 9. 测试标准与状态口径

每次快艇改动至少执行：

```powershell
./scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release
```

并检查 `.netmod` 只包含 Mod DLL、`modinfo.json` 和自有 Assets。运行测试必须使用新建世界和可丢弃备份。

| 层级 | 通过条件 | 本次状态 |
| --- | --- | --- |
| 已通过编译 | 目标哈希匹配、ABI 检查通过、Release 0 错误、包内容通过 | 构建完成后记录到 `docs/TESTING.md` |
| 已在 Windows 运行 | Windows x25 游戏实际加载，完成指定操作并留日志/截图 | 待验证 |
| 已在 Android 实测 | 7723 目标客户端记录设备、Android、程序集指纹、步骤与结果 | 仅 v0.1 最小加载已确认；快艇待验证 |
| 已通过多人联机测试 | 服务端、主机、普通客户端分别执行矩阵并记录同步/断线/重连 | 待验证 |

建议的联机矩阵至少覆盖：主机生成、客户端生成、主机驾驶/客户端副驾、客户端驾驶/主机副驾、两普通客户端同船、同时抢驾驶座、第三人上船、驾驶员死亡、驾驶员断线、副驾驶断线、船毁、保存重载、不同延迟和仅一端安装 Mod。任何一项未执行，都不能写“多人兼容”。
