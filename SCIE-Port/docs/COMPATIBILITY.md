# SCIE-Port 兼容性记录

## 0.29.0 便携堡垒（资产/离线规则/打包已确认，Android 真机待验证，2026-10-26）

Portable Fortress 由 Builder Assistant 正式迁入 SCIE-Port，成为独立玩法；`SCIEPort.netmod` 单独即可提供全部功能，不要求安装 `BuilderAssistant.netmod`。

| 项目 | 状态 | 结论 |
|---|---|---|
| 归属迁移 | 已确认（仓库） | 模型、设计规格、生成器、导入校验器全部位于 `SCIE-Port/`；源码与打包不再引用 `src/BuilderAssistant/`。旧副本保留仅作回滚参照，不参与编译。 |
| 模型与目标导入 | 已确认（目标程序集） | `Assets/Models/PortableFortress.dae`：Y_UP、单位 1、单 `PortableFortress` 节点、identity 矩阵、1 骨骼 / 11 网格 / 1248 三角面、包围盒 X `-5.5..5.5`、Y `0..9`、Z `-6.5..6.5`；真实 x25.01.17 `Engine.Media.ModelData.Load(Stream)` 返回 1/11。 |
| 几何变更 | 已确认（源码/生成器） | 唯一改动：原白模把 LAYOUT 预留的屋顶通道体积填成实心盒子，导致预留爬升体积不可通行；现改为 10 根横档 + 2 根侧轨，其余部件、外壳与程序不变。 |
| 方块注册 | 已确认（源码/CSV/构建守卫） | 944 展开核心（物品）、945 控制核心（无碰撞、隐藏于创造栏）、946 隐藏地基（碰撞 0..0.36、不可手放）。编号经全仓 CSV 与构建守卫校验，未与 903–943 / 995–1000 冲突。 |
| 碰撞架构 | 已确认（反编译证据 + 离线断言） | `ComponentBody.HandleSmoothRise` 只消费地形碰撞盒，实体代理无法被踩上；`FindBodiesCollisionBoxes` 以 `FindBodiesAroundPoint(4f)` 找体，故代理须小。地板/甲板/屋顶/外墙/护栏用分段代理（半对角线 ≤ 4），入口 0.36 台阶用 3 格隐藏地形，楼梯与屋顶竖井用有界垂直抬升辅助。 |
| 开口完整性 | 已确认（离线断言） | 后门洞、两侧观察窗带、前观察窗、楼梯井、屋顶舱口、屋顶通道柱逐项断言未被代理封死。 |
| 部署事务 | 已确认（离线断言） | 全 143 列 × 9 格预检；任何非空气方块、无实地、区块未就绪、玩家/生物重叠、高度越界、模型缺失、实例重复一律拒绝且不消耗道具；地形写入 4 格、写前捕获、失败逆序回滚；代理每 tick 最多 8 个分批创建，全部成功才置 `Deployed` 并消耗。 |
| 存读档 | 已确认（代码边界） | 锚点持久化实例 ID/朝向/原点/核心格/修订/状态，代理持久化实例 ID + 段索引；读档时由组件 `Load` 重建注册表，每 tick 校正漂移。真实存读档行为待真机。 |
| 回收 | 预留（不支持） | 只判定不拆除；已部署实例点击核心回复"回收接口已预留，本轮不执行拆除"，避免吞物资或重复返还。 |
| Release 构建/包 | 已确认 | `Build-NetMod.ps1 -GameDir` 指向本机 x25.01.17 目录、Release：exit 0；全部离线套件全绿（堡垒 291 断言），net472 0 warning/0 error，15 个 XDB `DATABASE VALIDATION PASSED`；`.netmod` **2,323,919 字节 / 87 条目**，SHA-256 `89FCCE29A77224F98D915D3D645DF6BEDBA3D80903964296DF008CA54C4159CB`，modinfo 0.29.0。 |
| Android 7723 渲染/行走 | 待验证 | 未真机运行：模型比例朝向、0.36 地板不浮空、楼梯与竖井抬升手感、护栏、窗洞视野、帧耗时全部未测。 |
| Android 7723 存读档 | 待验证 | 退出重进后模型、代理碰撞、控制核心是否保留未测。 |
| 联机 | 待验证 | 普通客户端拒绝展开已由代码保证；房主侧展开、同步与非装 Mod 客户端可见性未测。 |
| 储物 48 格、供暖、玻璃透明 | 不支持 | 数据结构与 API 路径已审计，本轮未实现；白模无玻璃材质。 |

## 0.26.0 双人直升机 V0.4（表现升级已编译，Android/双机待验证，2026-10-25）

本阶段只在 0.25.0 可验证直升机核心上叠加模型、旋翼状态/模糊、视觉姿态与音效。仓库没有可定位的 V0.3 提交，故未虚构 V0.3 基线；实际保留范围和源码证据见 `HELICOPTER-V0.4-AUDIT.md`。

| 项目 | 状态 | 结论 |
|---|---|---|
| V0.1 核心兼容 | 已确认（差异/编译） | ID 941、实体/组件 GUID、单 Mount 双座、包 240、驾驶员唯一输入、飞行参数/物理、碰撞体、生成和保存路径未改；原 Update 中物理段只抽成等价 `SimulateFlight`。乘客仍不进入控制路径。 |
| 模型/枢轴 | 已确认（目标导入） | clean-room Y_UP DAE 为 758 triangles、6 bones/5 merged meshes；真实 x25.01.17 `ModelData.Load(Stream)` 导入结果正是 6/5，低于七骨骼预算。VisualRoot 在座舱中央，主旋翼世界枢轴 `(0,1.80,-0.30)` 绕 Y，尾桨 `(0.30,1.20,2.37)` 绕 X。 |
| 材质与物品栏 | 已确认（资源/构建） | 96×16 全不透明十二色调色板；机体/玻璃/金属/橡胶/座椅/仪表/灯/blur 分色。物品栏复用同一 DAE/PNG 并隐藏 blur 网格。透明玻璃因未验证逐材质排序而降级为深色不透明。 |
| 动画状态与姿态 | 已确认（代码/41 规则断言） | Stopped/Starting/Running/Stopping 从既有状态派生；动画按 dt、角度完整回绕。VisualRoot 的俯仰/侧倾有上限和指数阻尼，只改骨骼，不改 Body 权威位置/碰撞/物理。solid/blur 是预建网格，无逐帧网格分配。 |
| 音效 ABI/资源 | 已确认（真实程序集/目标解码） | 反射确认 `SubsystemAudio.CreateSound/CalculateListenerDistance/CalculateVolume` 与 Sound 生命周期属性/方法；项目生成 WAV 被目标 `SoundData.Load` 解码为 mono PCM16、22050 Hz、22050 样本。Load 只建一个循环声源，Dispose 显式 Stop+Dispose。Android OpenAL 实播仍待验证。 |
| 联机影响 | 已确认（代码边界）；真机待验证 | 未新增位置/动画/音频包或旋翼实体；继续使用原生父 Body 状态与包 240。远端表现从既有 Body/座位状态派生。两台 Android 对位置、座位、运动、姿态、旋翼、音频及重连仍未执行，不能写成联机通过。 |
| Release 构建/包 | 已确认 | `Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release` exit 0；全部离线套件通过，net472 0 warning/0 error，12 个 XDB 通过。`.netmod` **520,065 字节 / 64 条目**，SHA-256 `23060B72D8D959265E32B8F3A9A86C73F6977CE4201575FDD7CE1276D5177DD1`；modinfo 0.26.0，DLL 0.26.0.0。 |
| Windows 游戏运行 | 待验证 | 未启动目标游戏；创造栏、世界渲染、起飞、声音和保存重载未运行。 |
| Android 7723 单机 | 待验证 | 创造栏/生成、触屏飞行、旋翼与姿态、音效、昼夜、切世界/重进、性能均需新建可丢弃世界实测。 |
| 两台真实 Android 客户端 | 待验证 | 必须以相同最终包哈希验证房主/客户端角色互换、位置/座位/运动、乘客不可控、上下机、断线重连和远端表现。 |
| 严格服务器权威 | 不支持 | 沿用 x25.01.17 驾驶员客户端父 Body 状态源；V0.4 未替换原生协议。 |

## 0.25.0 双人直升机 V0.1（最小实现已编译，Android/双机待验证，2026-10-25）

本阶段按 `HELICOPTER-V0.1-AUDIT.md` 的边界新增独立 `Features/AirVehicles` 模块，没有复制越野车或坦克的接地物理，也没有修改已有载具 ID、实体 GUID 或存档。直升机物品使用仓库内未占用 ID **941**，实体模板为 `SCIEPortHelicopter`；外部 Mod 同装冲突仍需安装时检查。

| 项目 | 状态 | 结论 |
|---|---|---|
| 物品/实体注册 | 已确认（代码/数据库/包） | 新增 `HelicopterBlock`、CSV、`SubsystemHelicopterBlockBehavior` 与独立 XDB 实体；生成只允许 Local/Server，并在生成、扣除物品前经过统一载具激活闸。数据库验证通过，未写世界存档或覆盖既有 ID/GUID。创造栏实际可见与生成仍属 Android 运行项。 |
| 双座与权限 | 已确认（代码/规则） | 只使用一个原生 `ComponentMount` 和一份 `ComponentTwoSeatManager` 唯一状态；Driver/Passenger 分配继续使用包 ID 240。共享层只缓存 Driver 的 Move/Jump/Sneak，Drive 再校验本机主玩家正是 Driver；Passenger 不进入控制路径。真实上下机、第三人拒绝及断线重连待真机。 |
| 飞行规则 | 已确认（代码/离线测试） | 独立 `HelicopterFlightRules` 显式实现重力、旋翼升力、垂直响应、水平加减速/阻尼、前后限速、高度上限和机舱边界；28 个无游戏依赖断言通过。参数由 XDB 读取，不与地面载具规则耦合。 |
| 启动/升降/悬停/转向/降落 | 已确认（代码路径） | 驾驶员入座自动启动并渐进加速旋翼；Jump 上升、Sneak 下降、Move.Z 前后、Move.X yaw；中立输入抵消配置重力并把速度收敛到悬停。驾驶员离开后 Local/Server 有界自动降落，接地后停机；Passenger 仍不能接管。触屏映射手感和实飞结果待 Android。 |
| 碰撞/保存/安全副作用 | 已确认（结构/编译） | Drive 只写 `ComponentBody.Velocity/Rotation`，不逐帧传送 Position；地形/实体碰撞、父子 Body 与实体保存沿用目标版本原生 Body/Spawn。深水伤害和强制下车只在 Local/Server 结算。碰撞体只包机身，不包旋翼圆盘；实际碰撞和保存重载待运行。 |
| 模型与动态部件 | 已确认（目标导入） | clean-room `Y_UP` DAE 只有 `HelicopterRoot/MainRotor/TailRotor` 三骨骼、17 个立方体网格，低于目标渲染器七骨骼上限；目标 `Engine.Media.ModelData.Load` 实际导入为 3 bones/17 meshes。主旋翼绕 Y、尾桨绕 X 用已验证的 `ComponentModel.SetBoneTransform` 动画；Android 世界渲染待验证。 |
| 配置与可复现资源 | 已确认 | XDB 暴露旋翼加减速、重力、垂直响应、升降/自动降落速度、水平加速/制动/阻尼/限速、横移、转向和最大高度；`New-HelicopterAssets.ps1` 可重建三骨骼模型与 48×8、全不透明六色贴图。 |
| Release 构建/包 | 已确认 | `Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release` exit 0；全部离线套件通过，0 warning/0 error，12 个 XDB 数据库验证通过。最终 `.netmod` **506,514 字节 / 63 条目**，SHA-256 `C424E021AD2DA65E255FD796EA77B47E0E2E7C7F5BEE5856CDF6833BB35F1736`；包内 modinfo Version=`0.25.0`，DLL AssemblyVersion/FileVersion=`0.25.0.0`。 |
| Android 7723 单机验收 | 待验证 | 创造栏查找/生成、启动、起飞、升降、悬停、前后/转向、降落、安全上下机、双座、碰撞及退出重进均未在目标手机执行。 |
| 两台真实客户端联机 | 待验证 | 代码复用 x25.01.17 原生骑乘父 Body 位置/旋转/速度通道和包 240 的离散座位角色；未用两台 Android 验证位置、座位、运动、角色互换、断线重连，不能声称联机已实现。 |
| 严格服务器权威飞行 | 不支持 | 移动继承目标版本的驾驶员客户端父 Body 状态源；V0.1 未 Hook 或替换游戏本体同步协议，不具备抗作弊服务器物理。 |

## 0.24.0 载具激活密码闸（统一激活码，主机校验的按会话授权；编译/离线已确认，Android 弹窗与联机授权待验证，2026-10-24）

在首次对任意 SCIE 载具进行生成/驾驶/乘坐/开火前插入一道触屏密码激活闸：统一激活码 `123456`，成功后整个游戏会话解锁全部七种载具（ATV 摩托、蒸汽快艇、越野车、飞艇、火车、车厢、T-34 坦克），失败或取消则拒绝该操作且**不消耗任何物品**。授权对主机与客户端都要求、按真实玩家会话隔离、由主机校验，并覆盖已在场的载具与坦克开火入口以防绕过。本阶段不改动物品 ID、实体 GUID、旧存档、物理与既有网络协议。

