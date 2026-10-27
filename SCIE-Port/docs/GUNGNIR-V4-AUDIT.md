# 昆古尼尔 V4 开发记录（未达到正式交付标准）

日期：2026-10-27。分支：`codex/gungnir-v4`。基线：远端 `2270179`；版本暂定 SCIE-Port `0.31.0`。

**本记录不是 Android/联机兼容验收报告。** 用户告知雷神之锤最新代码已在另一 agent 完成但未同步；当前无法访问那份工作区。完整构建曾发现远端遗漏 `tests/SCIEPort.Stormhammer.Tests/SCIEPort.Stormhammer.Tests.csproj`；项目所有者随后明确要求本次直接合并发布、不运行该测试。该例外只影响雷神之锤回归证据，不改变昆古尼尔自身测试结果。

## 真实源码审查

取证对象为本机开发者提供的 `D:\SC`，公开 Windows x25.01.17：

- Survivalcraft.exe SHA-256 `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`。
- Engine.dll SHA-256 `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`。
- EntitySystem.dll SHA-256 `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`。
- 反编译只放在忽略的 `artifacts/gungnir-audit/`，不得提交游戏源代码或程序集。

| 审查项 | 实际证据及实现影响 |
| --- | --- |
| 注册/资源 | `Mod/SCIEPortLoader.cs` 注册包及 Hook；Stormhammer 的 CSV/XDB、`ContentManager.Get<Model/Texture2D>`、`BlockMesh.AppendModelMeshPart` 已存在。昆古尼尔独立目录/资源，ID 948，包 ID 244；环境检查已扩展原版冲突检查。其他 Mod 占用仍待验证。 |
| 原生长矛 | 实际行为类是 `Game.SubsystemThrowableBlockBehavior`，不是 `SubsystemSpearBlockBehavior`。`OnAim(InProgress)` 设置手臂/第一人称变换；Completed 调 `FireProjectile` 并在权威端 `RemoveSlotItems`。本武器自有行为不调用该库存移除路径。 |
| 普攻 | `ComponentMiner.Hit` 固定限频 `0.6600000262260437` 秒，`ComponentPlayer.Update` 自带 2 格限制。自有输入适配在 -5 顺序（Input=-10，Player=0）消费 Hit，以命令交由权威端重新射线检测 3.5 格及遮挡；0.66/1.5 或 0.66/2 限频。原生重复近战伤害通过 AttackBody Hook 归零。 |
| 伤害 | 45 物理走 `ComponentMiner.AttackBody` 的护甲/韧性；20 神圣走 `ComponentHealth.Injure`，不绕过无敌；友伤先检查世界设置。神佑仅放大本枪近战/投枪，不放大裁决。 |
| 移动/跳跃 | `ComponentLocomotion.WalkSpeed/JumpSpeed` 是真实属性；跳跃还乘 `1 + .25*(SpeedFactor-1)`。`ComponentBody` 重力为 10，4 格理论冲量 `sqrt(80)`，补偿等级因子；30/60/120 Hz 纯数值测试峰值约 4 格。不是游戏实测。 |
| 重要权威限制 | `ComponentBody.Update` 在 Server 上仍用远程玩家的 `netPosition/netVelocity`；原生 `ComponentPlayerPackage.BodyUpdate` 写这些值。当前仅实现服务器授予增益、客户端执行原生移动，**不满足严格服务器位置/跳跃物理权威**。需要另行设计与验证原生运动协议适配，不能将属性包同步冒称权威仿真。 |
| 碰撞/返回 | `SubsystemBodies.Raycast`、`SubsystemTerrain.Raycast`、真实区块状态；单次 HashSet 去重最多 8 目标，35 格/秒、40 格，回归45格/秒，不结算回程伤害。库存始终保留一件，无移除/返还/掉落实体生成；服务端飞行状态禁止再投与近战。 |
| HUD/冷却 | 沿用实际 Canvas/Rectangle/Label/Clickable API，独立两按钮，服务器快照同步冷却/剩余持续。按钮上方居中，最终 Android 避让与多触点尚需实测。 |
| 展示路径 | 第一/第三人称通过实际 `m_drawBlockEnvironmentData` 引用识别玩家，离手隐藏；不是按距离猜测玩家。图标及掉落仍用同一网格。 |
| 特效 | `IDrawable` + `PrimitivesRenderer3D.FlatBatch` 连续四边形带、平面符文和完整三维枪影；三层圆环、1.2秒凝聚+0.2秒落枪、三道冲击波、余辉。复用已有画质/震动设置；没有地形写入或光球阵。 |

