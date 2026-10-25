# SCIE-Port 全载具联机稳定化审计（v0.25.0 补充）

日期：2026-10-23
目标：Survivalcraft NET `Net_x25.01.17` / Android 7723 / `.NET Framework 4.7.2`

## 范围与结论

原 v0.22.1 审计覆盖当时七种可骑乘实体；v0.25.0 新增双人直升机后，当前共八种：ATV 双人越野摩托、双人快艇、双人越野车、飞艇、火车、车厢、T-34 坦克和双人直升机。离线 ABI、规则测试、Release 构建和包校验已确认；Windows 游戏运行、Android 单机运行及 Android 主机/客户端联机仍为待验证，不能仅凭编译标记兼容。

目标程序集的载具同步不是通用“服务器输入仿真”模型。骑手的 `ComponentPlayerPackage.BodyUpdate` 会在骑乘时同步其父 `ComponentBody`；服务器与客户端的 `ComponentBody.Update` 又根据 `ChildBodies[0]` 是否为本机主玩家选择本地仿真或网络插值。因此本轮采用目标版本的真实原生模型：

- 仅当前设备的 `PlayerData.IsMainPlayer` 且确为驾驶员时运行自定义驱动；
- 驾驶员客户端通过原生父车体 `BodyUpdate` 上送位置、旋转和速度，服务器再由原生玩家更新链转发；
- 观察者与乘客不运行驱动，只插值网络状态；
- 不逐帧传送其他玩家，不由普通客户端直接写其他玩家实体；
- 爆炸炮弹例外：普通客户端绝不创建炮弹，只请求服务器验证并发射。

这可让普通客户端驾驶，但它继承 SCNET x25.01.17 的“骑乘驾驶员拥有父车体状态源”信任边界，不等同于抗作弊的服务器权威车辆。若以后要求服务器完全仿真，必须先阻断或替换原生骑乘 `BodyUpdate`，否则客户端位置包会与服务器物理争用。

## 实际 ABI 证据