| 项目 | 状态 | 结论 |
|---|---|---|
| UI 弹窗 ABI 取证 | 已确认（真实程序集反射） | 反射 x25.01.17 `Engine.dll` 取到 `Engine.Input.Keyboard.ShowKeyboard(String,String,String,Boolean,Action<string>,Action)`：`public static`、六参、第四参 `passwordMode=true` 遮蔽输入——这是原版告示牌编辑同款的触屏文本输入面，满足「不臆造、可用触屏弹出密码框」的前提。`Test-Environment.ps1` 以结构化类型比对（非脆弱的 FullName 字符串比对）逐参断言该签名存在。 |
| 节流时钟 ABI | 已确认（反射） | `Engine.Time.RealTime` 是 `Engine.Time` 类型上的 `public static double` **属性**（非独立类型），用作弹窗节流时基；`VehicleActivationRules.ShouldPrompt` 以 `PromptInterval=6.0` 秒限制重复弹窗，避免驾驶循环每帧弹。 |
| 网络包 ABI | 已确认（反射/编译） | 原生 `IPackage` ID 占用 0..38 连续，SCIE-Port 已用 240（双座分配）、241（坦克控制），**242** 空闲→新 `VehicleActivationPackage` 取 242；`Test-Environment.ps1` 保留段扩为 `@(240,241,242)` 并断言与原生无冲突。`PackageManager.RegisterPackage(IPackage)` 为 `public static`；`Client.GUID`、`NetNode.Server`、`NetNode.QueuePackage(IPackage)` 均反射确认存在。 |
| 包载荷读写 | 已确认（继承契约） | `PackageStreamWriter : System.IO.BinaryWriter`、`PackageStreamReader : System.IO.BinaryReader`；`Write(string)` 与 `ReadString()` 由基类提供。环境脚本改为断言基类继承+基类方法存在（派生类型上枚举 `GetMethods` 会因某未加载 `OpenTK` 重载解析失败），精确反映代码实际调用面。 |
| 授权模型 | 已确认（代码/规则） | 会话键：每台机器仅一个本地操作玩家，本地会话以全零 `Guid.Empty` 为键；主机额外按 `Client.GUID` 记录每个远程授权。客户端**绝不自我授权**——输入码经 242 包发给主机，主机用 `VehicleActivationRules` 校验、按 `From.GUID` 记账、再**单播**（`To=From`）回执，其它客户端不因广播解锁；客户端仅在主机的肯定回执后把本地 `Guid.Empty` 会话置为已授权。 |
| 入口覆盖（七载具） | 已确认（代码/构建守卫） | 六处生成 `OnUse`（ATV/越野/蒸汽船/飞艇/轨道车+车厢/坦克）在 `WorkType.Client` 早退之后、射线/生成/`RemoveActiveTool` 之前统一插 `VehicleActivationService.EnsureActivated()` 拒绝闸，未授权先拒后不消耗物品；六处驾驶组件（`ComponentAtvDrive/OffroadCarDrive/SteamBoatDrive/AirshipDrive/RailVehicle/TankDrive`）在主玩家判定内插同一闸，覆盖**已在场**载具；坦克开火入口在 `bool wantsFire = TankRules.CanFire` 之前置闸。`Build-NetMod.ps1` 对每处源码断言闸存在、生成闸先于物品消耗、坦克驾驶闸先于开火路径。 |
| 远程开火/控制授权 | 已确认（代码/构建守卫） | `TankControlPackage`（241）服务端分支在既有 `sender.Entity.EntityId == RiderId` 之后追加 `VehicleActivationService.IsGranted(From.GUID)` 门，未授权远程玩家的控制/开火请求被丢弃；`Build-NetMod.ps1` 断言该字面门存在。 |
| 失败安全 | 已确认（代码） | 弹窗调用包在 try/catch：x25.01.17 可编译但 7723 实机行为未观测，抛出异常时保持「拒绝」态而非崩溃驾驶/生成循环，节流窗口后重试。取消即无操作、保持拒绝。 |
| 纯决策（离线可测） | 已确认（net8 无游戏依赖） | `tests/SCIEPort.VehicleActivation.Tests` 22 断言：`IsValidCode`（含 trim/null/空/纯空白/反序）、`ShouldPrompt`（首次/大于/等于/小于间隔、零经过）、`PromptInterval==6.0`、`ShouldGrantActivation`、`CanOperate`、`IsUsableSessionKey`、`ActivationCode=="123456"`。随全工程 12 个离线测试套件在打包前全绿。 |
| 编译产物 | 已确认（Release 0/0） | `Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release` 全程 exit 0（`TreatWarningsAsErrors`+`GenerateDocumentationFile`→0 warning/0 error，`DATABASE VALIDATION PASSED`），打包前修复一处命名空间缺漏（服务类需 `using Engine;`+`using Game.NetWork;` 以解析 `Log`/`CommonLib`/`WorkType`/`VehicleActivationPackage`）。产物 `.netmod` **498,017 字节**，SHA-256 `C40A9C5EF5FE46C8578E4C079E7A461DEB4FBC6F9F96925885AE95D7E4D69213`；包内 `modinfo.json` Version=`0.24.0`、`SCIEPort.dll` AssemblyVersion=`0.24.0.0`。 |
| Windows 游戏运行 | 待验证 | 本机有可运行 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测：弹窗实际出现与遮蔽输入、输入正确/错误码的放行/拒绝、取消、已在场载具拦截、坦克开火拦截均未运行。 |
| Android 7723 | 待验证 | 目标平台真机的 `Keyboard.ShowKeyboard` 弹窗可达性、密码遮蔽渲染、触屏输入与节流手感均未观测，不得因编译或反射签名匹配标记为兼容；弹窗失败回退为拒绝（安全），但可用性待实测。 |
| 多人联机授权 | 待验证 | 主机校验+按 `Client.GUID` 隔离+单播回执为代码/ABI 层结论；真实主机+普通客户端两台下的授权隔离、越权拒绝、未授权远程开火被丢弃均需双机实测。 |
| 无越界改动 | 已确认 | 本阶段严格限定在 SCIE-Port 内：新增激活规则/服务/包三类、七载具入口插闸、`TankControlPackage` 增授权门、加载器注册 242、构建/环境守卫与离线测试，未动 Builder Assistant、未改物品 ID/实体 GUID/存档格式/物理/既有网络协议载荷。 |

## 0.22.2 坦克发射后坐、炮管限位、全距离触炸与有限耐久（2026-10-24）

| 项目 | 状态 | 结论 |
|---|---|---|
| 发射帧后坐 | 已确认（代码/规则/编译） | 根因是旧后坐先于同帧驾驶积分，随后被油门/抓地重写；现改为驾驶积分完成后、成功发射当帧施加反炮口方向水平速度 `1.25`。普通客户端请求开火时做即时本地预测，炮弹仍只由服务器创建 |
| 炮管机械限位 | 已确认（代码/规则） | 视觉骨骼、网络状态与弹道共用 `GunPitch`，限制为下俯 5°、上仰 25°；Android 视觉方向待验证 |
| 全距离首次命中爆炸 | 已确认（代码/规则/编译） | 取消 1.5 格解锁与 12 格发射车安全包络；独立 `OnHitAsProjectile` 首次地形/实体碰撞无论距离均爆炸。寿命结束、卸载等非命中移除只清理，不爆炸 |
| 有限装甲耐久 | 已确认（目标内容/代码/包） | 原版 Damage 模板实测声明 `Hitpoints` 范围 0..1；坦克使用 `Hitpoints=1`、`AttackResilience=600`，归零走原生残骸/实体移除，剩余值走原生保存链 |
| 不可点燃 | 已确认（模板/包） | 坦克不含 `ComponentOnFire`，构建守卫禁止加入；近爆仍可造成正常伤害和冲量 |
| Windows 游戏运行 | 待验证 | 未启动目标游戏 |
| Android 7723 单机/联机 | 待验证 | 发射瞬间后退、五向炮管/弹道、贴墙与远距单次爆炸、耐久保存/归零报废、火免及服务器开火一致性均需实机验证 |

## 0.22.1 双座同行、自由观察与乘客武器（2026-10-24）

| 项目 | 状态 | 结论 |
|---|---|---|
| 输入调度 ABI | 已确认 | 固定 x25.01.17 上 `ComponentInput=-10`、新增重定向 `-5`、`ComponentPlayer=0`；公开 `PlayerInput` 字段与 `ComponentLocomotion.LookOrder` 经环境脚本反射通过 |
| ATV/快艇/越野车驾驶输入 | 已确认（代码/编译） | 重定向阶段先缓存驾驶位 Move，三个 Drive 均只读该缓存；随后只清除两名稳定骑手的个人 Move/SneakMove/VrMove/Jump，避免骑手步行物理与车辆争用 |
| 双座位置稳定 | 已确认（代码/规则） | `Locomotion=1` 辅助更新在 Rider `Default=0` 后恢复两个独立 `ParentBodyPositionOffset/RotationOffset`；动画中或已脱离车辆者不强制回座 |
| 自由观察、攻击与方块使用 | 已确认（代码/ABI） | 水平 Look 转入各自 `LookOrder`；Aim/Dig/Hit/Interact、物品栏和下车输入不修改。实际触屏手感与弓箭/方块行为待 Android 验证 |
| v0.22 联机回声保护 | 已确认（代码/编译） | 保留驾驶员首子 Body 与 `CreatureModels` 阶段的普通客户端乘客父车发送脏位抑制，不使用私有网络队列反射 |
| Windows 游戏运行 | 待验证 | 未启动目标游戏 |
| Android 7723 单机/主机/普通客户端 | 待验证 | 双人连续同行、角色互换、环视、射箭、放块、下车与断线重连必须双机实测 |

## 0.22.0 全载具联机稳定化（2026-10-23）

| 项目 | 状态 | 结论 |
|---|---|---|
| x25.01.17 原生骑乘同步 ABI | 已确认 | `ComponentPlayerPackage.BodyUpdate` 同步骑手父 Body；`ComponentBody.Update` 以 `ChildBodies[0]` 的本机主玩家身份选择仿真/插值。程序集哈希未变，详见 `VEHICLE-MULTIPLAYER-AUDIT.md` |
| ATV/快艇/越野车普通客户端驾驶 | 已确认（代码/编译） | 驱动仅由本机驾驶位 `IsMainPlayer` 执行；驾驶员固定为首个子 Body，副驾驶客户端在 Body 更新后抑制父车体延迟回声。Android 双机待验证 |
| 飞艇/火车/车厢普通客户端驾驶 | 已确认（代码/编译） | 移除 Local-only 驱动门；观察者不运行驱动。飞艇伤害只由 Local/Server 结算；轨道无人车只由 Local/Server 模拟。Android 双机待验证 |
| 坦克普通客户端驾驶 | 已确认（代码/编译） | 车体沿用原生父 Body 同步；炮塔 ID 241 包由服务器绑定发送者/骑手/车体并检查回绕序号，炮弹仅在服务器创建。Android 双机待验证 |
| 包 ID | 已确认（目标 Windows 程序集） | 原生包为 0–38；SCIE-Port 座位 240、坦克控制 241 均未冲突。第三方 Mod 冲突仍待同装检查 |
| Windows 游戏运行 | 待验证 | 按用户约束未启动游戏 |
| Android 7723 单机/主机/普通客户端 | 待验证 | 编译与离线规则不能证明真机同步；须执行双机矩阵 |
| 严格服务器权威/抗作弊 | 不支持 | 移动继承 x25.01.17 原生“驾驶员客户端同步父车体”模型；未修改或 Hook 游戏本体 |

目标：Survivalcraft NET 2.4 `Net_x25.01.17`，Android 为最终平台。