## 当前资源（原创，仍需游戏内艺术验收）

`scripts/New-GungnirAssets.py` 生成双翼刃、真正菱形截面与刃缘倒角、古金护翼、金白能量脊、12边暗银枪杆、螺旋缠线、箍环、双面符文和独立尾部配重。

- `Assets/Models/Gungnir.dae`：单骨骼、41网格、1176三角形，Y_UP，显式法线/UV。
- `Assets/Textures/Gungnir.png`：1024²；`GungnirSkills.png`：512×256双技能图标。
- `Assets/Audio/Gungnir/Invoke.wav`、`Impact.wav`：原创 PCM16/44100Hz 单声道。
- `docs/images/gungnir-model-preview.png`：实际网格的软件预览，不是游戏截图。
- DAE SHA-256 `AB77463E2F6802304A2A76DD917CDBFE67C78F6E68CC6892EA5FAF941DE8D129`。
- 主纹理 SHA-256 `60FB2EA154130E47F10179BC5F7E0289DEF818D8A688EA1752195D4B2CEC23F2`。

## 实际验证结果

| 命令 | 结果 |
| --- | --- |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir D:/SC` | 已确认：通过；根脚本使用 ReflectionOnlyLoadFrom，必须 Windows PowerShell 5.1，PowerShell 7 初次运行不支持该接口。 |
| `SCIE-Port/scripts/Test-Environment.ps1 -GameDir D:/SC` | 已确认：固定程序集哈希、ABI、原版 ID/包 ID 冲突检查通过。 |
| `SCIE-Port/scripts/Test-GungnirAssets.ps1 -GameDir D:/SC` | 已确认：真实 Engine.ModelData.Load 导入 1骨骼/41网格/1176面，UV/资源/CSV守卫通过。 |
| `SCIE-Port/scripts/Test-Database.ps1` | 已确认：全部 XDB 校验通过。此脚本不接受 GameDir 参数，已按真实参数重跑。 |
| `$env:DOTNET_ROLL_FORWARD='Major'; & "$env:LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" run --project SCIE-Port/tests/SCIEPort.Gungnir.Tests -c Release` | 已确认：88项纯规则检查通过，包含重放/溢出序列、输入洪泛限频、跳跃数值积分、伤害边界。未设置 roll-forward 的初次运行因本机仅有 .NET 9 runtime 无法启动 net8 测试，设置后通过。 |
| `& "$env:LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" build SCIE-Port/src/SCIEPort/SCIEPort.csproj -c Release -p:SCNetGameDir=D:/SC --nologo` | 已确认：net472，0警告/0错误。首轮发现的 ClickableWidget.Size/MathUtils.TwoPi 不存在，已按实际 API 修正后重编译通过。 |
| `SCIE-Port/scripts/Build-WithPinnedSdk.ps1 -GameDir D:/SC` | 首次因缺少Stormhammer测试工程而失败；项目所有者明确要求移除该门槛后重新执行。昆古尼尔、Kelly、载具、生态、作物、激活、堡垒测试、Release编译、数据库及包内容校验全部通过并生成`.netmod`。 |
| Windows游戏、Android7723、主机/客户端 | 待验证：均未运行。 |

最终发布包：`SCIEPort.netmod`，3,027,538字节，SHA-256 `ADE009B962DE2935320554B6DB0ACD9212351B7C522A4A767F70AFB76B8B1793`；包内程序集版本与`modinfo.json`版本均为`0.31.0`。

## 正式交付前必须完成

1. 后续取得另一 agent 的未提交雷神之锤代码及缺失测试工程后，应补跑雷神之锤回归；本次发布按项目所有者指令豁免该项。
2. 实现并验证服务器对远程玩家运动/跳跃的实际权威控制。现有增益授权同步不能替代此项；不要发布为满足全部联机要求的正式版。
3. 当前属性恢复只减去本模块增量，可保留其他模块的加法变化；对第三方在任意帧覆盖绝对属性或乘写属性不能保证无冲突，需要协同修饰器机制/实测。当前冷却是世界会话内状态，跨退出重进持久化未实现。
4. 第一人称目前适配原生 Poke 与投掷姿势，专门刺击曲线、神枪返回插值平滑、沿枪杆真正流动的符文以及全身柔光还未完成最终表现；不能以现有脉动镶嵌、移动短带和环绕符文冒充所有美术条款已经完成。
5. 大招余辉目前是离散符文上升，完整枪影逐段分解尚需完善；全画质、昼夜、缩略图/手部比例/遮挡必须截图复核。软件模型预览和多边形数量不是正式美术验收。
6. 单次贯穿、重放包、丢包/延迟、死亡/断线/换装/传送/未加载区块/异常恢复必须游戏内验证。目前纯规则测试不覆盖游戏行为类及网络传输。

## Android可执行验收步骤（全部待验证）

取得完整构建并完成上述实现后，两台目标7723设备安装同一SHA-256的 `.netmod`，备份存档并新建可丢弃世界。记录客户端精确版本、程序集指纹、分辨率、画质、双方日志及录像。

1. 创造栏取 ID948。录制库存、掉落、第一/第三人称正侧背；昼/夜各一次。检查枪尖厚度、UV、双翼、尾部、握持点及发光，确认不是简陋模型。
2. 等级/负重/地面相同，空手与持枪各跑20格、各连续攻击30秒；装备后的速度/有效命中频率应为基准150%。飞行、骑乘不受加成。
3. 神佑15秒内重复计时，应为基准200%而非225%；记录最高脚底高度约4格，45+20及120/90伤害变为1.5倍，裁决不变。35秒冷却从激活起算。
4. 直线放置至少9个目标，末端建实墙；长按并松开原生攻击，记录一次投枪只伤前8个不同目标、第一120后续90、穿透不停止、墙前返回、回程无伤害、飞行中再次操作无第二把枪。扣除护甲影响后核算伤害。
5. 25格内对地面施放裁决：1.2秒预警、三层圆环、完整三维巨枪、落地三层冲击波；7格内300到150衰减，7格外无伤，55秒冷却，仅一次结算，地形逐格不变。
6. 投枪中换槽、丢弃/转交、死亡、断线、重连、传送、进入区块边界及世界退出，分别检查全库存/掉落总数不增不减、无永久加成、无残留效果。服务器与客户端均执行。
7. 1280×720/1920×1080及高DPI横屏，同时按移动、原生攻击和技能按钮；只存在两个技能按钮，不遮挡原生操控及物品栏；非持枪隐藏。
8. 主机施法、普通客户端施法和旁观者分别录像；注入延迟/重复请求，记录服务器实际命中次数、冷却、去重和运动裁定。检查雷神之锤完整回归。
9. 高/中/低画质连续战斗10分钟，记录帧耗时、峰值内存、温度、特效实体清理，确认低档仍有巨枪与三层符文形状。

## 修改文件清单

- `src/SCIEPort/Features/Gungnir/`：Rules、Package、Subsystem、Block、Scene、Hud 六个独立文件。
- `src/SCIEPort/Assets/`：上述模型、双纹理、双音频、Gungnir CSV/XDB。
- `src/SCIEPort/Mod/SCIEPortLoader.cs`：独立包注册、双按钮更新、原生重复伤害防护。
- `src/SCIEPort/SCIEPort.csproj`、`modinfo.json`：0.31.0候选版本。
- `scripts/New-GungnirAssets.py`、`Test-GungnirAssets.ps1`、`Test-Environment.ps1`、`Build-NetMod.ps1`。
- `tests/SCIEPort.Gungnir.Tests/`：纯规则测试工程/入口。
- 本记录、预览、README、兼容性与测试记录；根蓝图清单只标注非建筑物品不可导入，不改变 Builder Assistant 能力集合。