| 目标文件 | SHA-256 |
|---|---|
| `Survivalcraft.exe` | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` |
| `Engine.dll` | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` |
| `EntitySystem.dll` | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` |

| 接口/行为 | 取证结果 | 用途 |
|---|---|---|
| `ComponentPlayerPackage.BodyUpdate` | 骑乘时读取父 Body 的 `SendPosition/SendRotation/SendVelocity` | 使用原生车辆状态通道，不新增位置包 |
| `ComponentBody.Update` | Client/Server 均根据 `ChildBodies[0].Player.PlayerData.IsMainPlayer` 选择本地状态或 `NetPosition/NetRotation/NetVelocity` | 双座必须稳定保持驾驶员为首个子 Body |
| `ComponentFrame.PositionChanged/RotationChanged` | 写入 `SendPosition/SendRotation` | 副驾驶插值也可能形成延迟回声，必须抑制 |
| `ComponentBody.Velocity` getter | 变化时写入 `SendVelocity` | 同上 |
| `Client.PlayerData` | 可把收到的客户端包绑定到服务器上的真实玩家 | 坦克开火请求身份校验 |
| `PackageManager.RegisterPackage` / `IPackage` | 原生占用 ID 0–38；本 Mod 使用 240、241 | 座位事件与坦克炮塔/开火协议 |
| `SubsystemProjectiles.FireProjectile` | Client 直接返回 `null`；Local/Server 创建并发送原生 `ProjectilePackage` | 坦克炮弹必须由服务器创建 |

## 双座车辆

ATV、快艇和越野车继续只含一个原生 `ComponentMount`，由 `ComponentTwoSeatManager` 分配驾驶/乘客偏移。服务器或本地世界决定座位，ID 240 只复制离散座位角色。

为适配原生网络权威选择，管理器在所有节点把驾驶员 Body 移到 `m_childBodies[0]`。它在 `UpdateOrder.CreatureModels`（晚于 `UpdateOrder.Body`）运行；若本机主玩家是乘客，会清除父车体本帧由插值产生的 `SendPosition/SendRotation/SendVelocity`，防止副驾驶把延迟状态回传服务器。驾驶员状态不被清除。

v0.22.1 另加两个经过目标 ABI 验证的调度阶段。`-5` 位于 `ComponentInput=-10` 与
`ComponentPlayer=0` 之间：先缓存驾驶位 Move，再抑制两名稳定骑手的个人移动/跳跃，并把水平观察
送入各自 LookOrder；Aim/Dig/Hit/Interact、物品栏与下车控制不改。`Locomotion=1` 位于 Rider 后、
Body 前，每帧恢复驾驶员和乘客的独立父体偏移，避免原生单 Mount 偏移使第二人脱座。三个双座 Drive
都只消费缓存的驾驶输入。该修复沿用上述公开父体同步及回声抑制，不访问私有待发包队列。

## 坦克炮塔与开火

`TankControlPackage` 使用 ID 241：

- 驾驶客户端以 10 Hz 发送炮塔 yaw/pitch；点击开火时附加一次请求；
- 服务器要求 `From.PlayerData.ComponentPlayer` 与包内 Rider ID 一致，并再次验证玩家仍是该坦克唯一 Mount 的 Rider、玩家 Body 的 ParentBody 仍是该坦克；
- 序号采用 16 位回绕安全比较，拒绝重复和旧请求；
- yaw/pitch 在组件内再次归一化/钳制；
- 冷却、浸水限制和炮弹创建在服务器重新验证；
- 服务器使用已有安全炮口、引信与爆炸逻辑创建炮弹，再用原生弹丸/爆炸网络链同步；
- 服务器把炮塔状态转发给其他客户端，开火客户端不在本地伪造爆炸。

## 逐载具状态

| 载具 | 普通客户端驾驶代码 | 特殊同步 | 离线状态 | Android 联机 |
|---|---|---|---|---|
| ATV | 已接入本机驾驶员门控 | 双座首子 Body + 乘客回声抑制 | 已编译/规则通过 | 待验证 |
| 双人快艇 | 已接入本机驾驶员门控 | 同上；伤害/沉船只在 Local/Server 结算 | 已编译/规则通过 | 待验证 |
| 双人越野车 | 已接入本机驾驶员门控 | 双座首子 Body + 乘客回声抑制 | 已编译/规则通过 | 待验证 |
| 飞艇 | 已接入本机驾驶员门控 | 浸水伤害/强制下车只在 Local/Server 结算 | 已编译 | 待验证 |
| 火车 | 已接入本机驾驶员门控 | 驾驶端轨道对齐；无人车只由 Local/Server 模拟 | 已编译 | 待验证 |
| 车厢 | 与火车共用组件 | 同上；仍是独立可驾驶预览，不含编组 | 已编译 | 待验证 |
| T-34 | 已接入本机驾驶员门控 | ID 241 炮塔状态；服务器验证并创建炮弹 | 已编译/规则通过 | 待验证 |
| 双人直升机 | 已接入本机驾驶员门控 | 双座首子 Body + 包 240；旋翼仅本地视觉动画；无人驾驶由 Local/Server 自动降落 | 已编译/规则通过 | 待验证 |

## 已知边界

1. 本轮没有修改游戏本体，也没有 Hook 原生 `ComponentPlayerPackage`；移动同步继承原生客户端驾驶员信任模型，不能宣称抗作弊服务器权威。
2. 包 ID 240/241/242 未与目标原生 0–38 冲突，但 SCNET 没有第三方 Mod 包 ID 注册中心；与其它 Mod 同装仍可能冲突。
3. 火车和车厢尚无编组同步；车厢仍可独立驾驶。
4. 网络延迟、跨区块、驾驶员断线、双座重连、第三人拒绝、直升机自动降落、坦克连续开火和爆炸破坏尚未在两台 Android 设备上验证。
5. 7723 重打包 APK 的程序集哈希仍需从设备/安装包复核；Windows 同标签哈希不能替代 Android 证据。

## Android 双机验收

主机与客户端必须安装完全相同 SHA-256 的 `SCIEPort.netmod`，使用可丢弃的新世界并保留完整日志。

1. 每种载具分别让房主驾驶 2 分钟，再让普通客户端驾驶 2 分钟；双方观察前进、倒车、转向、制动、碰撞、上下坡和跨区块是否一致，无持续回弹或抖动。
2. ATV、快艇、越野车分别测试“房主驾驶+客户端乘客”和“客户端驾驶+房主乘客”；乘客摇杆不得控制车辆，环视互不影响。
3. 双座满员后第三人上车必须被安全拒绝；依次测试驾驶员/乘客下车、死亡、断线和重连，座位可再次占用且无幽灵乘员。
4. 飞艇验证客户端驾驶时升降、转向、存档重进和浸水下车；两端高度与速度不能持续分叉。
5. 火车/车厢在直轨、弯轨、坡轨、区块边界验证对齐；不能穿轨、瞬移或由观察端抢夺位置。
6. 坦克由普通客户端驾驶并连续转炮塔，房主观察炮塔方向；分别由房主/客户端开炮，确认第一炮不炸车、只有一枚炮弹、爆炸和方块破坏两端一致、冷却不能绕过。
7. 每种载具在有人和无人两种状态保存退出重进；确认实体、姿态、炮塔和座位均无重复/丢失。
8. 任一失败记录首个异常、双方设备/Android 版本、7723 APK 哈希、Mod 哈希、房主/客户端角色和最小复现步骤；未通过的行继续保持“待验证”。

直升机另需在第 4 步之后执行：房主/客户端互换驾驶与乘坐，双方观察启动、升降、悬停、前后、转向、最大高度和降落；Passenger 的摇杆/Jump/Sneak 均不得影响机体；驾驶员空中离开后两端必须观察到同一次安全自动降落。旋翼相位不是权威状态，只要求两端持续动画且不影响位置/座位一致性。