本表只使用“已确认 / 待验证 / 不支持”。编译和 Windows 程序集取证不能替代游戏运行、
Android 或多人测试。

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| Windows 目标程序集 | 已确认 | `Survivalcraft.exe` 2.4.0.0；固定哈希见 `API-DIFF.md` |
| 目标框架 | 已确认 | `.NETFramework,Version=v4.7.2` |
| SCNET ModLoader 与 CSV | 已确认 | 对真实程序集编译通过；运行加载待验证 |
| 物品 ID 995–1000 | 已确认 | 反射扫描目标原生常量未占用；其他 Mod 冲突仍待运行检查 |
| Kelly 植物 ID 903–916 | 已确认 | `Test-Environment.ps1` 对 x25.01.17 的 260 个原生方块常量完成反射扫描，目标区间未占用；其他 Mod 冲突仍需安装时检查 |
| XDB 合并格式 | 已确认（机理） | `ModsManager.CombineDataBase` 按 `LocalName + Guid` 匹配文件夹后再合并内容。**真正的判别项是文件夹 GUID 是否等于游戏原版值**：原版文件夹（Entities `5fcdf35f…`、Gameplay `d3d4b692…`、BlockBehaviors `00c97f0f…`）若使用自定义 GUID，合并不会并入原版文件夹，而是追加“同名不同 GUID”的游离 `<Folder>`（0.3.1 即此）。根节点用 Mod 容器（`<SCIEPortWatercraft>`，0.3.2 采用）还是 `<DatabaseObjects>`（正常运行的 Builder Assistant 采用）本身都可用——“根节点导致嵌套 `DatabaseObjects` 才报错”属早期误诊，已推翻。`scripts/Test-Database.ps1` 现强制校验该规则。**已用真实原版 `Assets/Database.xml`（解自 `Content.scpak`）+ 游戏真实 `DatabaseManager.LoadDataBaseFromXml` 端到端复现**：0.3.1 的确切首异常是 `Another database object with name "Entities" is already nested in database object "Root".`（`DatabaseObject.set_NestingParent`），0.3.2 与阳性对照 Builder Assistant 均加载成功。 |
| Boat/Mount/Rider/SpawnCreature ABI | 已确认 | `Test-Environment.ps1` 逐项反射签名通过 |
| 双人快艇推进代码 | 已确认（编译/规则） | 不再挂原生 `ComponentBoat`；v0.22.1 缓存本机驾驶位输入并隔离两名骑手个人移动，`ComponentBody` 保留浮力、阻力、碰撞和持久化。Windows/Android 运行待验证 |
| Windows 游戏加载与驾驶 | 待验证 | 尚未启动目标游戏进行生成、骑乘与存档测试 |
| Android 7723 数据库加载 | 已确认 | 用户确认 0.3.1 已修复 `Database not loaded`；渠道程序集哈希仍待取得 |
| 双人快艇联机 | 待验证 | v0.22 接入本机驾驶员的原生父体同步，v0.22.1 隔离乘客输入并维持独立座位；主机/普通客户端角色互换仍须 Android 双机验证 |
| SCIE 燃料/发动机库存 | 不支持 | 0.5.0 未实现，不能以占位代码宣称兼容 |
| 世界加载（进入世界） | 已确认（桌面真实反序列化 + 手机实测） | `ModsManager.CombineDataBase` 按原版文件夹 GUID 合并；用真实原版 `Assets/Database.xml` + 真实 `DatabaseManager.LoadDataBaseFromXml` 端到端证明修复后无 rogue `Entities`，且手机反馈“经过测试可以了”，能进世界不再报 `Database not loaded`。 |
| SCIE 蒸汽船纹理 | 待验证 | 64×64 PNG，上游授权文件哈希已确认；游戏渲染待验证 |
| SCIE 蒸汽船模型朝向（Y_UP） | 已确认（文件/打包级）/ Android 待实测 | SCNET 的 `SimpleModel` 按游戏约定直接读原始顶点坐标，Y=上、Z=前，故 `.dae` 必须 `up_axis=Y_UP`。上游授权 `SteamBoat.dae` 是 Blender 导出的 `Z_UP`（高度放在 Z），进世界后船竖直立着；对照能正常横放的 Builder Assistant `Speedboat.dae` 是手写 `Y_UP`。0.3.3 改为打包**项目内派生的修正副本** `src/SCIEPort/Assets/Models/SteamBoat.dae`：对 positions(98 顶点) 与 normals(135) 烘焙绕 X −90° 旋转 `(x,y,z)->(x,z,-y)`（保持右手系），`up_axis` 改 `Y_UP`，`Cylinder` 节点与顶点数不变；**上游源文件保持不动**（仅作授权溯源）。`Build-NetMod.ps1` 打包级强制校验 `up_axis=Y_UP`+294 float。手机里船是否真正平放仍须实机确认，不提前宣称已修复。 |
| 双人快艇尺寸与座位 | 已确认（静态/目标导入） | 授权模型原比例边界约 X±1.058、Y 0..1.694、Z -2.353..2.743；Body 为 2.3×1.75×5.6。驾驶/乘客偏移为 (-0.42,1,-0.45)/(0.42,1,-0.45)，侧向下船点 X=1.65。目标 Engine 导入必须为 1 bone/1 mesh；Android 乘坐观感待验证 |
| 0.5.0 飞艇朝向与骑乘缺陷 | 已确认 | 用户在 Android 实测飞艇竖直且无法骑乘；源模型为 Z_UP，同时 9 格宽碰撞体使驾驶座超出原生 2.5 格骑乘搜索范围 |
| 0.5.1 飞艇朝向与座位修复 | 已确认（文件/编译级） | 派生模型为 Y_UP、926 顶点，底面归零；碰撞体改为 `3,2,6`、座位 `0,1,-0.8`、侧向下船 `1.75,0.5,0`；Android 复测待验证 |
| SCIE 铁轨、火车和车厢编译/资源 | 已确认 | 0.5.1 的 RailBlock、形状更新、两实体、模型节点和纹理通过 Release 与包检查；火车已由 Z_UP 转为 Y_UP |
| 其他载具坐标轴 | 已确认（文件级） | 蒸汽船 Y_UP；火车 0.5.1 Y_UP；车厢源模型本身 Y_UP。三者实际 Android 姿态仍待验证 |
| SCIE 铁轨和轨道载具运行 | 待验证 | 未在 Windows/Android 验证放置、弯坡轨、生成、骑乘、驾驶和存档 |
| 车厢自动编组和货箱 | 不支持 | 旧实现依赖延迟父体复制及旧 GUI/库存链；0.5.0 仅提供独立试乘 |
| 轨道载具多人同步 | 不支持 | 自定义轨道物理仅在 `WorkType.Local` 执行 |
| Kelly 植物代码与资源 | 已确认（编译/静态） | 0.6.0 的 14 个方块类、4 个行为模板、原图集与果实模型已迁入 `SCIE-Port`；net472 对目标程序集编译通过，资源哈希与授权来源记录一致 |
| Kelly 创造栏原名 | 已确认（静态） | CSV 严格为野莓植株、野莓、香蒲、蒲菜、云莓植株、云莓、野生蘑菇簇、野生蘑菇、毒蝇伞簇、毒蝇伞、大豆、黄豆、仙人掌果、橡子；构建脚本按顺序哈希并拒绝中英文括号 |
| Kelly XDB 合并 | 已确认（静态） | `KellyPlants.xdb` 复用原版 `BlockBehaviors` GUID，4 个模板与 4 个成员继承均由 `Test-Database.ps1` 解析通过 |
| Kelly Android 资源加载 | 已确认（缺陷） | 用户 Android 日志确认 0.7.0 在创造栏绘制 `KellyFruitBlock` 时无法将仅打包的 `KellyExpansion.webp` 解析为 `Texture2D`，导致退出世界 |
| Kelly PNG 修复 | 已确认（文件/打包级） | 0.7.1 将同一 512×512 RGBA 图集无缩放转为 PNG，运行包只含 `KellyExpansion.png`；Android 修复结果待验证 |
| Kelly 其余运行行为 | 待验证 | `.dae`、创造栏完整显示、播种/成长、生态生成、雨后蘑菇、存读档和性能仍需 Android 实测 |
| 飞艇世界贴图 | 已确认（机理/修复） | 0.7.1 中 `AirshipDrive.Load` 先写运行时白贴图、随后 `SimpleModel.Load` 以空 XDB 值覆盖为 null；0.7.2 改为 XDB 明确引用包内 `Textures/Airship.png` |
| 载具世界黑色保底 | 已确认（编译/静态） | 蒸汽船、飞艇、火车和车厢的实体模型均设白色漫反射与低强度自发光，避免世界地形采样光接近 0 时渲染为纯黑；Android 实机待验证 |
| 载具资源引用守卫 | 已确认（打包级） | 构建检查 Watercraft XDB 的每个自有模型/贴图都在包内；仅车厢 `Textures/Creatures/Jaguar` 明确为 x25 原版资源；拒绝任意非 PNG 的 Mod 贴图 |
| ATV 物品与 ID 923 | 已确认 | `Test-Environment.ps1` 对固定 x25.01.17 的 260 个原版方块常量完成扫描；923 未占用且与本工程 903–922、995–1000 无内部冲突。其他 Mod 的 ID 冲突仍待安装时检查 |
| ATV 实体/XDB 合并 | 已确认 | `SCIEPortATV` 保持实体名、原 GUID、唯一原生 Mount、Body、Spawn、Damage 和 Drive，并新增座位管理组件；`AutoDespawn=False` 继续依赖原生实体保存链 |
| ATV 地面物理 | 已确认 | v0.22.1 由本机主驾驶缓存读取触屏 Move；乘客输入不进入 Drive。`ComponentBody` 继续负责碰撞、重力和 1.05 格 smooth rise |
| ATV 模型与贴图 | 已被 v0.23 取代 | 历史结论：项目创建的 `ATV.dae` 曾为 12 部件双座越野摩托、32×8 四色不透明 `ATV.png`。v0.23 起以「荒野探索者 X1」13 部件低多边形模型与 64×64 材质图集取代，见下条 |
| ATV Windows/Android/联机运行 | 待验证 | 旧 0.8.3 模型显示结论不自动覆盖 v0.3 新模型。Windows 未运行；Android 单人、保存重载、主机/客户端互换驾驶与乘客、独立观察及上下车均待验证 |
| ATV v0.2 模型/贴图重建 | 已确认 | 九个独立 Y_UP geometry，identity 矩阵、法线、UV、12 triangles/part；32×8 PNG 为四种不透明颜色；可见范围由构建脚本验证完全落入 `1.7,1.15,2.1` Body |
| ATV v0.2.1 Android 世界模型空白 | 已确认（缺陷/机理） | 用户在 Android 7723 确认 0.8.2 物品栏图标可见、生成实体无模型；目标 `ModLoader.GetMaxInstancesCount()` 返回 7，而旧 DAE 经目标 `Engine.Media.ModelData.Load` 导入为 10 bones/9 meshes。物品栏将网格烘焙进 `BlockMesh`，世界实体则把 10 个骨骼变换交给实例模型着色器，因此两条路径表现不同 |
| ATV v0.2.1 单骨骼修复 | 已确认 | 0.8.3 保留九个独立 geometry，但都挂到单个 identity `ATV` 场景节点；目标 Engine 实际导入结果为 1 bone/9 meshes，低于七骨骼限制。物品代码改为枚举全部 meshes；2026-10-20 用户在 Android 7723 确认物品栏与世界模型显示通过。驾驶、越障、保存和多人不继承此结论 |
| ATV v0.23 X1 模型 V2 | 已确认（静态/目标导入） | `New-AtvAssets.ps1` 可复现生成「荒野探索者 X1」：13 个独立 Y_UP geometry（放样 16 边轮、管状车架/前叉/排气、多面体油箱、环面弹簧减震、弧板挡泥）全部烘焙并挂单个 identity `ATV` 节点；目标 `Engine.Media.ModelData.Load` 导入为 1 bone/13 meshes，共 3352 三角面（预算 6000）；64×64 不透明材质图集（33 色）；包围盒 X±0.3713/Y0.0032–1.187/Z±1.2868 完全落入 `1.1,1.25,2.6` Body。Block 923、`SCIEPortATV` GUID、双座、驾驶与联机逻辑均未改动。Android/7723 新外观渲染待验证 |
| ATV v0.23.1 世界绿色偏色 | 已确认（缺陷/机理） | 世界实例渲染把绑定贴图乘以 `ComponentModel.DiffuseColor`；v0.23.0 沿袭旧草绿 `(0.38,0.74,0.16)`，与 X1 橙/银图集相乘后整车在世界里发绿，而物品栏图标烘焙进 `BlockMesh` 不乘该色故显示正常。v0.23.1 改为 `Vector3.One` 白漫反射 + 中性低自发光 `(0.045,0.045,0.05,0)`，与越野/蒸汽船/飞艇一致。Android/7723 世界配色待复验 |
| ATV v0.2 阻力修复 | 已确认 | 真实 `ComponentBody.Update` 的 `6*dt` 接地阻力已反编译确认；驾驶时禁用该项并使用帧率无关的纵向/横向控制，无人时恢复。Android 手感待验证 |
| ATV v0.2 一格越障 | 已确认 | ABI/代码级确认使用原生三轴碰撞和连续 `4.5*dt` smooth rise，`MaxSmoothRiseHeight=1.05`；不写 Position/地形且不抬高实体碰撞。Android 一格台阶与净空仍待验证 |
| ATV v0.2 诊断日志 | 已确认 | 仅本地单人、有人驾驶时每 5 秒输出速度、油门、地形/实体支撑、离地时间、原生阻力开关和状态；Android 日志格式待验证 |
| ATV v0.3 原生双 Mount 可行性 | 不支持 | `ComponentMountPackage` 只传目标实体 ID，接收端取首个 Mount；同实体第二个 Mount 无法寻址。多层乘客锚点又会因原生碰撞只排除直接父子 Body 而让乘客碰撞祖父车体 |
| ATV v0.3 座位关联 ABI | 已确认 | 两名玩家均直接挂 ATV Body；座位管理器由 Local/Server 最终分配，服务端以自定义 `IPackage` ID 240 下发离散角色/偏移。目标原生包占 ID 0–38，240 未占用；其它 Mod 冲突仍待安装时检查 |
| ATV v0.3 联机权威 | 待验证 | v0.22 允许本机主玩家在驾驶位沿用原生父车 BodyUpdate；v0.22.1 隔离乘客个人移动并维持独立座位。严格服务器权威仍不支持，Android 双机待验证 |
| 双人越野车 ID 925 | 已确认 | 固定 x25.01.17 的原生方块常量反射扫描未占用 925，且与本工程其它 ID 无内部冲突；其它 Mod 冲突仍需安装时检查 |
| 双人越野车模型与尺寸 | 已确认（静态/目标导入） | 项目原创 Y_UP 模型为 1 bone/24 meshes，范围 X±1.15、Y 0.08..1.48、Z -1.78..1.68，完整落入 2.4×1.55×4.0 Body；40×8 五色 PNG 全不透明。Android 世界显示待验证 |
| 双人越野车驾驶与双座 | 已确认（代码/规则） | 独立地面驱动，不使用船物理；原生 Body 负责重力、碰撞与 1.05 格 smooth rise。驾驶/乘客为左右固定座位，只有驾驶员输入进入 Drive；Windows/Android 运行待验证 |
| 双人越野车联机 | 待验证 | v0.22 接入普通客户端本机驾驶员的原生父体同步，v0.22.1 隔离乘客输入并维持独立座位；Android 双机角色互换待验证 |
| SCIE 工业家具 ID 917–922 | 已确认 | x25.01.17 的 260 个原生方块常量反射扫描未占用；本工程内部无重复，其他 Mod 冲突仍待安装时检查 |
| SCIE 工业家具模型 | 已确认 | Gear、Wheel、GearMould、WheelMould、Piston 已从 Z_UP 转为 Y_UP，Cylinder 源文件本身为 Y_UP；节点名、position float 数和包内资源由构建脚本校验 |
| SCIE 工业家具代码 | 已确认 | 六件静态可放置方块、四向 data、碰撞箱、掉落路径对 x25.01.17 Release 编译通过后方可维持此状态；不含机器语义 |
| SCIE 工业家具 Windows/Android 运行 | 待验证 | 创造栏、模型比例/颜色、四向放置、碰撞、拆除掉落、保存重载尚未进游戏验证 |
| SCIE 工业家具机器/配方/库存功能 | 不支持 | 0.7.0 明确只移植静态家具外观，不伪造上游工业逻辑 |
| T-34 坦克 ID 924 | 已确认 | `Test-Environment.ps1` 对固定 x25.01.17 程序集扫描 260 个原生方块常量；924 未被原版占用，仍需安装时检查其他 Mod 冲突 |
| T-34 内部炮弹 ID 926 | 已确认 | 固定 x25.01.17 原生常量扫描未占用 926，且本工程无内部冲突；`TankShellBlock.GetCreativeValues()` 返回空，不在创造栏显示。其他 Mod 的 926 冲突仍需安装时检查 |
| T-34 实体/XDB/资源布局 | 已确认 | `Tanks.xdb` 复用原版文件夹 GUID；静态构建守卫检查实体组件、模型、纹理、1.05 格台阶限制与三处模型节点；v0.12.2 明确禁止重新加入 `Damage` 组件 |
| T-34 单人地面驱动代码 | 已确认 | 仅 `WorkType.Local`；真实程序集反射检查 Mount/Rider/Input/Body/CreatureModel 所需表面，且 Release 编译通过后方可维持该状态 |
| T-34 Windows 游戏运行 | 待验证 | 未启动游戏；生成、骑乘、触屏/键鼠驱动、碰撞、坡度、下车、视角与存档恢复均待验证 |
| T-34 Android 7723 基础运行 | 已确认 | 2026-10-20 用户确认坦克测试通过；模型显示、生成、骑乘与基础驾驶可继承该实测结论。用户同时确认旧版不能开炮、不能跨越完整一格且占地偏小；这些缺陷不属于通过项 |
| T-34 多人同步 | 不支持 | 第一阶段明确只在 `WorkType.Local` 驱动，未实现服务器权威输入、状态复制或纠偏 |

