# ATV 双座越野摩托 v0.3 技术审计

日期：2026-10-20  
审计基线：`main` / `c031ed85c38856e3c5f7e4b2cd1ccc126fca6711`  
目标：Survivalcraft NET 2.4 `Net_x25.01.17`，Android 7723，`net472`

## 结论

双座在目标 ABI 上可以实现，但不能在同一个车辆实体中复制两个 `ComponentMount`。原生联机包只传递乘员实体 ID 和 Mount 所在的实体 ID，接收端调用 `FindComponent<ComponentMount>()`，无法寻址同一实体中的第二个 Mount。

v0.3 采用以下最小架构：

- `SCIEPortATV` 保持 ID 923、实体名和原 GUID，原生 Mount 作为驾驶位；
- 两名乘员都通过唯一原生 Mount 成为 ATV Body 的直接子 Body，避免多层父子链让乘客与祖父车体发生原生碰撞；
- 主机/本地权威把首名乘员关联为驾驶员、第二名关联为乘客，并只在座位变化时写原生骑乘目标偏移；
- 联机使用服务端到客户端的离散 `TwoSeatAssignmentPackage`（ID 240）复制座位角色；普通客户端不能向服务器分配座位；
- 乘员继续由原生 `ParentBodyPositionOffset` 跟随车辆，不逐帧写玩家 Position；
- 座位管理器从父子 Body 关系重建占用状态，不把临时玩家 ID 写入存档，避免断线后幽灵乘员；
- 同一原生 Mount 被重复占用时，仅主机/本地权威保留稳定占用者，将第二人改派到乘客座，满员后让额外乘员走原生下车流程。

## 实际程序集证据