| T-34 v0.2 模型/贴图 | 已确认（静态/导入） | 项目自有 Y_UP 资源由生成脚本重建为 20 网格；目标 `Engine.Media.ModelData.Load` 实际导入 `TankHull > TankTurret > TankGun` 共 3 骨骼/20 网格，低于七骨骼预算；64×8 PNG 含 8 种不透明颜色。Android 世界渲染仍待验证 |
| T-34 v0.2 地面驾驶 | 已确认（代码/测试） | 驾驶时关闭原生地面阻力以避免双重阻力；统一处理推进、倒车、制动、滑行、侧滑和转向。仅地形接触允许推进，离地保留惯性，浸水/实体支撑不施加推进。Android 手感和碰撞仍待验证 |
| T-34 v0.2 碰撞范围 | 已确认（参数/模型范围） | `BoxSize=2.6,1.55,4.4` 覆盖当前车身和炮管坐标范围，`MaxSmoothRiseHeight=0.3`；仍是单一原生长方体 Body，不宣称复合履带/炮管碰撞，狭窄地形待实机验证 |
| T-34 v0.12.2 主炮（历史） | 已确认（源码/固定内容包/编译），Android 待验证 | AABB 外炮口、owner 恢复与压力 2160 证据仍沿用；1.5 格引信和 12 格安全包络已被 v0.22.2 全距离首次命中爆炸取代。实际破坏范围和性能须 Android 可丢弃世界复测。 |
| T-34 v0.12.2 炮塔骨骼 | 已确认（源码/规则/编译），Android 待验证 | 活动摄像机每帧给出目标角，`TankTurret` 以 1.2 rad/s、`TankGun` 以 0.65 rad/s 逐步追随；点击射线只作为开火触发，不再覆盖瞄准方向。视觉骨骼和炮弹共同读取当前 `TurretYaw/GunPitch`，避免“点哪里瞬间打哪里”。 |
| T-34 v0.22.2 有限耐久 | 已确认（XDB/构建守卫），Android 待验证 | 恢复原生 `ComponentDamage`：`Hitpoints=1`、`AttackResilience=600`，归零报废；仍无 `ComponentOnFire`。旧 v0.12.2 无限耐久结论已被本版本取代 |
| T-34 v0.12.3 炮弹外观 | 已确认（源码/编译），Android 待验证 | 专用 `TankShellBlock` 复用目标运行时内置 `Models/Snowball` 网格，不打包游戏资源；以纯黑顶点色和 3.5 倍原生投射物绘制比例替代旧 `BulletBlock` 的 0.3 平面贴片。Android 实际尺寸、光照可见性待验证。 |
| T-34 v0.12.3 首次命中爆炸（历史） | 已确认（ABI/XDB/构建守卫），Android 待验证 | `SCIEPortTankShellBehavior.OnHitAsProjectile` 首次碰撞架构及 CSV 防反弹设置继续沿用；旧安全引信/包络已由 v0.22.2 取消，普通移除不爆炸。 |
| T-34 v0.3 尺寸与一格越障 | 已确认（静态/编译），Android 待验证 | 模型坐标整体放大 35%，Body 改为 `3.6,2.1,5.9`，座位与下车偏移同步调整；`MaxSmoothRiseHeight=1.05` 使用原生连续抬升与碰撞/净空检查，不写 Position、不改地形 |

## 当前包布局

```text
SCIEPort.dll
modinfo.json
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Blocks/KellyPlants.csv
Assets/Blocks/IndustrialFurniture.csv
Assets/Blocks/LandVehicles.csv
Assets/Blocks/OffroadVehicles.csv
Assets/Blocks/Tanks.csv
Assets/Database/Watercraft.xdb
Assets/Database/KellyPlants.xdb
Assets/Database/LandVehicles.xdb
Assets/Database/OffroadVehicles.xdb
Assets/Database/Tanks.xdb
Assets/Models/Airship.dae
Assets/Models/ATV.dae
Assets/Models/OffroadCar.dae
Assets/Models/KellyFruits.dae
Assets/Models/Gear.dae
Assets/Models/GearMould.dae
Assets/Models/Piston.dae
Assets/Models/Minecart.dae
Assets/Models/SteamBoat.dae
Assets/Models/Train.dae
Assets/Models/Wheel.dae
Assets/Models/WheelMould.dae
Assets/Models/Cylinder.dae
Assets/Models/T34Tank.dae
Assets/Textures/SteamBoat.png
Assets/Textures/Train.png
Assets/Textures/ATV.png
Assets/Textures/OffroadCar.png
Assets/Textures/KellyExpansion.png
Assets/Textures/T34Tank.png
THIRD-PARTY-NOTICES.md
```

## 0.13.0 狐狸生物验证

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| glTF/GLB 直接加载 | 不支持 | 固定目标 `SurvivalCraftModEntity` 仅注册 DAE、JSON、OBJ；参考项目的 `AnimationController` 也不在目标程序集。 |
| 狐狸 XDB/原版 AI 继承 | 已确认（静态） | `SCIEPortFox` 继承固定 `Content.scpak` 中 Wolf GUID `49a9845c-3810-4092-ba53-df213d10d563`，真实 Entities/BlockBehaviors GUID 与参数由构建检查固定。 |
| 狐狸 DAE/PNG | 已确认（离线导入） | 项目自制 Y_UP 资源；目标 `Engine.Media.ModelData.Load` 导入为 6 bones/13 meshes，骨骼名与 `ComponentFourLeggedModel.SetModel` 完全一致。 |
| 狐狸生成物品 ID 927 | 已确认 | 固定程序集 260 个原生 Block.Index 扫描未占用；其它 Mod 的运行时冲突仍需安装时检查。 |
| 自然生成 | 不支持 | 第一阶段没有注册 `SubsystemCreatureSpawn.CreatureType` 或地形生成 Hook。 |
| Windows 游戏运行 | 待验证 | 按要求未启动游戏；加载、生成、AI、动画、死亡和存档恢复未在 Windows 运行。 |
| Android 7723 | 待验证 | 图标、模型、UV、动画、碰撞、生成与退出重进均需真机新世界验证。 |
| 多人联机 | 不支持 | 普通 Client 被禁止本地创建狐狸；尚无客户端请求、服务器确认与实体复制专项协议。 |

兼容性结论发生变化时，必须同时更新 `TEST-RESULTS.md` 中的测试证据。

## 方块图集 slot→像素映射取证（V1 作物/植物自绘图集前置）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 方块面贴图网格（block face texture slot） | 已确认（原生资源实测，非自证） | 从目标 `Content.scpak` 用 `Game.SurvivalCraftModEntity.GetDecipherStream`（static, NonPublic）解出 ZIP，取 **游戏自带** `Assets/Textures/Blocks.png`：反射 PNG 头实测 `512×512`、bitDepth=8、colorType=6（RGBA）。CSV 第 74 列 `DefaultTextureSlot` 是纯整数（原生实测：Snow=66、Ice=65、Sandstone=176、Water=189、Magma=141）。对同一张**原生**图集按不同“每行 tile 数”假设取样各格平均 RGB：仅 **每行 16 格 / 每格 32×32 像素**（即 `col=slot%16, row=⌊slot/16⌋, 像素矩形左上=(col*32,row*32)`，容量 256 槽）假设下，五个已知方块都落在语义正确颜色——Snow(64,128)=250,252,252 近白、Ice(32,128)=217,227,244 冷蓝、Sandstone(0,352)=196,182,138 黄褐、Magma(416,256)=174,57,5 橙红、Water(416,352)=185,185,185 中性偏蓝；每行 8 格（64px）全部读到 (0,0,0) 越界空白，每行 32 格（16px）读到灰阶错色。结论与自绘 PNG 无关，是对游戏资产的独立验证。 |
| 命名贴图（模型/部件/GUI 图集）机制 | 已确认（机理区分） | 同一 `Content.scpak` 内 `Assets/Atlases/Atlas.txt`（驱动 `Game.TextureAtlasManager.LoadTextureAtlas(texture, atlasDefinition, prefix)`）是**按名字显式列出每个子图矩形**（`Name x y w h ...`，尺寸 16/24/32/40/64/112/224 不等、位置任意）。因此 `Models/*`、实体 `TextureOverride` 与 GUI 走“具名矩形”通道，与方块面的“固定 16×16 网格整数槽”通道**不同**，两者不可混用；自绘作物方块必须按方块面网格约定排布，不能借用 Atlas.txt 具名机制。 |
| 自绘作物图集（Crops.png）实际渲染 | 待验证 | 上述网格与槽→像素公式已在原生资产上确认；但据此新建的 512×512 RGBA 自绘图集能否被加载器解析并在 Android 7723 正确采样（尤其 alpha 测试、放大过滤、创造栏图标）须真机新世界验证，不提前宣称已修复。 |

## 0.13.0 兔子生物基础验证（commit cb0f309，手动生成器阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 兔子 XDB/原生 AI 继承 | 已确认（静态） | `SCIEPortRabbit` 继承固定 `Content.scpak` 中原版 Alpaca 食草动物模板 GUID `8e62c6e4-...`（LandAnimal 后代），复用原生四足步态 + 吃拾取物/群居/逃跑 AI，思路与狐狸复用 Wolf 的追捕链一致。`Test-Database.ps1` 已把该 GUID 加入 `$KnownVanillaParents` 白名单；真实 Entities/BlockBehaviors GUID 与 7 个覆盖组件由 `Build-NetMod.ps1` 校验。 |
| 兔子 DAE/PNG | 已确认（离线导入） | 项目自制 Y_UP `Rabbit.dae`（6 骨骼 Body/Head/Leg1-4 + 12 立方网格，每网格 24 顶点/12 三角）与 32×8 四色不透明 `Rabbit.png`，由 `New-RabbitAssets.ps1` 生成；`Build-NetMod.ps1` 用目标 `Engine.Media.ModelData.Load` 实测导入 6 bones/12 meshes 并逐骨骼名校验，PNG 断言恰好 4 种 A=255 颜色。 |
| 兔子生成物品 ID 928 | 已确认 | 固定 x25.01.17 的 260 个原生 `Block.Index` 反射扫描未占用 928；`Test-Environment.ps1` 保留区间扩至 903–928 且无原生冲突；其它 Mod 运行时冲突仍需安装时检查。 |
| 兔子权限闸（联机） | 已确认（代码/规则） | `SubsystemRabbitSpawnBlockBehavior.OnUse` 在 `CommonLib.WorkType == WorkType.Client` 时直接返回不生成，避免普通客户端本地制造未同步实体。真实主机/客户端同步效果待联机实测。 |
| 自然生成 | 不支持 | 本阶段仅创造栏手动生成器（`Spawner Eggs` 类），未注册 `SubsystemCreatureSpawn.CreatureType` 或地形生成 Hook。 |
| 生态互动（被捕食/喂食） | 不支持 | 本阶段不接线狐狸追捕兔子、兔子吃作物等互动，留待基础加载存读档实机验证后再单独成阶段。 |
| Windows 游戏运行 | 待验证 | 按要求未启动游戏；加载、生成、AI、动画、死亡与存读档未在 Windows 运行。 |
| Android 7723 | 待验证 | 图标、模型、UV、动画、碰撞、生成与退出重进均需真机新世界验证。 |
| 离线逻辑测试 | 已确认（净8 无游戏依赖） | `Features/Ecology/EcologyRules.cs`（种群上限、每块生成预算、确定性 chunk 种子、生物群系适宜度、权限闸、捕食资格、存读档计数钳位）配套 `tests/SCIEPort.Ecology.Tests` 25 条断言通过，不引用游戏程序集。 |
兼容性结论发生变化时，必须同时更新 `TEST-RESULTS.md` 中的测试证据。

## 0.14.0 玉米作物基础验证（自绘图集 + 多阶段生长，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 玉米三块注册（Stalk 929 / Seed 930 / Ear 931） | 已确认（静态 ABI） | 三个公开 `Block` 子类各带 `public const int Index`（`CornStalkBlock:CrossBlock`、`CornSeedBlock:FlatBlock`、`CornEarBlock:FlatBlock`），遵循 SCNET“类名即注册、CSV `Class Name` 列关联元数据”约定。`Test-Environment.ps1` 保留区间扩至 903–931，反射扫描 x25.01.17 全部原生 `Block.Index` 无占用冲突。 |
| Crops.csv 元数据 | 已确认（打包校验） | 表头 + 3 行，UTF-8 CRLF、每行 79 列（由脚本读取原生 Kelly CSV 模板生成，非手抄）。`Stalk` 复用 SoybeanBlock 模板、`Seed/Ear` 复用 SoybeanFruitBlock 模板；`Class Name`/`DefaultDisplayName`（玉米/玉米种子/玉米棒）/`DefaultCategory=Crops`/`DisplayOrder=330/331/332`/`DefaultTextureSlot=0/8/9`/`IsPlaceable=TRUE` 均由 `Build-NetMod.ps1` 逐列断言。 |
| Crops.xdb 轮询行为挂载 | 已确认（合并/结构） | 根 `DatabaseObjects`；`Folder BlockBehaviors` 用真实原版 GUID `00c97f0f-731e-481c-9909-eae9cc5ee940`；`SubsystemTemplate SCIEPortCornBehavior` 继承固定父 `fefb9590-4972-4893-b02a-76063611b745`（`SubsystemBlockBehavior` 基），`Parameter Class=SCIEPort.Features.Crops.SubsystemCornBlockBehavior`；`ProjectTemplate`（真实 GUID `85023bf8-1c90-4dd1-9442-e6c13691d078`）挂 `MemberSubsystemTemplate`。轮询生成走世界子系统 `HandledBlocks={929}`，非 CSV `Behaviors` 列。所有叶子 GUID 由 `randomUUID()` 生成，`Build-NetMod.ps1` 校验继承父与成员 GUID 一致。 |
| 生长规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | `Features/Crops/CropRules.cs`：尺寸=data&7（0..7 成熟阈 7）、`SetSize` 保留掩码外位（测 0x40）、最低光照 9、生长间隔 8 基值（水利 -2、氮肥 -2、低温≤4 +8、高温≥12 -1）、放置需顶面土壤、收获 1 棒 + 2 种、权限闸 `!isClient`。配套 `tests/SCIEPort.Crops.Tests` 26 条断言全部通过，不引用游戏程序集。 |
| 轮询行为接线 | 已确认（代码） | `SubsystemCornBlockBehavior.OnPoll` 在 `CommonLib.WorkType==WorkType.Client` 直接返回（联机仅权威端推进），校验 skylight≥9、size<7、下方为土壤，读 `SoilBlock.GetHydration`（反射实测返回 `Boolean`，直取）、`GetNitrogen`、季节温度与 `SubsystemWeather.GetTemperatureAdjustmentAtHeight`，`ChangeCell` 推进 size+1，成熟时消耗土壤氮 1；`OnNeighborBlockChanged` 土壤被移除则摧毁作物。真实主机/客户端推进与联机同步待实测。 |
| 自绘图集 Corn.png | 待验证 | 512×512 `Format32bppArgb`，按已确认的方块面网格公式排布 slot 0..9 于首行（`x=slot*32,y=0`），slot 0..7 茎秆逐阶段、8 种子、9 玉米棒，10/11 留空。`Build-NetMod.ps1` 断言尺寸/像素格式且 slot 0..9 存在不透明像素、10 与 15 全透明；独立 PNG 解码确认阶段 0..7 不透明像素单调增长（4→10→32→38→60→66→96→123）、种子 78、棒 246。但据网格公式自绘的图集能否被加载器在 Android 7723 正确采样（alpha 测试、放大过滤、创造栏图标、跨阶段贴图切换）须真机新世界验证。 |
| Windows 游戏运行 | 待验证 | 未启动游戏；种植、逐阶段生长、成熟收获、掉落到背包与存读档未在 Windows 运行。 |
| Android 7723 | 待验证 | 方块渲染、贴图、种植交互、生长轮询、收获掉落、退出重进均需真机新世界验证，不提前宣称已修复。 |

## 0.15.0 竹子作物基础验证（纵向逐段生长 + 建筑方块，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 竹子两块注册（Plant 932 / Block 933） | 已确认（静态 ABI） | `BambooPlantBlock:CrossBlock`（Index 932，逐段堆叠的植株）与 `BambooBlock:Block`（Index 933，实心建筑立方体），各带 `public const int Index`。`Test-Environment.ps1` 保留区间扩至 903–933，反射扫描 x25.01.17 全部原生 `Block.Index` 无占用冲突。`SoilBlock/SandBlock/DirtBlock` 均有公开静态 `Index`（反射实测）作基座判定。 |
| 实心立方体自绘图集渲染 | 已确认（API 取证）+ 待验证（真机） | 目标程序集实测：`BlockGeometryGenerator.GenerateCubeVertices(block,value,x,y,z,Color,TerrainGeometrySubset[])` 走方块自身 `GetFaceTextureSlot` 并允许按面传入自建图集的 `SubsetOpaque`；图标用 `BlocksManager.DrawCubeBlock(...,Texture2D)` 重载传入 `Textures/Bamboo`。据此 `BambooBlock` 六面采样自绘图集 slot 2。能否在 Android 7723 正确实心渲染待真机验证。 |
| 逐段纵向生长机制 | 已确认（代码/离线规则） | DATA bit0（`BaseFlagMask=1`）标记“栽下的基段”，仅基段驱动向上堆叠（一段一生长者，防失控）。`OnPoll`：基段脚下需土壤/沙/泥土，向上数连续竹段得高度，`CanExtend(height<8)` 且顶上一格为空气、其上光照≥8，按 `GetGrowthInterval`（基 3、水利 -1、低温≤4 +3、下限 1）用 `pollPass % interval` 节流后在顶端 `ChangeCell` 加一段（bit0 清）。`OnNeighborBlockChanged`：基段失基座销毁，非基段下方非竹销毁（支持逐段塌落）。 |
| 生长规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | `Features/Bamboo/BambooRules.cs`：`MaxHeight=8`、基段位往返/幂等、掩码外位保留（测 0x40 与基位共存）、上段判定、`CanExtend` 上下界（0 不可、cap 停、超 cap 停）、间隔（干暖 3/水利 2/低温 6/水寒 5）、放置需顶面有效基座、砍伐每段掉 1、权限闸 `!isClient`。配套 `tests/SCIEPort.Bamboo.Tests` 25 条断言全部通过，不引用游戏程序集。 |
| Bamboo.csv 元数据 | 已确认（打包校验） | 表头 + 2 行（植株/建材），UTF-8 CRLF、每行 79 列（由 node 读 Crops.csv 表头按列名改写生成，非手抄）。`Build-NetMod.ps1` 断言：`BambooPlantBlock` 属 Crops 类可放置且 `DefaultTextureSlot=0`；`BambooBlock` `IsPlaceable=TRUE`、`IsCollidable=TRUE`、`IsTransparent=FALSE`、`DefaultTextureSlot=2`。 |
| Bamboo.xdb 轮询行为挂载 | 已确认（合并/结构） | 根 `DatabaseObjects`；`Folder BlockBehaviors` 用真实原版 GUID `00c97f0f-...`；`SubsystemTemplate SCIEPortBambooBehavior` 继承固定父 `fefb9590-...`（`SubsystemBlockBehavior` 基），`Parameter Class=SCIEPort.Features.Bamboo.SubsystemBambooBlockBehavior`；`ProjectTemplate`（真实 GUID `85023bf8-...`）挂 `MemberSubsystemTemplate`。轮询走世界子系统 `HandledBlocks={932}`。叶子 GUID 由 `randomUUID()` 生成，`Build-NetMod.ps1` 校验继承父与成员 GUID 一致。 |
| 自绘图集 Bamboo.png | 待验证 | 512×512 `Format32bppArgb`，slot 0 基段十字面、slot 1 上段十字面、slot 2 建材立方面。`Build-NetMod.ps1` 断言尺寸/格式、slot 0..2 有不透明像素、slot 2 全 1024 像素不透明（实心面）、slot 5 全透明。实际加载/alpha 测试/放大过滤/创造栏图标待真机验证。 |
| Windows 游戏运行 | 待验证 | 本机未启动游戏；种植、逐段堆叠、砍伐塌落、建材放置与存读档均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、生长动画、塌落、建材实心碰撞与退出重进恢复均需真机新世界验证。 |
| 多人联机 | 待验证 | `OnPoll`/`OnNeighborBlockChanged` 已在 `WorkType.Client` 直接返回，仅权威端堆叠，但真实主机/客户端同步与一致性未实测。 |

## 0.16.0 亚麻作物基础验证（多阶段生长 + 纤维收获，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 亚麻两块注册（Plant 934 / Fiber 935） | 已确认（静态 ABI） | `FlaxPlantBlock:CrossBlock`（Index 934，0..7 阶段）与 `FlaxFiberBlock:FlatBlock`（Index 935，纤维成品），各带 `public const int Index`。`Test-Environment.ps1` 保留区间扩至 903–935，反射扫描 x25.01.17 全部原生 `Block.Index` 无占用冲突。 |
| 生长规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | `Features/Flax/FlaxRules.cs`：`MaxFlaxSize=7`、尺寸=data&7、`SetSize` 保掩码外位（测 0x40）、最低光照 8、生长间隔 7 基值（水利 -2、氮肥 -2、低温≤4 +6、高温≥12 -1）、放置需顶面土壤、收获 2 纤维 + 1 可重种种子、权限闸 `!isClient`。配套 `tests/SCIEPort.Flax.Tests` 29 条断言全部通过，不引用游戏程序集。 |
| 轮询行为接线 | 已确认（代码） | `SubsystemFlaxBlockBehavior:SubsystemPollableBlockBehavior`（`HandledBlocks={934}`）在 `WorkType.Client` 直接返回（联机仅权威端推进），校验 skylight≥8、size<7、下方土壤，读 `SoilBlock.GetHydration`（`Boolean` 直取）、`GetNitrogen`、季节温度与高度调整，`ChangeCell` 推进 size+1，成熟时消耗土壤氮 1；`OnNeighborBlockChanged` 土壤被移除则摧毁。真实主机/客户端推进与联机同步待实测。 |
| Flax.csv 元数据 | 已确认（打包校验） | 表头 + 2 行（植株/纤维），UTF-8 CRLF、每行 79 列（node 读 Crops.csv 表头按列名改写生成，植株复用玉米茎 CrossBlock 模板、纤维复用玉米种 FlatBlock 模板，非手抄）。`Build-NetMod.ps1` 断言两者属 Crops 类可放置且 `DefaultTextureSlot` 分别 0 / 8。 |
| Flax.xdb 轮询行为挂载 | 已确认（合并/结构） | 根 `DatabaseObjects`；`Folder BlockBehaviors` 用真实原版 GUID `00c97f0f-...`；`SubsystemTemplate SCIEPortFlaxBehavior` 继承固定父 `fefb9590-...`，`Parameter Class=SCIEPort.Features.Flax.SubsystemFlaxBlockBehavior`；`ProjectTemplate`（真实 GUID `85023bf8-...`）挂 `MemberSubsystemTemplate`。轮询走世界子系统非 CSV Behaviors 列。叶子 GUID 由 `randomUUID()` 生成，`Build-NetMod.ps1` 校验继承父与成员 GUID 一致。 |
| 自绘图集 Flax.png | 待验证 | 512×512 `Format32bppArgb`，slot 0..7 亚麻植株逐阶段（细茎随阶段增高、成熟期顶端蓝色亚麻花）、slot 8 纤维束，slot 12 留空。`Build-NetMod.ps1` 断言尺寸/格式、slot 0..8 有不透明像素、slot 12 全透明；生成器自检阶段 0..7 不透明像素单调增长（18→36→54→72→90→114→132→154）、纤维 271。据网格公式自绘的图集真机采样/alpha/图标待验证。 |
| Windows 游戏运行 | 待验证 | 本机未启动游戏；种植、逐阶段生长、成熟收获（纤维 + 可重种种子）、掉落与存读档均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、蓝花阶段、种植交互、收获与退出重进均需真机新世界验证，不提前宣称已修复。 |

## 0.17.0 自然生成 spawner 验证（狐/兔按种子散布，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 生成器接线（复用已证 Hook） | 已确认（代码） | 自然生成走已被 Kelly 植物散布验证的 `OnTerrainContentsGenerated(TerrainChunk)` 块生成钩子，未新增地形写入路径。`CreatureWorldSpawn` 静态世界侧类不注册独立 SubsystemTemplate/XDB/CSV/GUID：它由 `SubsystemFoxSpawnBlockBehavior.Load` 与 `SubsystemRabbitSpawnBlockBehavior.Load` 各调 `SetProject(Project)` 缓存 `SubsystemCreatureSpawn/SubsystemGameInfo/SubsystemTerrain`（二者已持有经验证的 `Project`），故零新增数据库面。加载器把植物散布与生物生成放在各自 `try/catch` 内、失败 `Log.Error` 记录不吞异常。 |
| 静态 ABI 取证 | 已确认（反射） | 本阶段先反射目标 x25.01.17 再动手，确认 `TerrainChunk.GetTemperatureFast(x,z)→Int32`、`GetHumidityFast(x,z)→Int32`、`chunk.Coords.X/Y`、`chunk.Origin.X/Y`（世界偏移，与 Kelly 的 `Origin.X+localX/Origin.Y+localZ` 映射一致）、`chunk.GetTopHeightFast/GetCellValueFast`、`SubsystemGameInfo.WorldSeed→Int32`、`SubsystemCreatureSpawn.SpawnCreature(string,Vector3,bool)`（狐/兔手动生成器已用）均存在；`Game.Random(seed).Int(min,max)` 沿用 Kelly 已验证用法。实体类型按狐/兔生成器一致写全限定 `GameEntitySystem.Entity`，`Random` 全限定 `Game.Random` 避免 Engine/Game 二义。 |
| 决策规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | 一切可纯化的判定委托 `Features/Ecology/EcologyRules.cs`：狐狸基础几率 8 < 兔子 15（捕食者比猎物更稀有，避免新图被过度捕食）；`ChanceMet(roll0To99,chancePercent)` 对越界 roll（<0 或 >99）一律不命中，且该 roll 守卫置于 `chance>=100` 短路之前（本阶段实测发现原实现 `ChanceMet(-1,100)` 误判命中，已改序并新增断言）；`CanSpawnWithinBudget` 每块至多 2 次尝试；`ClampTrackedCount` 钳到物种上限；`IsFoxHabitat/IsRabbitHabitat` 按温度/湿度/自然地面/树冠覆盖。`tests/SCIEPort.Ecology.Tests` 37 条断言全部通过（含新增几率/预算 12 条）。 |
| 确定性种子 | 已确认（离线）+ 待验证（真机） | 掷骰序列由 `GetChunkSeed(WorldSeed, chunk.Coords.X, chunk.Coords.Y)` 派生，同世界同 chunk 恒定、重进不重排（离线确定性断言通过）。真机跨存档/重进是否一致待验证。 |
| 权限闸（联机） | 已确认（代码/规则） | `CanAuthoritativeWorldMutation(WorkType==Client)` 令普通客户端直接返回、绝不在本地制造未同步实体；仅单机 Local 与主机/服务端尝试生成。真实主机/客户端同步效果待联机实测。 |
| 每块预算与种群上限 | 已确认（规则）+ 已知缺口 | 单块生成至多 2 只（不整图扫描、不高帧写入），狐全局上限 3、兔 6。存活计数 `s_trackedFoxes/s_trackedRabbits` 仅在成功生成时自增并钳到上限。生物死亡回收（递减计数）未接线——这是如实声明的已知缺口：计数只增不减，最坏是过早停止生成（保守安全）而非过度繁殖。死亡事件接线留待后续阶段。 |
| 实体生成激活（生成期 SpawnCreature） | 待验证 | 静态 ABI 与全部纯规则已确认，但“在块生成回调内调用 `SpawnCreature` 造实体”能否在 Windows 与 Android 7723 真机稳定成功、不崩、不阻塞生成、不产生未同步实体，尚未观测；按要求不提前宣称已修复。生成失败经 `Log.Error` 记录（不吞异常）以留下真机证据。 |
| 生态互动（狐追捕兔） | 待验证（未接线） | `IsEligiblePredation(狐活/兔活/距离²/追捕半径)` 纯谓词已测；实际追捕仍委托原生 Wolf 衍生 AI，本阶段不接线，留作独立阶段。 |
| Windows 游戏运行 | 待验证 | 本机未启动游戏；自然生成时机、生成实体存活、存读档与卸载均未运行。 |
| Android 7723 | 待验证 | 生成实体在低端机的性能、上限表现、与原生生物群系的共存、退出重进恢复均需真机新世界验证。 |
| 多人联机 | 待验证 | 已拒普通客户端生成，但主机权威生成后向客户端的实体同步、以及房主/加入者一致性未实测。 |