| 程序集 | SHA-256 |
|---|---|
| `Survivalcraft.exe` | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` |
| `EntitySystem.dll` | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` |
| `Engine.dll` | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` |

通过 IL 反编译和随后针对上述程序集的 Release 编译确认：

| API/行为 | 实际结果 | 对设计的影响 |
|---|---|---|
| `ComponentMount.Rider` | 遍历 Mount Body 的 `ChildBodies`，返回首个带 `ComponentRider` 的子 Body | 原生 Mount 没有容量检查，必须由权威座位管理器消除重复占用 |
| `ComponentRider.Mount` | 从玩家 Body 的 `ParentBody.Entity` 查找首个 Mount | 同实体双 Mount 无法稳定区分 |
| `FindNearestMount()` | 每个附近 Body 只取实体上的首个 Mount | 复制第二个 Mount 不可用 |
| `ComponentMountPackage` | 只含 rider ID、target entity ID 和事件类型 | 包中没有座位编号或组件名；需要单独的服务端座位关联事件 |
| `StartMounting()` / `StartDismounting()` | Local/Server 执行并广播；Client 只发 request | 座位最终分配必须在 Local/Server 执行 |
| `ComponentBody.ParentBody` | 维护 ChildBodies，并保存父实体 ID、位置/旋转偏移 | 可稳定跟随并随存档恢复，不需要逐帧传送 |
| `ComponentBody.FindBodiesCollisionBoxes` | 只排除直接父 Body 和直接子 Body，不排除祖父 Body | 乘客不能放在“ATV → 锚点 → 玩家”的两层 Body 链，必须直接挂 ATV |
| `PackageManager.RegisterPackage` | 可注册 Mod 自定义 `IPackage`；目标原生只占 ID 0–38，240 未占用 | v0.3 注册 ID 240；与其它 Mod 的自定义包冲突仍需安装时检查 |
| 骑乘视角 | 自定义载具会落入普通玩家 TurnOrder 分支；v0.22.1 在 Player 更新前把 Look.X 重定向到各骑手 LookOrder | 代码/ABI 已确认；Android 自由瞄准待验证 |

## 联机权威边界

目标版本的 `ComponentPlayerPackage.BodyUpdate` 在玩家骑乘时会发送父 Body（车辆）的坐标、旋转和速度。`ComponentBody` 也以首个子玩家判断模拟权威。因此，原生实现实际允许骑乘中的普通客户端同步父车辆状态。

本项目不能在不知道服务器纠偏完整链路的情况下声称“普通客户端只发输入、服务器模拟车辆”。若要让普通客户端成为驾驶员，至少需要：

1. 新增带驾驶员身份验证、序号和限频的输入请求包；
2. 服务器模拟油门、转向与刹车；
3. 阻止或替换原生骑乘玩家对父车辆的 `BodyUpdate`；
4. 实现客户端预测、服务器纠偏及断线重连；
5. 在两台 Android 设备上验证延迟、抖动、下车和区块重载。

v0.3 最小实现因此限定为：

- 单人 `WorkType.Local`：支持驾驶员和本地座位规则；
- 联机 `WorkType.Server`：本机主玩家在驾驶位时驱动车辆，服务器负责座位改派和拒绝第三人；
- 联机 `WorkType.Client`：本机主玩家在驾驶位时沿用 v0.22 原生父车 BodyUpdate 驾驶；作为乘客时不运行 Drive、不自行决定座位，并抑制父车插值回声；
- 普通客户端担任驾驶员已接入 v0.22 的原生父体同步，但严格服务器权威/抗作弊物理仍**不支持**。

## 固定座位与资源设计

| 项目 | 值 |
|---|---|
| 驾驶位（ATV 本地） | `(0, 0.9, -0.3)` |
| 乘客位（ATV 本地） | `(0, 0.9, 0.62)` |
| 车体碰撞箱 | `1.1, 1.25, 2.6` |
| 模型坐标系 | Y_UP，单 identity 根骨骼 |
| 模型形式 | 项目自制 12 部件低多边形双座越野摩托 |
| 贴图 | 项目脚本生成 32×8、四种不透明调色板颜色 |

车辆驱动参数、地面碰撞、重力、原生 smooth rise、一格越障规则均保持 v0.2 逻辑，不用船只水面物理。

## 存档与生命周期

- ATV 本体名称、ID 923、实体模板 GUID 和原有组件 GUID 保持不变；旧世界 ATV 可按新增组件模板加载。
- 两名乘员都使用原生 Body 父实体引用和偏移保存；重载后按已保存的前/后座偏移重建角色。
- 驾驶员/乘客临时实体 ID 不写入存档，避免断线后幽灵占用。
- 玩家死亡由原生 `ComponentRider` 下车逻辑处理，座位管理器下一帧释放状态；ATV 删除时原生 Body 会解除全部直接子 Body。
- 玩家断线/实体移除时，Body 父子关系解除；座位管理器不保留已离开的实体引用。

## 状态

| 项目 | 状态 |
|---|---|
| 实际 API 审计 | 已确认 |
| v0.3 源码编译 | 已确认，net472 Release 0 warning / 0 error |
| 规则与资源静态测试 | 已确认，ATV 35 项、目标 Engine 1 bone / 12 meshes、XDB/包守卫通过 |
| Windows 游戏运行 | 待验证（按用户要求不在本机启动游戏） |
| Android 7723 单人 | 待验证 |
| Android 主机驾驶 + 客户端乘客 | 待验证 |
| Android 普通客户端驾驶 | 待验证（v0.22 原生父体同步路径已编译） |

## Android 7723 验收清单

只使用新建的可丢弃世界，并先移除旧 `SCIEPort.netmod`。主机和客户端必须安装 SHA-256
`3818392DD24BEECF834080C56CAD73DC0E147BB29F70B18231630BE04C8AB3AA` 的同一 0.10.0 包。

1. 单人创造栏搜索“SCIE 双人越野摩托”，确认物品栏、手持和世界模型为两轮越野摩托且贴图正常。
2. 放置后先上车，确认位于前座；测试前进、倒车、转向、刹车、一格台阶、碰撞、下车和再次上车。
3. 保存退出并重进，确认车辆、方向和单名乘员状态正常，无 `Database not loaded`、包 ID 或 XDB 错误。
4. Android 设备 A 建房并先上车驾驶；设备 B 加入后上同一辆车，确认进入后座且车辆移动时不抖动、不与车体挤压。
5. B 自由环视并操作移动摇杆，确认不改变车体速度/转向，也不改变 A 的视角；A 的驾驶手感应与单人一致。
6. 分别让 B、A 下车并重新按先后顺序上车，确认释放正确；满两人时让第三名玩家尝试，必须被安全下车。
7. 分别测试乘客死亡、驾驶员死亡、乘客断线、车辆被销毁；确认其余玩家不被卡住，空座可再次使用。
8. 两人乘坐时保存、退出并重进，确认没有幽灵占位；记录是否由游戏本身保存在线玩家骑乘关系。
9. 记录设备、Android 版本、7723 APK 文件哈希、完整首个异常日志和复现步骤。通过前不得把联机状态改为“已确认”。

## v0.13.0 第二乘员延迟下车修复（2026-10-24）

用户 Android 实测确认旧实现无法稳定同时乘坐：第一人上车后，第二人能短暂进入后座，但随后自动下车。
目标程序集 IL 复核确认原生 Rider 每帧把两名稳定乘员都重写到唯一 MountOffset，并在位置偏差持续
0.1 秒时主动下车。共享座位管理器现改为 `UpdateOrder.Locomotion`，在 Rider 重写之后、父/子 Body
物理更新之前，每帧恢复驾驶位和乘客位的独立父体偏移；挂载动画和已经脱离车辆的 Rider 不强制改写。
源码、ABI 顺序、Release 与包检查已确认；Android 双人持续乘坐仍待用 0.13.0 包复测。

## v0.22.1 同车移动、自由瞄准与武器修复（2026-10-24）

用户 Android 反馈证明 v0.13.0 只修复了延迟下车，原生普通步行分支仍会把两名乘员的
Move/Jump/Turn 施加到各自子 Body。v0.22.1 在输入与玩家更新之间缓存主驾驶 Move，随后仅清除
乘员自身移动、把水平观察改走各自 LookOrder；ATV Drive 只读驾驶员缓存。普通客户端乘客继续使用
v0.22 的父车发送脏位抑制，避免把延迟插值回传。挖掘、放置、交互、近战和 Aim 动作完全保留。
环境检查、35 项规则、Release 和包校验已确认；Android 双机连续前进、环视、放块、射箭和下车待验证。