## 0.18.0 生态互动：狐追捕兔（数据式委托原生 Wolf AI，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 捕食机制取证 | 已确认（反射 + 资产实测） | 反射 x25.01.17 确认原生 `Game.ComponentChaseBehavior` 拥有 `FindTarget()`/`ScoreTarget(ComponentCreature)`/`IsTargetInAttackRange`，字段 `m_autoChaseMask : Game.CreatureCategory`；`Game.CreatureCategory` 为 `[Flags]`（LandPredator=1/LandOther=2/WaterPredator=4/WaterOther=8/Bird=16）；`Game.ComponentCreature.Category` 属性即生物声明的类别。再用 `Game.SurvivalCraftModEntity.GetDecipherStream(Stream)`（static, NonPublic）解出 `Content.scpak` 内的 `Assets/Database.xml`（987 条目、463610 字节）实测：基类 `ChaseBehavior`(GUID `31894441-…`) 默认 `AutoChaseMask="LandOther, Bird"`、`ChaseNonPlayerProbability=0`；`Wolf`(GUID `49a9845c-…`，狐继承父) 只覆写 `ChaseNonPlayerProbability=0.03`、`DayChaseRange=4`、`NightChaseRange=10`、`NightChaseTime=15`（掩码沿用基类）；`Alpaca`(GUID `8e62c6e4-…`，兔继承父) `Creature.Category="LandOther"`（参数 `1914b71d-…`）。据此狐（继承狼的 ChaseBehavior+Miner）本就把 LandOther 列为可追对象，兔（继承羊=LandOther）本就在其猎物集合内。 |
| 不臆造 API | 已确认（取证纠偏） | `Database.xml` 基类 `ChaseBehavior` 参数集不含 `IsAttack`——反射中 `m_isAttack`/`IsAttack` 是运行时字段而非可序列化模板参数，故本阶段未在模板里写 `IsAttack`（避免臆造参数）；实际“咬击伤害”来自狐继承的狼 `Miner.AttackPower=1` 与追捕进入攻击距离后的原生结算。 |
| 数据式接线（委托原生 AI） | 已确认（代码/数据） | 只在 `SCIEPortFox` `EntityTemplate` 增加一个 `ChaseBehavior` `MemberComponentTemplate` 覆盖：`AutoChaseMask="LandOther"`（把猎手范围显式锁定到陆生猎物，不再依赖基类默认顺带含 Bird）与 `ChaseNonPlayerProbability="0.35"`（狼基线 0.03 过低、几乎不主动猎非玩家动物，故提高使捕食可观察）。不写任何 C# 转向/寻路/攻击逻辑，全部交给原生 `ComponentChaseBehavior` 的 `FindTarget/ScoreTarget`。匹配方式与本工作区既有覆盖一致：按 `Name` 关联继承组件、`Parameter` 按 `Name` 覆写字段、各用唯一 Guid（成员 `b8e3cebb-…`、参数 `60d23cc8-…`/`7e78e276-…`）。 |
| 猎物资格（离线纯逻辑） | 已确认（净8 无游戏依赖） | `EcologyRules` 新增：`FoxAutoChaseMask="LandOther"`、`RabbitCreatureCategory="LandOther"`（与 XDB 单一事实源一致）、`VanillaWolfChaseNonPlayerProbability=0.03`、`FoxChaseNonPlayerProbability=0.35`，及镜像原生“类别是否在掩码内”判定的纯谓词 `IsCategoryChased(mask,category)`——逗号分隔解析、把序列化为 `"0"`（即 `CreatureCategory.None`）与 null/空一律判不追、大小写不敏感并逐段 trim。`tests/SCIEPort.Ecology.Tests` 由 37 增至 47（新增 10 断言，含核心不变量“狐掩码必须覆盖兔类别”“狐概率必须高于狼基线”）。 |
| 打包校验 | 已确认 | `Build-NetMod.ps1` 对 `SCIEPortFox` 断言存在 `ChaseBehavior` 覆盖、其 `AutoChaseMask` 逗号集合含 `LandOther`、`ChaseNonPlayerProbability` 可解析为 float 且 > 0.03；缺失或过小即抛错。 |
| 实机捕食激活 | 待验证 | 静态/数据 ABI 与纯规则已确认，但狐在真机是否确实发现、追捕并杀死兔、0.35 概率的手感与频率、与羊群 `AutoNearbyCreaturesHelp`/逃跑行为的交互、退出重进后一致性，均未在 Windows 客户端或 Android 7723 观测；按要求不提前宣称已修复。 |
| 多人联机同步 | 待验证 | 追捕/攻击结算由实体权威端（单机 Local 或主机/服务端）仿真，普通客户端不本地生成实体；但权威端捕食结果向客户端的实体状态同步、房主与加入者观感一致性未实测。 |
| Windows 游戏运行 | 待验证 | 本机存在可运行的 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测捕食行为。 |
| Android 7723 | 待验证 | 低端机上捕食 AI 的开销、可见性与稳定性需真机新世界验证；不得因编译或离线测试通过标记为兼容。 |
| 无越界改动 | 已确认 | 本阶段不新增地形/实体写入路径、不重写寻路，仅一处只读纯谓词 + 一处数据模板覆盖，符合“先独立基础功能、委托原生 Wolf AI”的方向。 |


## 0.19.0 批次2首片：向日葵作物（复用已验证作物管线，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 复用已验证作物切片 | 已确认（代码/数据） | 完全镜像本工作区已充分验证的亚麻（Flax）垂直切片结构：`SunflowerRules`（纯逻辑，无游戏依赖）+ `SunflowerPlantBlock : CrossBlock` / `SunflowerSeedBlock : FlatBlock`（自绘 atlas）+ `SubsystemSunflowerBlockBehavior : SubsystemPollableBlockBehavior`（生长轮询）+ CSV 两行 + XDB 行为模板绑 `Class`。不引入任何新的 ABI 面，只复用既有基类。 |
| ID 取证 | 已确认（枚举） | 编译单元内 `public const int Index = 9xx` 全量枚举：作物带 929..935（玉米 929-931、竹 932-933、亚麻 934-935）连续，936..994 空闲，995..999 为载具保留段。向日葵取 936（植株）/937（瓜子产物），无冲突。 |
| clean-room 取证 | 已确认（资产实测） | 反射前已从 `Content.scpak` 解出的真实原版 `Assets/Database.xml`（463,610 字节）中检索：无 sunflower、无 chicken/hen/rooster、无 bee/hive、无 apple 任何痕迹，确认批次2四项皆为 clean-room 新增，非移植。 |
| 食物语义取证 | 已确认（既有可食物对照） | 不臆造“可食”：`KellyFruitBlock` 实测仅为 `Block` 抽象子类，可食性纯由 CSV `DefaultNutritionalValue>0` 驱动（Kelly 野果/浆果 nutr 0.08..0.15、rotPeriod 120..170 为本工作区已证 Android 可跑数据）。据此向日葵种子按 cat=Food / nutr=0.12 / rotPeriod=160 / 无病害建模，与渲染路径（FlatBlock + `GetFaceTextureSlot` 覆盖）正交。 |
| XDB GUID 唯一性 | 已确认 | 新行为模板 Guid `eccfc7fa-…`、Class 参数 `6dd1c1e3-…`、Project 成员 `37706394-…` 均 `node randomUUID()` 生成、全局唯一；仅复用允许跨 xdb 重复的容器根（BlockBehaviors `00c97f0f-…`、Project `85023bf8-…`）。`Test-Database.ps1` 通过（DATABASE VALIDATION PASSED）。 |
| 纯决策（离线可测） | 已确认（净8 无游戏依赖） | `tests/SCIEPort.Sunflower.Tests` 33 断言：DATA 位 0..7 往返、越界钳制且保外位、成熟阈值、生长间隔（底 8、水/氮各 −2、冷 ≤4 +8、暖 ≥12 −2，并断言“冷减速 > 暖加速”这一温季作物签名、间隔下限 1）、光照阈值（向日葵全日照 `MinLightToGrow=10` 严格高于耐阴亚麻 8 且 ≤15）、放置需顶面土壤、收获 4 食籽 + 1 可重种籽、权限闸 `!isClient`。 |
| 自绘图集 | 结构已确认 | `New-SunflowerAssets.ps1` 生成 `Textures/Sunflower.png`：512×512、Format32bppArgb、16×16 格 32px；slot 0..7 生长阶段（绿芽→高茎→顶端黄花盘棕心）、slot 8 瓜子堆，其余全透明（cross-face alpha test）。打包断言：0..8 每格非空、slot 12 全透明。真机采样/开花渲染/图标 待验证。 |
| 打包校验 | 已确认 | `Build-NetMod.ps1` 新增：跑 Sunflower 测试；requiredEntry 加 `Sunflower.csv/xdb/png`；XDB 断言 `SCIEPortSunflowerBehavior` 继承原生 pollable 基 `fefb9590-…` 且 `Class` 绑定子系统类、Project 成员挂该行为 Guid；CSV 断言恰一行植株（Crops/可放/slot0）+ 一行瓜子（Food/可放/slot8/nutr>0）；atlas 逐格像素校验。 |
| 编译产物 | 已确认 | `Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0（`TreatWarningsAsErrors` → 0 warning / 0 error），54 条目，DLL `[AssemblyName]`/`FileVersion`=`0.19.0.0`、`ProductVersion`=`0.19.0+5b793cf`。二进制内确含 `SunflowerPlantBlock/SunflowerSeedBlock/SubsystemSunflowerBlockBehavior/SCIEPort.Features.Sunflower/SunflowerRules`。产物 `.netmod` 417,793 字节，SHA-256 `C08B52F2ECB9E8E31C6A5C1E1EA7A9B79ABDD6E4BAE69B4A36606763EB58D8C6`（磁盘复核一致）。 |
| Windows 游戏运行 | 待验证 | 本机有可运行 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测：种植、逐阶段生长、成熟收获（可食籽 + 可重种籽）、吃掉种子回饥饿、存读档、卸载均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、种植交互、生长轮询、收获与退出重进恢复均需真机新世界验证，不得因编译或离线测试通过标记为兼容。 |
| 多人联机 | 待验证 | 轮询已在 `WorkType.Client` 直接返回，仅权威端推进；真实同步一致性未实测。 |
| 无越界改动 | 已确认 | 本阶段严格限定在 SCIE-Port 内新增向日葵切片，未改其他 mod、未吞异常、未动既有作物/生物行为，符合“按阶段推进、每片独立提交”。 |

## 0.20.0 批次2第二片：鸡生物基础验证（复用已验证生物切片管线，手动生成器阶段，静态/离线）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 复用已验证生物切片 | 已确认（代码/数据） | 完全镜像本工作区已充分验证的狐/兔垂直切片结构：`EntityTemplate` 覆写 7 组件（Creature/Body/Locomotion/Health/Spawn/HerdBehavior/FourLeggedModel）+ clean-room COLLADA 六骨骼模型 + 32×8 四色 PNG + `ChickenSpawnBlock : Block`（把模型网格 AppendModelMeshPart 进 BlockMesh 作创造图标方块）+ `SubsystemChickenSpawnBlockBehavior : SubsystemBlockBehavior`（`OnUse` 经 `SubsystemCreatureSpawn.SpawnCreature(string,Vector3,bool)` 生成）。不引入任何新的 ABI 面，只复用原生四足模型/食草 AI。 |
| 原生 AI 继承 | 已确认（静态） | `SCIEPortChicken` 继承原版 Alpaca 食草动物模板 GUID `8e62c6e4-a544-4c09-a3fb-59fd37d5b998`（LandAnimal 后代），复用原生四足步态 + 吃拾取物/群居/逃跑 AI，与兔的复用路径一致。鸡的 `Category` 因此为 `LandOther`，天然落入狐 `ChaseBehavior.AutoChaseMask=LandOther` 的捕食目标集，与生态阶段 synergy 一致（本阶段仅记录该数据层结论，实际追捕仍待真机验证）。 |
| 四足 rig 适配 | 已确认（离线导入） | 鸡仅两条真实腿：保留原生强制六骨骼（Body/Head/Leg1-4）完整性——前两条骨骼挂可见腿、后两条挂靠后收起的短腿，视觉读作鸟 stance。`Build-NetMod.ps1` 用目标 `Engine.Media.ModelData.Load` 实测导入 `Chicken.dae` 为 6 bones/10 meshes 并逐骨骼名（Body/Head/Leg1-4）校验；Y_UP 断言。 |
| clean-room 模型/贴图 | 已确认（资产实测） | 项目自制 `New-ChickenAssets.ps1` 生成 `Models/Chicken.dae`（10 立方网格，每网格 24 顶点/12 三角，无蒙皮/控制器/导入动画，纯程序化步态）与 `Textures/Chicken.png`（32×8、四色不透明：白身/红冠髯/橙喙腿/暗尾；U 值 0.125/0.375/0.625/0.875 选列）。`Build-NetMod.ps1` PNG 断言 32×8、每像素 A=255、恰 4 种 ARGB。 |
| ID 取证 | 已确认（枚举） | 编译单元内 `public const int Index` 全量枚举：作物带止于 937（向日葵瓜子），938..994 空闲，995..999 载具保留段。鸡生成器取 938，无冲突；`Creatures.csv` DisplayOrder 取 911（兔 910 之后）。 |
| CSV 行为绑定 | 已确认 | `ChickenSpawnBlock;SCIE 鸡生成器;Spawner Eggs;SCIEPortChickenSpawnBehavior;911;…;scieport-chicken-spawner;…`，由 Node 脚本按列名从已验证的兔行派生（禁手抄 CJK，`鸡` 字直接取自 `Creatures.xdb` 已提交 DisplayName），UTF-8 无 BOM、CRLF；`Build-NetMod.ps1` 断言恰一行 `ChickenSpawnBlock`、Behaviors/Category 正确。 |
| XDB GUID 唯一性 | 已确认 | 新实体 `SCIEPortChicken`、行为 `SCIEPortChickenSpawnBehavior`（继原生 pollable/behavior 基 `fefb9590-…`）与 Project 成员各用 `node randomUUID()` 生成的全局唯一 Guid；仅复用允许跨 xdb 重复的容器根（Entities `5fcdf35f-…`、BlockBehaviors `00c97f0f-…`、Project `85023bf8-…`）。文件内 Guid 无重复、CR=LF=126 一致。`Test-Database.ps1` 通过（DATABASE VALIDATION PASSED）。 |
| 权限闸（联机） | 已确认（代码/规则） | `SubsystemChickenSpawnBlockBehavior.OnUse` 在 `CommonLib.WorkType == WorkType.Client` 时直接返回不生成，避免普通客户端本地制造未同步实体；与兔一致。真实主机/客户端同步效果待联机实测。 |
| 自然生成 | 不支持（本阶段） | 仅创造栏手动生成器（`Spawner Eggs` 类）。`SubsystemChickenSpawnBlockBehavior.Load` 刻意**不**调用 `CreatureWorldSpawn.SetProject`，不注册 `CreatureType`、不接地形生成 Hook——鸡暂不进入既有狐/兔自然生成 spawner，留待基础加载/存读档实机验证后再单独成阶段。 |
| 跨切片互动（喂籽/下蛋） | 不支持（本阶段） | 不接线鸡吃向日葵/亚麻籽、下蛋等行为；本阶段只做「可手动生成的鸡实体」这一独立基础功能，食性/繁殖留作后续增强，避免臆造 ABI。 |
| 编译产物 | 已确认 | `Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0（`TreatWarningsAsErrors` + `GenerateDocumentationFile` → 0 warning / 0 error），54 条目（含 `Assets/Models/Chicken.dae`、`Assets/Textures/Chicken.png`），DLL `[AssemblyName]`/`FileVersion`=`0.20.0.0`、`ProductVersion`=`0.20.0+2151279`（SourceRevisionId 取构建时 HEAD）。二进制内确含 `ChickenSpawnBlock/SubsystemChickenSpawnBlockBehavior/SCIEPort.Features.Creatures`。产物 `.netmod` 421,326 字节，SHA-256 `246CFD35A066D90BAE2116E293FAD4A320D9540BD45E668C991A450F434EEDC9`（磁盘复核一致）。 |
| Windows 游戏运行 | 待验证 | 本机有可运行 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测：图标方块渲染、使用生成、行走/群居/逃跑 AI、碰撞、死亡与存读档均未运行。 |
| Android 7723 | 待验证 | 图标、模型、UV、程序化步态、碰撞、生成与退出重进恢复均需真机新世界验证，不得因编译或离线导入校验通过标记为兼容。 |
| 多人联机 | 待验证 | 生成已在 `WorkType.Client` 直接返回，仅权威端生成实体；真实同步一致性未实测。 |
| 无越界改动 | 已确认 | 本阶段严格限定在 SCIE-Port 内新增鸡切片（块/行为/CSV 行/XDB 实体与行为/资产生成脚本/构建校验/版本号），未改其他 mod、未吞异常、未动既有作物/生物行为，符合「按阶段推进、每片独立提交」。 |

## 0.21.0 批次2第三片：苹果结果实植物基础验证（复用作物管线 + 野果结果位范式，静态/离线）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 架构取证与选型 | 已确认（取证后决策） | 用户选定「复用结果实植物范式（低风险）」：苹果 = 成熟前按茎叶作物分阶段生长（size 落 DATA 低 0..3 位，仿已验证亚麻/向日葵）+ 成熟后按 Dedicated 结果位承载可摘果实（DATA bit 3，仿已审计 `WildBerryPlantBlock` 的 `GetFruiting/SetFruiting` 与季节日驱动 + 破坏结果丛掉果）。完整 sapling→树干体因需未反编译的私有 `Game.SubsystemSaplingBlockBehavior`/Saplings ParameterSet 本阶段明确**不支持**，避免臆造 ABI。 |
| clean-room 取证 | 已确认（资产实测） | 反射前已从 `Content.scpak` 解出的真实原版 `Assets/Database.xml` 检索：无 apple 任何痕迹，确认苹果为 clean-room 新增，非移植。ID 枚举确认作物带止于 937（向日葵）、938 鸡生成器、939/940 空闲、995..999 载具保留段；苹果植株取 939、果实取 940，无冲突。 |
| 温带作物签名 | 已确认（纯逻辑差异） | 非向日葵换名：苹果为温带多年生——基础生长间隔 10（慢于一年生向日葵 8，木本），水/氮各 −2，**冷（≤2）+6 硬冻停摆**，但普通温暖**不加速**（区别于向日葵暖 ≥12 −2 的加速签名）；`MinLightToGrow=10` 全日照；仅秋结果季且植株成熟方结果（`ShouldBearFruit = 成熟 && 结果季`）。 |
| 食物语义 | 已确认（既有可食对照，不臆造） | 可食纯由 CSV `DefaultNutritionalValue>0` 驱动（沿用 Kelly 野果 nutr 0.08..0.15/rotPeriod 120..170 已证 Android 可跑数据带）。苹果果实按 cat=Food / nutr=0.13 / rotPeriod=160 建模，与渲染路径（FlatBlock + `GetFaceTextureSlot` 覆盖）正交；成熟结果丛掉落 `AppleBlock×2` + 可重种 stage-0 植株 `×1`。 |
| 纯决策（离线可测） | 已确认（net8 无游戏依赖） | `tests/SCIEPort.Apple.Tests` 49 断言：DATA 位 0..7 往返、越界钳制且保结果位、结果位 8 往返、结果位与 size 相互独立、成熟阈值、生长间隔（底 10、水 8、水+氮 6、冷 16，并断言「温带签名=热==温和、冷 > 温和」与「冷减速 > 暖加速」对照、下限 1）、光照阈值（==10 且 >耐阴亚麻 8、≤15）、结果季门（成熟&&季）、放置需顶面土壤、收获 2 果 + 1 可重种株、权限闸 `!isClient`。 |
| 自绘图集 | 结构已确认 | `New-AppleAssets.ps1` 生成 `Textures/Apple.png`：512×512、Format32bppArgb、16×16 格 32px；slot 0..7 生长阶段（绿芽→幼苗→木质丛→树冠）、slot 8 结果态树冠缀红果、slot 9 单枚苹果（果身+梗+叶），其余全透明（cross-face alpha test）。打包逐格断言：0..9 每格非空（自检不透明计数 slot0=30 递增至 slot7=408、slot8=425、slot9=153、slot12=0）、slot 12 全透明。真机采样/结果渲染/图标 待验证。 |
| XDB GUID 唯一性 | 已确认 | 新行为模板 `SCIEPortAppleBehavior` Guid `d7c6e2aa-…`、Class 参数 `81e1072b-…`、Project 成员 `9ca07594-…` 均 `node randomUUID()` 生成、全局唯一；仅复用允许跨 xdb 重复的容器根（BlockBehaviors `00c97f0f-…`、Project `85023bf8-…`）。曾修一处手抄遗留：Project 成员 `InheritanceParent` 初指废弃 Guid，已全量替换为 `d7c6e2aa-…` 并复核 match。`Build-NetMod.ps1` XDB 校验 + `Test-Database` 通过（DATABASE VALIDATION PASSED）。 |
| CSV 结构修复 | 已确认 | `Apple.csv` 初生成时缺 79 列英文表头且为 LF；已 Node 从已验证 `Sunflower.csv` 取表头（纯 ASCII）前置到两数据行、改写 UTF-8 无 BOM、CRLF，复核 CR=行数=3、每行 79 列。Build 断言恰一行植株（Crops/可放/slot0）+ 一行果实（Food/可放/slot9/nutr>0 经 `[double]::TryParse`）。 |
| 打包校验 | 已确认 | `Build-NetMod.ps1` 新增：跑 Apple 测试；requiredEntry 加 `Apple.csv/xdb/png`；XDB 断言 `SCIEPortAppleBehavior` 继承原生 pollable 基 `fefb9590-…` 且 `Class` 绑子系统类、Project 成员挂该行为 Guid；CSV 断言植株/果实两行各恰一且类别/slot/nutr 正确；atlas 逐格像素校验 0..9 非空 + slot12 透明。 |
| 编译产物 | 已确认 | `Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0（`TreatWarningsAsErrors` + `GenerateDocumentationFile` → 0 warning / 0 error），`.netmod` zip 实测 **59 条目**（含本切片新增 `Assets/Blocks/Apple.csv`、`Assets/Database/Apple.xdb`、`Assets/Textures/Apple.png`；构建脚本 requiredEntry 校验清单为 57 项，其余为 `modinfo.json`/`SCIEPort.dll`/`THIRD-PARTY-NOTICES.md` 及未列入逐项校验的既有资产），DLL `[AssemblyName]`/`FileVersion`=`0.21.0.0`、`ProductVersion`=`0.21.0+8eac0a4`（SourceRevisionId 取构建时 HEAD）。二进制内按命名空间+裸类型名分别确含 `SCIEPort.Features.Apple` 与 `AppleRules/ApplePlantBlock/AppleBlock/SubsystemAppleBlockBehavior/AppleTextures/ApplePlacement/GetAppleGrowthInterval/ShouldBearFruit/GetAppleYield/MinLightToGrow`（连写全名探测为假阴，已按元数据分离存储法复核；同名 sunflower sanity 通过证探测有效）。产物 `.netmod` 425,632 字节，SHA-256 `30C62468FF6D58EE3734BABD6AB3AEEF57AED013658F040E82923E4DD37DE038`（磁盘复核一致）。 |
| Windows 游戏运行 | 待验证 | 本机有可运行 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测：种植、逐阶段生长、成熟转结果季、摘果/掉果、吃掉苹果回饥饿、存读档、卸载均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、种植交互、生长/结果轮询、季节判定、收获与退出重进恢复均需真机新世界验证，不得因编译或离线测试通过标记为兼容。 |
| 多人联机 | 待验证 | 轮询已在 `WorkType.Client` 直接返回，仅权威端推进生长/结果；真实同步一致性未实测。 |
| 无越界改动 | 已确认 | 本阶段严格限定在 SCIE-Port 内新增苹果切片（Rules/Blocks/行为/CSV/XDB/图集脚本/构建校验/版本号），未改其他 mod、未吞异常、未动既有作物/生物行为，符合「按阶段推进、每片独立提交」。 |

## 0.21.1 联机权限加固与全工程联机静态审计（静态代码审计；运行时同步待真机验证）

本节是对整个 `.netmod` 做一次联机维度的静态取证，并把发现的所有「改世界 / 生成实体」路径统一收敛到「仅权威端可写」的模型。这是行为加固、非新增内容，版本按补丁位 0.21.0→0.21.1。

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 权威模型取证 | 已确认（真实程序集反射） | 带 AssemblyResolve 反射 `Survivalcraft.exe` 取到 `Game.NetWork.WorkType`：`Local=0`（单人，权威）、`Server=1`（主机/服务端，权威）、`Client=2`（普通联机客户端，非权威）。故「改世界」只允许在 `Local` 或 `Server`；`Client` 必须让位。目录内存在 `LiteNetLib.dll`+`MessagePack.dll`，证实 SCNET 为真 client/server 架构，非 P2P 对等同步。`WorkType` 三成员同时被本工程代码编译引用（`TreatWarningsAsErrors` exit0），成员存在性由编译证明、非臆测。 |
| 已确认原本即安全（既有闸） | 已确认（代码） | 狐/兔/鸡创造生成器 `OnUse` 已 `WorkType.Client → return true`；生态学自然生成 `CreatureWorldSpawn.TrySpawnNaturalCreatures` 已 `CanAuthoritativeWorldMutation(WorkType==Client)` 闸 + 世界种子确定性 + 每区块预算；5 个作物 `OnPoll` 生长/结果轮询已 `WorkType.Client → return`。这些是先前切片就正确的路径。 |
| 本次新补的对称权限闸（12 处） | 已确认（代码/构建） | 修复审计发现的不对称：① 5 作物 `OnNeighborBlockChanged`（Apple/Sunflower/Corn/Flax/Bamboo）的「根部无土自毁 `DestroyCell`」此前无闸，现补 `WorkType.Client → return`（void 早退），与既有 `OnPoll` 闸对称；② 6 载具创造生成 `OnUse`（ATV/越野/蒸汽船/飞艇/轨道车/坦克）此前调 `SpawnCreature` 无闸，现补 `WorkType.Client → return true`（消费点击、不在本地造未同步实体），镜像生物生成器语义；③ 铁轨自动连接 `SubsystemRailBlockBehavior.OnBlockAdded → UpdateShape → ChangeCell` 为同类裸奔地形写，`Client → return`。各文件补 `using Game.NetWork;`。Release 构建 0 warning / 0 error，`DATABASE VALIDATION PASSED`。 |
| 重载具驾驶联机 | 不支持（联机） | `ComponentTankDrive.Update`、`ComponentRailVehicle`、`ComponentAirshipDrive` 控制回路开头是 `WorkType != Local → return`，即真正的联机局（主机是 `Server` 非 `Local`）里坦克/轨道车/飞艇**开不动、瞄不了、打不出**，仅单人有完整驾驶。此为工业载具移植期的既定范围，明确标注为联机不支持，而非未测。 |
| 轻载具驾驶联机 | 待验证 | `ComponentAtvDrive`/`ComponentOffroadCarDrive`/`ComponentSteamBoatDrive` 用 `WorkType.Client → return`、`Server` 仍运行并含 `IsMainPlayer` 判定，`ComponentTwoSeatManager.IsAuthority = Local||Server`——代码是联机感知的，但远程客户端输入→主机仿真→回同步这条链未经主机+客户端实测，不标已确认。 |
| 上游 Kelly 植物 neighbor 反应 | 未纳入本次改动（同类待评估） | `KellyPlantBehaviors` 的 `OnNeighborBlockChanged`/自毁/干燥推进沿用上游原始逻辑，本次对称加固**未**覆盖，属与作物同类的潜在客户端重复写地形风险；因是上游移植且无实测，单列为后续联机阶段待评估项，不在本次宣称范围。 |
| 部署一致性约束 | 已确认（机制） | SCNET netmod 为主机+客户端各自加载：同一 `SCIEPort.netmod`（0.21.1）必须**主机与每个加入客户端都安装且同版本**，否则未注册方块/实体 ID 在缺失端解析失败。此为 netmod 固有要求，非本工程可选优化。 |
| 运行时联机同步 | 待验证（真机） | 静态审计只能证明「权限闸在不在、代码是否只在权威端写」，证明不了「同步包实际行为」：作物 DATA/生长随区块更新回同步、实体生成回同步、`OnUse`/`OnNeighborBlockChanged` 到底在哪些端触发、载具物理跨端一致，均需主机+普通客户端两台实测。 |
| Windows 游戏运行 | 待验证 | 本机有 `Survivalcraft.exe`（x25.01.17），但本阶段未开联机服实测单机/主机/客户端三档。 |
| Android 7723 联机 | 待验证 | 目标平台真机联机（7723 联机版）未测，不得因编译或静态审计通过标记为兼容。 |
| 无越界改动 | 已确认 | 本阶段严格限定为联机权限加固：只增对称 `WorkType.Client` 闸（不改任何功能语义、不吞异常、不动既有正确闸），未新增/删除内容，未跨工程引用，产物随版本位递增单独重建并复核 SHA。 |

### 联机三档实测清单（待执行，兑现「一起验证」）

1. 单人（Local）：种 5 作物逐阶段生长→成熟→（苹果秋季结果）摘取→根部无土时自毁；手动生成狐/兔/鸡；自然生成狐兔；放置并驾驶各载具。基线正确性。
2. 主机（Server）：以上全部在主机侧执行一次；额外验证坦克/轨道车/飞艇按当前设计「联机不可驾驶」是否符合预期，ATV/越野/蒸汽船主机侧可驾驶。
3. 普通客户端（Client，第二设备同版本加入）：验证 ① 客户端点作物生成器/载具生成器不再本地造幽灵实体（应无反应或仅收到主机回同步结果）；② 作物自毁只由主机触发、客户端收到一致销毁、不出现双毁或残留；③ 铁轨形状两端一致；④ 存读档与退重进后自定义 DATA 一致。任何一项异常记录到本节并降级为「不支持」。

## 0.23.0 Stormhammer 正式资源、渲染与技能（静态/离线）

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 目标资源 ABI | 已确认 | x25.01.17 `Engine.Media.ModelData.Load(Stream)` 实际导入 `Stormhammer.dae` 为 1 bone / 30 meshes；COLLADA 1.4.1、Y_UP、triangle/normal/单套 TEXCOORD 均通过。`SoundBufferReader` 实际声明 wav/ogg；纹理沿用已验证 PNG `Texture2D` 路径。 |
| 正式模型 | 已确认 | 原创 1368 三角面，独立主锤头、双打击面、银钢中脊、8 暗金护角、双雷纹面板、双六边雷晶、连接轴、握柄/112 段螺旋带/4 固定环、尾端配重/尾晶；6 个挂点元数据逐名校验。非临时方盒或纯增加面数。 |
| UV/贴图/音频 | 已确认 | 1024² 主图集含 8 材质区、显式逐三角 UV、磨损/拉丝/高光/皮纹/晶体分面；1024² glow mask、256² effects atlas；三段原创 44.1kHz PCM16 WAV。构建检查尺寸、UV 对应、抽样色数、RIFF/WAVE 签名。 |
| 四种展示接线 | 已确认（代码） | `InventorySlotWidget`、`ComponentFirstPersonModel.Draw`、`ComponentHumanModel.Animate`、`SubsystemPickables.Draw` 最终均进入 `StormhammerBlock.DrawBlock`；库存偏移、第一人称、第三人称、掉落缩放来源已分别取证。实际视觉和手部贴合待运行验证。 |
| 动态视觉 | 已确认（代码） | 待机 GlowPoint 超过 2 帧未触摸即移除；蓄力脉冲上限 1/0.09s；电弧 28 粒子、冲击 38 粒子、寿命 0.12–0.55s；召雷仅视觉粒子，不调用天气雷击、爆炸或地形写入。 |
| 攻击技能 | 已确认（代码/编译） | `OnMinerHit` 仅 ID 941 生效；蓄力 ≥0.55s 后首击 + 半径 6、最多 3 跳链击，复用 `ComponentMiner.AttackBody`；Local/Server 执行伤害，Client 明确拒绝权威写入。手感/数值仍待实测。 |
| 材质限制 | 不支持 | `BlocksManager.DrawMeshBlock` 只有单 diffuse sampler；当前不能直接绑定第二 emissive 材质。专用 glow mask 已入资源加载和美术管线，运行效果以主图亮色 + GlowPoint + additive 粒子等效实现。 |
| Release / 包 | 已确认 | `Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；0 warning / 0 error；DATABASE VALIDATION PASSED；包 781,455 bytes，SHA-256 `0B2BD38528E5226802DB354D377DA92DF89130E60383EC61F4EB4E48AE799FB0`；DLL/FileVersion `0.23.0.0`。 |
| Windows 游戏运行 | 待验证 | 本轮未启动游戏；库存缩略图、第一/第三人称姿态、掉落比例、不同光照、音频与技能运行均不得标记已确认。 |
| Android 7723 | 待验证 | 真机 GPU/CPU/内存/发热、低端机帧时间、触屏蓄力可达性、遮挡和穿模均待新建可丢弃世界验证。 |
| 多人联机 | 待验证 | 尚无 Stormhammer 专用请求/VFX 网络包。普通客户端不执行召雷伤害；主机执行后生命同步与自定义粒子/音效的远端可见性需双设备实测。 |

完整 API 位置、制作结构和截图清单见 `docs/STORMHAMMER-AUDIT.md`。
# 0.24.0 苍穹之怒 V2.0（2026-10-26）

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 独立觉醒物品 ID 942 | 已确认 | x25.01.17 原生常量扫描未占用，且与 SCIE-Port 当前 ID 无内部冲突；其他 Mod 同装冲突待验证。ID 941 普通雷神之锤未替换。 |
| 觉醒锤/雷矛/贴图/图标 | 已确认 | 静态/目标导入证据：目标 Engine 实际导入 1 bone/39 meshes/1604 tris 与 1 bone/5 meshes/108 tris；UV、挂点、1024/256/2048 PNG 和三个非空图标槽由构建锁定。运行观感待验证。 |
| 三按钮 HUD | 已确认 | 代码/编译证据：源码构建守卫断言恰好三个 `SkillButton`；仅 ID 942 显示，大招尺寸大于普通按钮。Android 多点触控与缩放待验证。 |
| 技能数值与权限 | 已确认 | 代码/编译证据：雷矛 45–80/9s/4 额外链/6s 雷印；领域 5s/R5/6 per second/印记 +20%/18s；雷劫 60s/6 副雷×25/主雷中心 110/预警/3s 余震。Client 不结算，Local/Server 权威执行。 |
| 专用网络包 242 | 已确认 | ABI/代码证据：原生 0–38、SCIE-Port 240/241 后使用 242；服务端绑定发送者与实体并校验装备、序号、冷却和大招互斥。真实丢包/延迟/观察者一致性待双机验证。 |
| 天空/雾色覆盖 | 已确认 | ABI/代码证据：使用真实 `ChangeSkyColor`、`ViewFogColor` Hook，平滑淡入淡出；刻意不写全局光照缓存，不改天气、时间或地形。Android 视觉待验证。 |
| Release / 包 | 已确认 | `Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；0 warning/0 error；DATABASE VALIDATION PASSED；2,198,186 bytes；SHA-256 `7AF8909F5373B5A741CDF4943C603507CAA17FDD132ED7CC8C5A2E012A5EF013`。 |
| Windows/Android/多人运行 | 待验证 | 本轮未进入游戏或连接真机；不得从编译与静态校验外推为运行通过。 |

## 0.27.0 远端 0.26.0 与苍穹之怒合并发布（2026-10-26）

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| Git/功能合并 | 已确认 | `main` 已同步远端 `a2adeca`；保留直升机 V0.4、载具激活密码包及全部远端改动，同时加入 ID 941/942 雷锤资源与包 242。加载器与打包清单均保留双方注册项。 |
| 目标 ABI/环境 | 已确认 | `Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'` exit 0；目标程序集哈希、2.4.0.0、API 1.42、SC 2.4.10.6、net472 与现有 Hook/网络 ABI 检查通过。 |
| Release / 包 | 已确认 | 系统 SDK 9.0.314 执行 `Build-NetMod.ps1` exit 0；0 warning/0 error；全部离线规则测试与数据库校验通过；包 81 条目、2,287,868 bytes，Version/AssemblyVersion `0.27.0`/`0.27.0.0`，SHA-256 `497F5A94B6D5A2D755937236D68A4A6D9848A67464622059F957F2813932E4D9`。 |
| Android 7723 / Windows 游戏 / 多人联机 | 待验证 | 未启动游戏、未连接 Android 设备或多人主机/客户端；编译、资源导入和静态校验不外推为运行兼容。 |

## 0.28.0 苍穹之怒 V3.2（2026-10-26）

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 原生伤害与受伤 ABI | 已确认 | 目标 `ComponentMiner.AttackBody` 的真实顺序为 `AttackBody` Hook → `ComponentClothing` → `ComponentLevel.ResilienceFactor` → `ComponentHealth.AttackResilience` → `Injure`；`ComponentHealth.Attacked(ComponentCreature)` 在权威有效结算后提供攻击者。目标程序集没有元素伤害枚举。 |
| 普攻双段/反伤实现 | 已确认（代码/编译） | 物理 40 走原生全链；雷电 25 与反伤 5 使用 `Injure(power/AttackResilience)`，尊重无敌、死亡、权威端和内在韧性，但明确绕过衣甲/等级韧性。延迟雷电段、攻击去重与反伤递归闸已编译；实际扣血待运行。 |
| 两技能 HUD | 已确认（静态/编译） | 构建守卫断言 `new SkillButton(` 恰好两处，并拒绝旧 `ThunderLance/ThunderDomain/雷狱天矛/雷霆王域`；按钮为雷霆万钧与万象雷劫。Android 多点触控待验证。 |
| 雷霆万钧 | 已确认（代码/编译） | 权威半径 9，HashSet 单次去重，0–4 格 110、4–9 格线性到 70，冷却 12；客户端只播放 8/10/12 条确定性放射电弧、分叉、爆心与双层雷波。实际范围/伤害待运行。 |
| 万象雷劫 | 已确认（代码/编译） | 冷却 55；权威中心 24 格快照最多 24 个存活目标；目标雷 130，主雷中心 220/外围 100/半径 7，余震 18×4。玩家目标服从 FriendlyFire，洞穴遮挡不排除。数值与联机待实测。 |
| 联机演出协议 | 已确认（ABI/编译） | 0.28.1 将雷锤演出迁到专属包 243，包 242 继续专用于载具激活；载荷含 `TargetId/CastId/ServerTime`，并按施法/阶段/目标/步骤去重。联机双方必须统一升级到 0.28.1；真实丢包、延迟与观察者行为待双机验证。 |
| 人物/武器/天空渲染 | 已确认（ABI/编译） | 使用真实 `OnModelRendererDrawExtra`、HumanModel 六骨骼、`EmissionColor`、`SubsystemGlow`、`ChangeSkyColor`、`ViewFogColor`；未发现 Bloom、动态灯、云层材质或安全镜头震动 API，未伪造。实际视觉待 Android/Windows 运行。 |
| 高中低与清理 | 已确认（代码/编译） | High/Medium/Low 控制挂点、电弧间隔、粒子和远雷；GlowPoint 三帧过期，卸装/死亡恢复 emission。断线/切世界实际清理待运行。 |
| Release / 包 | 已确认 | `Test-Environment.ps1` 与 `Build-NetMod.ps1` exit 0；0 warning/0 error；所有既有离线规则测试和数据库校验通过；81 条目、2,292,859 bytes，Version/AssemblyVersion `0.28.0`/`0.28.0.0`，SHA-256 `B261BC031C8B26B8CBA0D463F2F81457F7D87EEF45AEB35D710B53290A19D66B`。 |
| Windows/Android/多人运行 | 待验证 | 未启动游戏、未连接 Android 或多人会话；伤害、反伤、HUD、骨骼雷光、天空恢复和性能均不得仅凭编译标记运行通过。 |

完整限制与真机步骤见 `docs/SKYWRATH-V3.2-AUDIT.md`。

## 0.28.1 V3.2 加载修复（2026-10-26）

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 整包物品消失根因 | 已确认 | Windows x25.01.17 实际启动日志复现：`VehicleActivationPackage` 与 `StormhammerSkillPackage` 同占 ID 242，第二次 `PackageManager.RegisterPackage` 抛出“数据包存在冲突”，中断 DLL 初始化；随后所有 CSV 因 Block 类型未注册而失败。 |
| 修复 | 已确认（源码） | 载具激活保留 242，苍穹之怒迁移至 243；构建脚本扫描全部 `*Package.cs` 并拒绝重复 ID，环境检查保留段扩展到 240–243。 |
| 方块 ID 合并冲突 | 已确认 | 修复包首次真实加载又发现直升机与普通雷神之锤同占 941；为保留已发布直升机存档，直升机继续使用 941、苍穹之怒继续使用 942，普通雷神之锤迁移到 943。构建脚本现在拒绝任何重复 `Block.Index`。旧测试版中写入为 941 的雷神之锤无法与直升机无歧义区分，不能自动迁移。 |
| Windows 加载 | 已确认 | 将 0.28.1 包临时部署到 x25.01.17 `NetMods` 后真实启动到主菜单；日志确认 81 资源、DLL、全部 XDB/CSV 正常加载，无“数据包存在冲突”或“BlocksData 中没有找到”错误。测试后恢复原安装包。 |
| Android 7723 | 待验证 | 用户已确认 0.28.0 整包物品消失；0.28.1 尚待重新导入确认。 |
