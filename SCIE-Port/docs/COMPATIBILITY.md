# SCIE-Port 兼容性记录

## 2026-10-30 main 0.56.2：昆古尼尔数值调整

纯数值调整，不改 Hook、方法签名、包布局与存档字段：卢恩神佑冷却 6 秒、持续 9 秒；命运裁断改为每目标 10~100 随机（权威端掷点，客户端不参与）；永恒裁决改为固定 341。包 244 字段未变。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 数值与随机边界 | 已确认 | 386 项 Gungnir 规则通过，含 10/100 边界、越界夹紧与 341 平伤。 |
| Android 7723 / 联机 | 待验证 | 未实机运行；随机伤害与 341 的实际观感需用户验收。 |

## 2026-10-30 main 0.56.1：雷神之锤大招缩减与雷域

纯数值与结算路径调整，不改 Hook、方法签名、存档字段与包布局：雷霆王域每跳 17 点；雷神降世窗口内技能冷却按 0.625 缩短；窗口内每秒 7 点雷域伤害（8 格、最多 12 跳、结束帧补齐后关闭）。新增 `StormhammerSkillEvent.UltimatePulse=17`，`StormhammerSkillPackage`(243) 字段未变。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 王域 tick 修复 | 已确认 | 根因是活动实体集合在迭代中被修改；改为先快照再结算，规则测试 751 项通过。 |
| 冷却缩减与雷域数值 | 已确认 | 8→5、24→15、7 点/8 格/12 跳离线断言全部通过。 |
| Android 7723 / 联机 | 待验证 | 未实机运行；雷域观感与 HUD 冷却刷新需用户验收。 |

## 2026-10-30 main 0.56.0：妙尔尼尔与昆古尼尔第三技能

两件神器各新增一个主动技能，原有 Hook、方法签名、存档字段、方块 ID（942/943/948）与包 ID（243/244）全部不变。

- 昆古尼尔：`GungnirPackage`(244) 只在**尾部追加** `FatalismCooldown:float`，前面所有字段顺序与语义不变；新增 `Kind=4` 事件（Phase 1 起手 / Phase 2 结算，`Blessing` 携带命中数）。命令 5 为新增，1~4 不变。伤害仍走 `ComponentMiner.AttackBody`。
- 妙尔尼尔：`StormhammerSkill.Dominion=3` 追加在枚举尾部，`StormhammerSkillEvent` 追加 14/15/16；`StormhammerSkillPackage`(243) 字段未改，复用 `Value/Step/TargetId`。冷却数组由 3 槽扩到 4 槽。
- 新增纯规则命名空间 `Features/Targeting/CreatureTargetRules`，用原生 `CreatureCategory` 位标志判定敌对，已进入两个测试工程。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 技能数值与去重规则 | 已确认 | 离线纯规则：Gungnir 381 项、Stormhammer 728 项全部通过，覆盖 79/39、12/7/8/24、7 跳、30 目标去重与友军排除。 |
| Release 构建与包内容 | 已确认 | 构建 exit 0，图标槽位与三按钮守卫通过，见 [TEST-RESULTS.md](TEST-RESULTS.md)。 |
| 混版（0.55.x ↔ 0.56.0） | 不支持 | 244 尾部新增字段、243 新增事件值，双方必须同为 0.56.0。 |
| Android 7723 / 真实联机 | 待验证 | 未实机运行；触控、演出与断线清理需用户验收。 |

## 2026-10-30 main 0.55.5：神器默认属性与攻速口径统一

新增规范：神器默认持有移速 +20%、近战攻速 +10%、专属物品栏背景（见根 `AGENTS.md`）。本版把两件现有神器补到该基线，不改 Hook 名、方法签名、包布局或存档字段。

- 移速：仍是 `ComponentLocomotion.WalkSpeed` 自有增量。海神陆地 +20%、游泳 +500%（两档互斥，先归还旧增量再重算）；雷神之锤把 `HoldsHammer` 扩到 942/943，普通形态也享受 ×1.20。
- 攻速：沿用原生的 `ComponentMiner.m_lastHitTime` 前移口径（`0.66*(1-1/frequency)`）。`OnMinerHit` 中武器倍率与 `SubsystemFourSaints.AttackFrequency` **相乘**；四圣兽的 1.1 只在觉醒锤装备时由 `FourSaintsRules.AttackFrequency` 计入，因此不会与武器倍率重复叠乘。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| `WalkSpeed` / `m_lastHitTime` 写入 | 已确认 | 固定 x25.01.17 程序集字段可写；`m_lastHitTime` 已由既有 Stormhammer 实现验证。 |
| 陆地 +20% / 攻速 +10% 规则 | 已确认 | 3397 项纯规则与布局检查（离线），含两条移速增量的 1000 帧不累积断言。 |
| 手感与叠加 | 待验证 | 与四圣兽攻速、载具、其他神器的组合手感未实测；水中/陆地切换阈值为浸水 >0.5。 |
| Android 7723 与联机 | 待验证 | 未启动设备或真实多人世界。 |

## 2026-10-30 main 0.55.4：海神数值倍率与领域持续伤害

纯数值与判定调整，不改 Hook、方法签名、包布局或存档字段：水中伤害倍率 2→3（水盾吸收倍率仍为 2，两者已拆成独立常量）、水中移速 +200%→+500%（仍是 `WalkSpeed` 自有增量）、二技能巨浪 8×4→15×8、大招领域由"全向圆盘一次命中"改为"水龙卷环带（半径 4.5–11.5 格、高 10 格）每秒 52 点持续伤害"。

`ComponentMiner.AttackBody` 与 `ComponentBody.ApplyImpulse` 的调用方式未变，持续伤害只是对同一目标按秒重复调用，不新增持久实体、不写地形；`Cast.Ticks` 为纯内存结构，随技能结束释放。网络包 248 的 78 字节布局与字段语义不变（快照只描述领域剩余时间/原点/方向），客户端不需要额外字段。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| 倍率与环带判定规则 | 已确认 | 2402 项纯规则/布局检查（离线），含水中三倍、每跳 52/156、环带内外沿与 72 方位等价。 |
| Release 构建与包内容 | 已确认 | 环境/ABI、net472 Release、全部既有回归与包校验通过，见 [TEST-RESULTS.md](TEST-RESULTS.md)。 |
| 持续伤害节奏手感 | 待验证 | 1 秒/跳、52（水中 156）在实机上是否过强需用户确认；数值集中在 `PoseidonRules` 常量，可直接调。 |
| Android 7723 与联机 | 待验证 | 未启动设备或真实多人世界；高速游泳（×6）是否穿墙需实机确认。 |

## 2026-10-30 main 0.55.3：海神三技能全向判定与玩家水中移速

`ComponentLocomotion` 的实际移动分支已复核：`WalkSpeed` 是玩家（含水中）唯一读取的移动速度，`SwimSpeed` 只在 `SwimSpeed>0 && SwimOrder.HasValue` 时生效，而 x25.01.17 中只有 `ComponentPilot` 写入 `SwimOrder`，玩家输入链路从不设置，因此对玩家写 `SwimSpeed` 不会改变移动。0.55.3 起海神的 +200% 水中移速改以 `WalkSpeed` 独立增量实现，同时保留 `SwimSpeed` 增量给 AI 路径；两者均可读写，ABI 检查已覆盖。这是设计缺陷修复，不涉及 Hook 名、方法签名、包布局或存档字段变更。

领域命中判定由朝向扫掠改为纯半径 `InVortex(radial,height,radius)`，覆盖四面八方；半径上限 18 格、高 10 格，候选查询以领域中心为圆心。地形视线、区块加载、友伤开关、命中集合与 0.08 秒查询间隔不变，网络包 248 的 78 字节布局与快照字段不变。

| 项目 | 状态 | 证据与边界 |
| --- | --- | --- |
| `ComponentLocomotion.WalkSpeed` / `SwimSpeed` 可写 | 已确认 | 固定 x25.01.17 程序集属性可读写；反编译 `NormalMovement` 证实玩家路径只消费 `WalkSpeed`。 |
| 全向领域判定与演出 | 待验证 | 2320 项纯规则/布局检查（含 72 方位等价性）与 Release 构建通过；未在设备上目视确认覆盖面与遮挡观感。 |
| Android 7723 游泳提速 | 待验证 | 需实机确认浸水 >0.5 时约为原速 3 倍、浅滩不加速、反复出入水无叠加。 |

## 2026-10-30 main 0.55.2：移除星门 XmlWriter 依赖

0.55.1 的用户日志进一步确认 `XmlWriter.WriteStartElement(string)` 缺失；仅替换 Create 重载不足。0.55.2 的保存路径改为固定 XML v1 文本编码，处理引号、尖括号、& 和属性中的制表符/换行/回车；名称校验使用 XML 1.0 字符规则。成品守卫禁止注册表及其辅助方法调用 XmlWriter、XmlWriterSettings、XmlConvert。

读取改用 `XmlReader.Create(TextReader, XmlReaderSettings)` + `XElement.Load(XmlReader, LoadOptions.None)`，由本机固定 `EntitySystem.dll` 的 `XmlUtilities.XmlUtils.LoadXmlFromTextReader` 反编译证实是原生解析链。只使用同一方法已调用的设置 setter；启用字符校验，解析前限制 32768 字符并拒绝 DTD，再检查版本、64 个记录上限、坐标和链接。Windows 证据不能替代 Android 程序集证据。

`VoidPortalRecords`、XML v1、WorldId、星门 ID/所有者/坐标/链接、XDB GUID、方块 ID 和网络布局保持。解析放在禁止内联的调用边界内；遇到 MissingMemberException（含 MissingMethodException）或 TypeLoadException 时沿用“不支持”路径，保留原文并停用星门，避免此类解析错误中断世界加载。不吞保存异常，不部分加载或清空记录。Android 7723 实机进入/保存/退出/重进与真实联机 **待验证**；实际构建记录见 [TEST-RESULTS.md](TEST-RESULTS.md)。

## 2026-10-30 main 0.55.1：Android 星门存档 XML 重载修复（不完整，已由 0.55.2 替代）

用户 Android 日志已确认旧 `XmlWriter.Create(StringBuilder, XmlWriterSettings)` 在其运行时缺失。改用 `StringWriter` 包装缓冲区并调用 TextWriter 重载；`VoidPortalRecords`、XML v1、WorldId、星门 ID/所有者/坐标/链接、XDB GUID、方块 ID 和网络包布局均保持。

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 旧包问题签名防回归 | 已确认 | 成品 IL 守卫在原 0.55.0 DLL 上实际失败，拒绝 StringBuilder 重载；Release 构建集成同一守卫。 |
| Windows 固定 x25.01.17 构建与存档隔离检查 | 已确认 | 环境/ABI、完整 Release/包校验通过；成品 IL 为 TextWriter 重载；星门152项及原生夹具890项通过，实际 Save 空/正常/损坏记录路径均覆盖。 |
| Android/7723 世界保存、退出与重进 | 待验证 | 已消除用户报告的缺失方法引用；未取得手机 System.Xml 程序集或操作真机，不能宣称整个客户端已兼容。 |
| 联机 | 待验证 | 本次未改协议、权威或 ID；真实主机/普通客户端保存与切世界需同版复测。 |

产物 7,339,987 字节/143项，SHA-256 `BA23942419BCA039939F106E7FFE7140F3D84DB61DDB951FB149AA0D88F9BF7A`；对比0.55.0仅DLL和modinfo变化。详见 [测试记录](TEST-RESULTS.md)；保留非法星门 XML 原文的既有行为。

## 2026-10-30 main 0.55.0：海神三叉戟 V2

水御潮环15秒/14攻击力/刷新5点盾；方向巨浪11秒/44；海神领域60秒、20秒、55，领域内前两技能7/5秒。伤害与有效护盾水中翻倍，普通近战也翻倍，水中游速为原值3倍。蓝色世界采用限时天空/雾色与原生视口叠色，未修改地形或天气。

| 项目 | 状态 | 实际证据与边界 |
|---|---|---|
| Windows x25.01.17 ABI | 已确认 | 固定2.4.0.0/net472程序集指纹；受伤Hook、IsInvulnerable、DrawOverlay、SwimSpeed、圆形Widget及原生压缩器实际检查通过。 |
| 模型、图标与包 | 已确认 | 原模型配色调整，Engine导入仍1根/83网格/1504三角形；3张image_gen原图及RGBA256运行图有提示词/哈希记录，原生PNG解码、透明角和包内精确哈希通过。 |
| 规则与编译 | 已确认 | 2004项规则/布局检查，66个真实协议字段往返、队列生命周期与原生圆形命中通过；完整Release/既有回归/21 XDB及包校验通过。 |
| 网络布局 | 已确认 | 246旧41字节布局保留但旧入口停用；新增独立248包78字节，目标39个原生包及本工程无内部ID冲突；主机与客户端必须同版0.55.0。 |
| 游戏内/Android7723/真实联机 | 待验证 | 未启动游戏或自动操作设备；水盾伤害链、蓝色世界强度、技能动作/浪形、多指和左右手、远端快照与帧率需用户实测。 |

产物7,339,972字节/143项，SHA-256 `7ED64ECD7DEFC87094F41ED2892F9CECA3ABB6244C3BF06E94FF681F7FB8E304`。Block951/DATA0、XDB GUID、已有包240–247布局、Builder Assistant实现保持；新增技能未引入真实水方块。规则与设备步骤见SCIE-Port/docs/POSEIDON.md。

## 2026-10-29 main 0.54.0：阿比斯圆形技能与挥镰

| 项目 | 状态 | 证据 / 限制 |
| --- | --- | --- |
| Windows x25.01.17 呈现 ABI | 已确认 | 固定 Survivalcraft.exe 2.4.0.0（SHA-256 `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`），`Widget.HitTest(Vector2)` 虚方法、`Subtexture`、第一/第三人称位移/旋转命令经反编译及 Release 验证。圆形控件实际命中变换运行隔离夹具，未运行游戏。 |
| 资源与身份 | 已确认 | 新增 imagegen 四格 `Assets/Textures/AbyssSkillsV1.png`，目标 PNG 解码和包哈希检查；武器 DAE/调色 PNG、ID、GUID、持握 CSV、三连斩状态机保护检查通过，无新增 Hook 或网络包。 |
| 游戏内触屏、视野与性能 | 待验证 | 左右手、各分辨率在离线规则上检查；真实 Android 7723 及游戏画面未测。无法安全排布时隐藏技能，避免覆盖原生控件。 |
| 联机 | 待验证 | 单人/本地主机普通攻击新范围代码已构建，实际传播与动画需双端测；普通客户端普攻及三项新技能权威同步不支持。 |

## 2026-10-29 main 0.53.0：虚影瞬狩视听与动作

| 项目 | 状态 | 证据 / 限制 |
| --- | --- | --- |
| Windows x25.01.17 API 与打包 | 已确认 | 目标程序集 2.4.0.0、net472 Release 编译和资源校验；两个原创 `Assets/Audio/Abyss/Blink{Shift,Hunt}.wav` 经目标 Engine 解码、包内哈希比对。无新增网络包、ID 或 Hook。 |
| 单人与本地主机 | 待验证 | 移动与伤害规则不变；新增短时动作锁、线框残影、裂隙和弧光，实际触屏、动画、声音和性能未运行游戏验证。 |
| 普通客户端及 Android 7723 | 待验证 | 普通客户端技能同步不支持；未核验 7723 程序集或运行双端实测。 |

## 2026-10-29 main 0.52.0：终焉坍缩音效

| 项目 | 状态 | 证据 / 限制 |
| --- | --- | --- |
| Windows x25.01.17 音频接口与资源 | 已确认 | SCNET Windows 2.4.0.0、net472 Release 编译使用 `SubsystemAudio.PlaySound`；新增 `Assets/Audio/Abyss/Horizon{Wake,Rift,Collapse,Burst}.wav`，目标 Engine `SoundData.Load` 和包内哈希校验通过。 |
| 单人及本地主机音效和战斗 | 待验证 | 纯规则覆盖阶段一次触发、跳帧与静默；未运行游戏或听取实际音量。音频异常由呈现层捕获，不作为伤害条件。 |
| Android 7723 与普通客户端 | 待验证 | 未取得或验证 7723 程序集，真实设备与联机听感待测试；普通客户端的终焉技能同步仍不支持。 |

## 2026-10-29 main 0.51.0：终焉坍缩视觉预览

Windows x25.01.17 目标程序集 Release 编译及现有绘制 API 调用 **已确认**；未新增 Hook、网络包、物品 ID 或资源格式。游戏内渲染、性能与 Android 7723 **待验证**。普通客户端的新技能同步 **不支持**。

## 2026-10-29 main 0.50.0：星门目的地区块加载

星门单人/本地主机在目的地未加载时申请半径 32 的临时原生更新位置，等待最多 10 秒；仅在目标周边区块均有效且落点、碰撞及领地检查通过后移动。取消时同时处理排队中的添加和已应用的位置；成功后保留 3 秒，供原生玩家视角接管。现有已加载目的地仍可在私有 ABI 不匹配时使用；无法申请时安全拒绝。星门传送同时检查四圣兽动作锁。

| 项目 | 状态 | 证据 / 限制 |
| --- | --- | --- |
| Windows 固定 x25.01.17 API | 已确认 | 编译与原生隔离夹具验证 `TerrainUpdater.SetUpdateLocation`、`UpdateLocations` 和私有 `m_pendingLocations` 的排队/取消/外部冲突；无新增 Hook、网络包或方块 ID。 |
| 实际远距离加载与释放 | 待验证 | 隔离夹具未运行地形工作线程或游戏世界；Android 7723 重打包程序集、生成速度、切世界与内存表现必须实测。私有字段不符时主动加载安全拒绝。 |
| 普通客户端新技能 | 不支持 | 远程玩家位置权威纠偏、请求/状态及特效同步仍未实现；断界斩既有网络路径仍待实测。 |
| 设备与 V1 完整演出 | 待验证 | 单机/主机、Windows 游戏内和 Android 7723 未测试；三、四技能视觉与音效仍未完成。 |


## 2026-10-29 main 0.49.0：阿比斯技能开发预览整合

合入 `codex/abyss-v1` 的断界斩和四技能本地开发预览，保留主线紫曜巨镰 A3、四圣兽强化、全部已注册神器与独立分类。断界斩新增包 247；已有包 240–246、物品 952、GUID、持握参数和模型不变。复用主线天空 Hook，将阿比斯与妙尔尼尔、昆古尼尔天空合成；断界斩请求也检查四圣兽动作锁。新增星门存档字段和 XDB 上限/画质参数。

| 项目 | 状态 | 证据 / 限制 |
| --- | --- | --- |
| Windows 固定程序集、Release、包检查 | 已确认 | x25.01.17 / net472，环境、原生接口、A3 模型导入、资源/分类、离线测试和包校验通过。 |
| 单人、本地主机四技能预览 | 待验证 | 游戏内输入、星门存读档及传送、攻击、画面与性能未实测；视觉和音效尚不完整。 |
| 普通客户端新技能、远距离星门主动加载 | 不支持 | 尚缺权威位置纠偏、客户端状态同步和安全加载/释放；断界斩既有客户端协议仍待实际联机验证。 |
| Android 7723 / 完整 V1.0 | 待验证 | 7723 包程序集和设备尚未测试；本包是开发预览，并非 V1.0 完成版。 |


## 2026-10-29 main 0.47.0 / 阿比斯 V0.2

在 main `6b33598` 合并 port `dcbe228`，保留 main 0.46.0 全部功能/品牌/打包守卫与 Builder Assistant 0.19.0。
新增主线方块 950（吞噬之眼）、952（阿比斯），现有 949/951、衣物 203/200–203、网络包 240–246、GUID/存档身份不改。
两个新物品均使用独立神器 Tab 和专属主题。未新增 Hook 或网络包；既有 GuiUpdate/UpdateInput 合并接入，
阿比斯仍通过真实 Input+1=-9 子系统更新采样，四圣兽锁定时取消攻击。

| 项目 | 状态 | 证据/限制 |
|---|---|---|
| Windows x25.01.17 目标 | 已确认 | 下列历史登记三程序集 SHA-256 未变；本轮 Test-Environment 重新比对通过 |
| 阿比斯 A3 原生离线导入 | 已确认 | 1 identity bone / 32 meshes / 9,260 triangles，拓扑/UV/不透明色板/资源精确哈希检查通过 |
| net472 Release / 包内容 | 已确认 | 4,010,467 bytes / 133 entries，SHA-256 `192704A9DE6D2A2E25549EFF65A660F3DF6708366B179CF40FA6206A9E22B26E`；21 XDB、22 CSV/55 行、全套离线检查通过；命令见 TEST-RESULTS.md |
| 阿比斯普通客户端战斗 | 不支持 | 没有客户端自定义战斗请求协议，不结算客户端伤害 |
| 吞噬之眼回收主线坦克 | 不支持 | 主线坦克包含端口五组件白名单以外的组件，保守拒绝，未扩大白名单 |
| Android 7723 / Windows 游戏内 / 联机 / A3 性能 | 待验证 | 本轮未启动游戏或操作设备；旧运行版截图与离线模型验收不能替代 A3 实测 |

独立遗迹工程是本次用户已发起合并中的新增目录，保留原样，不打包进 SCIEPort；其历史兼容性不由本轮继承。

## 0.46.0 合并 origin/main（四圣兽 V3）与本地直升机 HUD 主线（离线已确认，Android/双机待验证，2026-10-28）

本地 main（含直升机 HUD 合并，原 0.45.0）与 origin/main 新增提交 `5d36821`（四圣兽 V3 独立装甲/骨骼附件/装备图标/装备页人物预览修复，原 0.44.1）自 `6fb084e`（v0.44.0）分叉。执行 `git merge origin/main` 后冲突集中在元数据与变更日志：`SCIEPort.csproj`、`modinfo.json` 版本统一升到 **0.46.0**（modinfo 与 csproj 一致以满足构建守卫），Description 合并两条主线；`README.md`、`COMPATIBILITY.md`、`TEST-RESULTS.md` 保留双方历史条目并新增 0.46.0 说明。四圣兽 V3 源码/资源（`Features/FourSaints/FourSaintsArmorRenderer.cs`、`QinglongCrownRenderer.cs`、`FourSaintsClothingPreview.cs`、`ClothingBlock.cs`、`FourSaintsVisuals.cs`、`Assets/Models/*V3.dae`、`Assets/Textures/FourSaintsArmorV3.png`/`QinglongCrown.png`、相关脚本与文档）与直升机 HUD 源码（`Features/AirVehicles/Hud/*` 等）由三方自动合并，无文本冲突；`Build-NetMod.ps1`、`Test-Environment.ps1` 自动合并双方新增守卫。

合并后离线复核（本机 `D:\SC`）：`scripts/Test-Environment.ps1 -GameDir 'D:\SC'` exit 0，同时通过 “Helicopter V0.5 per-finger touch and coordinate-transform ABI” 与 “Native clothing-preview ModelWidget.Draw, bone arrays, shader and owner/child replacement ABI”（四圣兽 V3）；`scripts/Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release` exit 0，`DATABASE VALIDATION PASSED`、独立 Tab 守卫（20 CSV / 53 行）、专用分类 CSV/命名守卫（53 行）全部通过。产物 `artifacts/荒野探险家.netmod` 与字节一致的 `荒野探险家-0.46.0.netmod`：**3,742,075 bytes / 125 entries**，包内 `SCIEPort.dll` AssemblyVersion/FileVersion=`0.46.0.0`、modinfo Version=`0.46.0`、PackageName=`SCIEPort`，SHA-256 `385DEAAA0A0797A683598CE0A32410ED0A035DB205B2F4A3704A8C9E262BA6F0`；包内同时含直升机资产与四圣兽 V3 资产（`BaihuLegArmorV3/XuanwuArmorV3/ZhuqueBootsV3/QinglongCrown.dae`、`FourSaintsArmorV3/QinglongCrown.png`），确认两条主线内容均在合并包中。方块/衣物索引、GUID、网络包与存档字段两侧均未改，存档与联机身份保持兼容。Windows 游戏内、Android 7723（直升机 HUD 触控全项 + 四圣兽 V3 装备页人物预览/装甲实显）与两台真实客户端联机仍待真机验证。

## 0.45.0 合入直升机移动端飞行控制 HUD（离线已确认，Android/双机待验证，2026-10-28）

将长期分支 `feature/helicopter-hud`（基线 `a2adeca`，v0.27.x 时代）合并进 main（v0.44.0）。冲突集中在元数据与变更日志：`modinfo.json`/`SCIEPort.csproj` 一律保留 main 的品牌契约（Name=荒野探险家、PackageName/AssemblyName/RootNamespace=SCIEPort），丢弃分支旧的 “SCIE-Port 移植集合 (Audit Build) / 0.28.0”，版本统一升到 **0.45.0**（modinfo 与 csproj 一致，满足构建守卫）；`README.md` 取 main 现行品牌版并新增 0.45.0 条目；`COMPATIBILITY.md`/`TEST-RESULTS.md` 保留双方历史条目。直升机 HUD 代码（`Features/AirVehicles/Hud/*`、`HelicopterFlightInput.cs`、`HelicopterHoverController.cs`、`ComponentHelicopterDrive.cs`、`HelicopterFlightRules.cs`、`Helicopter.xdb`、`Test-HelicopterHud.ps1`、`HELICOPTER-HUD-V0.5-AUDIT.md`）由三方自动合并，无文本冲突。

功能：右下角独立上升/下降按键（持续多点触摸总距、松手平滑回中、自动定高悬停、紧凑座舱遥测、落地滑橇锁），仅本地授权驾驶位显示，复用既有双座驾驶权限与原生父 Body 一元位置同步，经 `ResolveIntent` 归一后走既有 `GetVerticalTarget`/`IntegrateVerticalSpeed`，绝不直写实体坐标。最终 HUD 为纯程序化矢量控件（`BevelledRectangleWidget`/`ImageWidget`/`ClickableWidget` + 自持全屏 `CanvasWidget m_layer` 定位层），**不依赖任何外部贴图**（早期 v0.27.x 的 `HeliArrow.png` 方案已被分支 tip 取代，合并后代码与包均无该引用）。方块/衣物索引、GUID、网络包与存档字段未改，存档与联机身份保持兼容。

验证（本机 `D:\SC`）：`scripts/Test-Environment.ps1 -GameDir 'D:\SC'` exit 0（含新增 “Helicopter V0.5 per-finger touch and coordinate-transform ABI” 检查）；`scripts/Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release` exit 0，net472 **0 warning / 0 error**，直升机 HUD 源码守卫、720 条分类/配色断言、独立 Tab（20 CSV / 53 行）守卫、既有全套离线规则测试、`DATABASE VALIDATION PASSED` 全部通过。产物 `artifacts/荒野探险家.netmod` 与字节一致的 `荒野探险家-0.45.0.netmod`：**3,672,324 bytes / 119 entries**，modinfo Version=`0.45.0`、DLL AssemblyVersion/FileVersion=`0.45.0.0`，SHA-256 `A677BAA5F6486B0E2064B167CE6C11D4A3DA4189762BAD68C2C24B27AEBF5A50`。

Windows 游戏内、Android 7723 触控（按钮命中/持续按压/双手多点与左摇杆并用/松手悬停/换驾驶员无残留/多分辨率与 UIScale 安全区）、以及两台真实客户端联机（仅授权驾驶员出键、乘客隐藏、垂直输入经主机校验随一元位置包同步）**均待验证**；离线编译与规则测试不能替代真机手感。详见 `HELICOPTER-HUD-V0.5-AUDIT.md`。

## 0.44.1 主线合并（2026-10-28）

将四圣兽V3独立装甲、常驻骨骼附件、网格图标及装备页人物预览修复合入 main 0.44.0；保留全知之眼、波塞冬、独立分类及主题背景。合并衣物块 DrawBlock 与 GetCategory，不改变衣物203/200–203、既有属性技能、碰撞及协议。历史0.42/0.43四圣兽候选版本号与同时期主线神器版本号曾重叠，最终统一为0.44.1。Android7723及联机由用户验收，仍待验证。

## 0.44.0 独立 Tab、全神器/四圣兽背景、中文产物名（2026-10-28）

53 条 CSV 配置归入 `SCIEPort.*` 独立分类键，通过目标实际 `BlocksManager.AddCategory`、
`LanguageControl.loadJson` 注册并显示九类中文 Tab。使用已有 BlocksInitalized Hook，
在圣兽衣物兜底注册后执行，Hook 签名与注册次数不变。普通衣物保持 Clothes/Dyed，
仅衣物块 203 内索引 200–203（含颜色/损耗 DATA）迁入 SCIEPort.Saints。
统一背景只使用目标原生 InventorySlotWidget.CenterColor/BevelColor，不增加 UI 覆盖层；
五件现有神器与四件圣兽分别配色，未知未来神器有通用兜底但正式发布需专属主题。
主题切换/普通槽/隐藏或移除 Widget/世界切换恢复原色和旧引用。

所有 Block ID、衣物 DATA、GUID、网络包及 modinfo 字段集合不变；版本为 0.44.0。
外部新产物 `荒野探险家.netmod` / 同字节 `荒野探险家-0.44.0.netmod`，内部
PackageName/AssemblyName/RootNamespace/SCIEPort.dll 不变。旧包历史不重命名。
固定 Windows x25.01.17 ABI、720 条纯规则断言、160 项真实分类/翻译/幂等/颜色 ABI
检查、既有套件与 Release/包内容检查已确认；Windows 游戏内/Android 7723/联机 UI 待验证。
见 [规范与真机清单](INVENTORY-STANDARD.md) 和测试记录。

## 0.43.0 波塞冬 V1（2026-10-28）

新增独立 `PoseidonBlock` 951（保留 port 分支吞噬之眼 950），网络包 246；
固定 Windows x25.01.17 260 个原生方块常量和 39 个原生网络包 getter 均已审计，
本工程全部 Block/Package ID 无冲突。外部 Mod 冲突仍待逐包核验。
模型真实 Engine 导入 1 bone / 83 meshes / 1504 triangles，Y_UP/烘焙矩阵/不透明 PNG。
使用已核验 `ComponentHealth.Air`、`ComponentLocomotion.SwimSpeed`、
`ComponentBody.ApplyImpulse`（原生 SubsystemBodyPackage ID 6）及 AttackBody；
呼吸豁免仅原生本地化原因 `LanguageControl.Get(ComponentHealth.fName,7)`。
复用现有 GuiUpdate/OnCreatureInjure 注册，不新增或改变 Hook ABI；包 246 新布局与旧包独立，
各字段和权威流程见 [POSEIDON.md](POSEIDON.md)。客户端请求经身份绑定排队，
伤害/冲量仅 Local/Server 的 IUpdateable 执行；未引入地形写入、持久实体或新存档字段。
net472 Release、自动回归与包内容校验已确认；Windows 游戏、Android 7723 及联机待验证。

## 0.42.0 main 合入全知之眼（2026-10-28）

将功能提交 `896469b` 整合到 main 基线 `a856ced`（0.41.0），保留主线品牌、四圣兽
新名称/500 耐久/朱雀 150% 移速及体温逻辑、全部既有模块/网络包/测试守卫。
版本统一为 0.42.0；`GuiUpdate` 复用现有一次注册，在原有 HUD 调用后增加 Eye.Tick，
没有重复注册；ID 949 经主线全部编号与原生 260 常量重新检查无重复。
全知之眼仍为只读，本次没有引入分支上的其它工程或修改 Builder Assistant 实现。
真实模型导入、Release 与主线自动检查结果见测试记录；Android 7723、Windows 游戏内
界面与联机继续为待验证。下方 0.14.0 是开发分支历史记录，不是本次主线版本。

## 0.43.1 装备页立体人物预览（2026-10-28）

用户澄清“模型仍旧”发生于背包/装备页人物预览，不是世界第三人称。根因：0.43.0仅接入SubsystemModelsRenderer世界绘制，而ClothingWidget通过PlayerModelWidget/ModelWidget独立绘制UI人物，不调用该Hook。新增FourSaintsClothingPreview，从现有GuiUpdate仅替换外层人物的原生ModelWidget绘制子控件，保留宿主PlayerModelWidget、原生底层衣物、旋转/动画/皮肤/相机/布局，base.Draw后提交四件缓存装甲。复用原生UI LitShader的View/Projection和GlobalTransform/DPI投影，使用其骨骼数组，不套世界相机或距离裁剪；DrawMeshBlock执行UI投影后Flush(Matrix.Identity)，避免重复投影。内部皮肤预览保持原生语义；其他Mod自定义ModelWidget子类不被替换。新增ABI守卫检查模型控件Draw虚方法、骨骼数组、Shader、人物所有者和子控件替换接口。没有新Hook/资源路径/协议，也未改属性、碰撞、ID、几何或贴图。Windows目标ABI与Release构建检查已确认；官方x25.01.17 Android APK反编译确认同一控件/字段/绘制链路，7723真机预览与交互仍待用户验收。ComponentSAClothing加载异常已由用户确认是其他Mod造成，未用吞异常或修改存档方式处理。Git操作按用户要求停止，未创建提交/更新远程分支。

## 0.43.0 四圣兽V3整套统一验收候选（2026-10-28）

在0.42.0青龙冠之上新增XuanwuArmorV3.dae（Body）、BaihuLegArmorV3.dae（Leg1/Leg2）、ZhuqueBootsV3.dae（Leg1/Leg2）与共享不透明FourSaintsArmorV3.png。现有OnModelRendererDrawExtra接入FourSaintsArmorRenderer，ClothingBlock.DrawBlock为衣物200–203分别提供真实网格图标；无新Hook/新网络字段。新增Settings根属性SCIEPortFourSaintsArmorV3Preview（默认true）与SCIEPortFourSaintsArmorDiagnostics（默认false），原冠体设置保持。三件真实目标导入分别1/40/1212、3/36/804、3/30/596（骨骼/网格/三角面），整套3292三角面。男女Body/双Leg30次合成姿态/相机变换、原生鞋底高度/静态腿间与裙摆净空已确认；游戏内动作/穿模、Android7723与主机/客户端/观察者分别待验证，用户明确要求统一自行验收。衣物块203/数据200–203、基础贴图、属性技能、物理、GUID、包240–245均未改变。完整构建/最终包证据见TEST-RESULTS.md，细节见FOUR-SAINTS-V3-FULL-ARMOR-AUDIT.md。

## 0.42.0 四圣兽V3第一阶段青龙冠（2026-10-28）

新增 `Assets/Models/QinglongCrown.dae`、`Assets/Textures/QinglongCrown.png`；现有 `OnModelRendererDrawExtra` 接入常驻 Head 附件，ClothingBlock.DrawBlock 为数据200使用相同网格图标，其他衣物图标不变。原生衣物块203、数据200–203、.clo基础贴图、属性/技能/物理、GUID及包240–245未改。新增设置根属性 `SCIEPortQinglongCrownV3Preview`（默认true，用户要求测试包直接显示）与 `SCIEPortQinglongCrownDiagnostics`（默认false）。目标x25.01.17核心三程序集哈希与既有记录完全一致，已确认真实API、1骨骼/31网格/680三角面导入和男女Head绑定基逆变换；模型缓存、正常衣物穿戴判断和单次投影不依赖技能反馈窗口。完整Release/包检查已确认，Windows带Mod动作/穿戴、Android7723和主客机分别待验证；用户指定自行验收，未进入第二阶段。详见 [V3审查](FOUR-SAINTS-V3-CROWN-AUDIT.md) 与 TEST-RESULTS.md。
## 0.41.0 四圣兽更名·耐久统一·朱雀移速·体温钳制（离线已确认，Android 待验证，2026-10-27）

四件衣物更名为万里青龙(200)/北冥玄武(201)/镇狱白虎(202)/炽焰朱雀(203)，`Sturdiness` 统一为 500，炽焰朱雀靴 `MovementSpeedFactor` 提升到 1.5（镇狱白虎保持 1.2）。`.clo`（权威）与 `FourSaintsClothing.cs`（合并失效兜底）两条路径同步更新，`Layer` 仍统一为 10、`IsOuter=True`。方块索引 203、衣物数据号 200–203、GUID、网络包 245 与存档值均未改，因此本版对存档与联机完全兼容；改名只影响显示名，存档按方块/衣物索引存取。

新增权威侧每帧体温钳制修复“穿整套仍会因低温感冒”的缺陷：`SubsystemFourSaints.ApplyTemperature`（`UpdateOrder.Locomotion`，仅 `Authority`）调用纯函数 `FourSaintsRules.ClampTemperature`——北冥玄武把体温下限抬到 8（高于感冒发病门限 6，故不再累积 `ComponentFlu` 发病，也免疫严寒伤害索引 25/26 与结冰遮罩），炽焰朱雀把体温上限压到 8（远低于过热伤害 24），同穿时体温钉在 8。钳制通过既有公开属性 `ComponentVitalStats.Temperature`（System.Single，public get/set，经 x25.01.17 反射核实）写入，不新增 Hook、不改方法签名、不引用 `ComponentFlu.fName`、不触碰索引 35，既有构建守卫（idx25/idx26 必含、idx35/ComponentFlu.fName 必无、四件 Layer=10/IsOuter=True）继续通过。

`scripts/Test-Environment.ps1 -GameDir 'D:\SC'` 与 `scripts/Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release` 均 exit 0，编译 0 warning/0 error；`SCIEPort.FourSaints.Tests` 全绿（新增 8 条 `ClampTemperature` 断言，PASS 标签 37 条），Gungnir 306、Stormhammer/Skywrath 671 等既有套件、17 XDB、`DATABASE VALIDATION PASSED` 通过。产物 `artifacts/SCIEPort.netmod` 108 条目、3,501,301 bytes，包内 `modinfo.json` Version=`0.41.0`、DLL AssemblyVersion/FileVersion=`0.41.0.0`，SHA-256 `C75D3139FAC5AFA7A737F599A74D2ADB77777B328919A4D2E9DB1944F1461F5C`。Windows 游戏、Android 7723 创造栏/穿戴、实际体温·感冒·过热表现与双机同步仍待真机验证；离线编译与规则测试不能代替真机手感。

## 0.40.6 四圣兽衣物补注册兜底（离线已确认，Android 待验证，2026-10-27）

0.40.4/0.40.5 已修正稀疏衣物索引的空值与穿戴层级（`incoming.Layer > current.Layer`，四件统一 Layer=10），但 `.clo` 合并在 x25.01.17 里是"单个文件失败只记日志"的弱契约，一旦整份被丢弃，四件衣物的 `ClothingData` 不会注册，创造栏与世界内都看不到。本轮在 `BlocksInitalized` 增加补注册：按 200～203 逐个检查，缺失才创建，已存在则完全跳过（不覆盖 .clo 数据），每件独立 try/catch 且贴图失败会回退并记录路径。方块索引、衣物数据号、.clo 内容与能力数值均未改。Four Saints 29、Gungnir 306、Stormhammer 671、Ecology 53 及全部既有套件、17 XDB、net472 Release 与 108 条目包通过；包 3,500,765 bytes，SHA-256 `EF58D05EC73587EE1E45A8EE8896C88DC2B9C86EC91CFBE53FF7C877ABADD909`，modinfo `0.40.6`。Android 7723 创造栏与穿戴仍待验证。

## 0.40.5 四圣兽穿戴层与昆古尼尔投掷准心（离线已确认，Android 待验证，2026-10-27）

目标 x25.01.17 的 `ComponentClothing.CanWearClothing` 只在新衣物 `Layer` **严格大于**当前槽最后一件衣物时允许穿戴；玄武铠/白虎胫甲原为 Layer 4、朱雀靴原为 Layer 1，会被玩家已有的同层上衣、裤子和鞋拒绝，而通常为空的头部槽仍能穿青龙冠，形成“只有青龙冠显示”。四件现统一为 `IsOuter=True, Layer=10`，确保作为神装最外层穿戴；方块 203、衣物数据 200–203、纹理、属性、耐久、包 245 与存档值均未改。构建新增四件数量/外层/Layer 守卫。

昆古尼尔继续使用 0.40.0 的 `PlayerInput.Aim` 自建蓄力时钟和空 `OnAim`，避免恢复 1.4 秒重武装与 `m_isAimBlocked` 回归；缺失准心的原因是这条可靠路径未像原生 `SubsystemThrowableBlockBehavior` 一样每帧调用 `ComponentAimingSights.ShowAimingSights`。现只补回同一纯显示调用，不改变蓄力、发射、伤害、飞行租约或网络字段。两个技能徽记按要求移除名称铭牌/文字，保留图标、冷却数字、就绪光与底部能量条。

`Test-Environment.ps1` 对真实 `E:\SCNET\SCNETx25.01.17` 通过；Gungnir 306 项、Four Saints 29 项及全部既有离线套件、17 XDB、net472 Release（0 warning / 0 error）、108 条目 `.netmod` 校验通过。产物 SHA-256 `78ED809C55782309FC6D5C0192E57F02A43E20685AF45DAABEA0A347AFEEBE51`。Windows 构建与离线检查不能代替 Android/7723 真机：四件实际穿戴模型、触屏长按准心、连续投掷与双机行为仍为待验证。

## 0.40.4 四圣兽稀疏衣物数组修复（Windows 已确认，Android 待验证，2026-10-27）

x25.01.17 原生衣物最高索引为 37；四圣兽沿用已发布数据号 200–203 后，`ClothingBlock.m_clothingData` 的 38–199 为 `null`。目标程序集的 `GetCreativeValues()` 先执行 `OrderBy(cd => cd.DisplayIndex)`、后判断 `cd != null`，因此加载任意一件四圣兽衣物都会抛 `NullReferenceException` 并中断 `BlocksManager.Initialize`。新增同名、同方块 ID 203 的兼容子类，只覆盖创造栏枚举为“先过滤空槽、再按 DisplayIndex 稳定排序”；衣物 XML 加载、原生模型、绘制、耐久与穿戴逻辑仍由基类实现。方块 203、衣物 200–203、包 245、属性、纹理与存档值均未改。已做交叉复现：无 `.clo` 不报错，任意 200/201/202/203 均报错，低索引 38 + 自定义纹理不报错，高索引 200 + 原版纹理仍报错；0.40.4 本机 x25.01.17 冷启动不再出现空引用并到达 MainMenu。Four Saints 29 项、全部既有套件、17 XDB、net472 Release 0 warning/0 error、108 条目包通过；包 SHA-256 `89A68B89BE3EF5A8ABA56BE57696187406BE180ABF8D2F0AD1EC3557554AC441`。Android 日志已证明旧包会报错，但新包仍需真机重新进入世界确认。

## 0.40.3 `.netmod` 品牌打包契约（2026-10-27）

把「荒野探险家 / Wilderness Explorer（作者 zyan）+ 内部标识恒为 SCIEPort」固化为可执行的打包契约（`docs/PACKAGING-RULES.md`、`AGENTS.md`），并由 `Build-NetMod.ps1` 在打包前后各校验一次：显示名、作者、`PackageName`/`AssemblyName`/`RootNamespace`、csproj 与 modinfo 版本一致性、简介中英名、`THIRD-PARTY-NOTICES.md`、CSV 名称无状态后缀。`modinfo.Description` 改为中英品牌名开篇。方块索引（903–948、995–1000）、衣物数据 200–203、GUID、网络包 240–245 与存档兼容逻辑均未改，因此本版对存档与联机完全兼容。构建守卫首次运行即发现 `Watercraft.csv` 四个显示名（飞艇/铁轨/火车/车厢）带「（授权移植预览）」后缀，已移除；存档按方块索引，改名不影响兼容。完整构建通过，包 3,498,377 bytes / 108 entries，SHA-256 `73BC0D92D8E162782D1750B562FF3DB8F21A8538CA6EE3303B1EF08DD97346D3`，包内 `Name=荒野探险家`、`PackageName=SCIEPort`、`Version=0.40.3`。Android 7723 与双机仍待验证。

## 0.40.2 诊断版：关闭自然生物刷新（2026-10-27）

为定位「世界卡在加载界面」的报告，本轮把自然生物刷新整体关闭（`CreatureWorldSpawn.NaturalSpawnEnabled = false`）。它是唯一在区块生成期间执行的 Mod 代码，且一直是待验证功能。其余逻辑、方块 ID（903–948、995–1000）、衣物数据 200–203、实体模板、网络包与数值均未改动。ID 审计未发现重复：51 个方块索引与 51 行 CSV 一一对应，衣物索引远高于原版上限 37，XDB GUID 复用均为既有共享模板，CSV 的 `DisplayOrder` 908/909 重复只影响创造栏排序。Ecology 53、Gungnir 307 及全部既有套件、17 XDB、net472 Release 与 108 条目包通过；包 3,498,409 bytes，SHA-256 `84E1254BD385B9C78F107AF22BD45E1C45F1A76D7F14BFE2D19F98D58FE01B27`。确认世界可进入后需恢复该功能再验证。

## 0.40.1 自然生物刷新越界修复（离线已确认，Android 待验证，2026-10-27）

`Game.Random.Int(min, max)` 在 x25.01.17 中是闭区间，`Int(0, 16)` 有约 1/17 概率返回 16；区块数组索引为 `y + x * 256 + z * 256 * 16` 与 `x + z * 16`，于是 `GetTopHeightFast(16, 15)` 访问 `Shafts[256]`、`GetCellValueFast(16, y, 15)` 访问 `Cells[65536 + y]`，抛 `IndexOutOfRangeException`。该路径只在区块内容刚生成时执行，所以表现为「新世界进不去、旧存档正常」。同时 `Int(0, 100)` 与 `ChanceMet` 的 0..99 域不匹配，100 会被判为非法输入而静默不生成。现坐标与概率分别改用 `EcologyRules.LastChunkCellIndex`(15) 与 `LastChanceRoll`(99)，所有区块读取先 clamp 并做上下界检查；区块钩子改为只入队，实体在 `SubsystemUpdate` 中于区块状态 ≥ `TerrainChunkState.InvalidLight` 后每帧最多生成 2 只（队列上限 32），连续 5 次异常自动本会话禁用。生物模板、生成概率/上限、生态位判定、方块 ID 与网络包均未改。Ecology 53 项、Gungnir 307 项及全部既有套件、17 XDB、net472 Release 与 108 条目包通过；包 3,498,461 bytes，SHA-256 `3C4C4A721007F2B97C33D6CE2AE8DE761E4B5719A630D7DCCBED192D25833784`。Android 7723 新世界加载与联机仍待验证。

## 0.40.0 昆古尼尔技能名铭牌、投枪可靠性与天降巨枪（离线已确认，Android/联机待验证，2026-10-27）

技能名改为刻在徽记内部的 21 单位铭牌带（`GungnirHudLayout.NameBandHeight`），徽记 `Visual` 恢复为 `size × size`，因此既有 1280×720 / 1920×1080 / 1.25× / 1.5× 布局、左手镜像与 16 单位原生避让结果全部不变，名称不可能再被 DPI、安全区或快捷栏挤出可视区。投枪不再依赖 x25.01.17 `OnAim` 通路：`Update` 直接采样 `PlayerInput.Aim` 自建蓄力时钟，`OnAim` 改为空实现，`Launch` 取消相位门（飞行中再次投掷即召回旧枪），并新增 3.2 秒飞行租约看门狗与 0.15 秒最短蓄力；这消除了引擎 1.4 秒重新武装、`m_isAimBlocked` 锁与相位滞留三者叠加造成的「投几次后无法投掷」。永恒裁决改为裂穹—高空显形—俯冲—落地—消散的巨枪演出，时间轴纯函数化，1.4 秒伤害时点、9 秒生命周期、`SkyBlend/ImpactFlash` 与三档预算规则未改。包 244/245 字段、ID 948、模型/贴图、伤害 120/90/45/20、裁决 300→150、冷却 8/50 秒、移速 ×1.5/×2 与飞行 35/45 格每秒全部未改。Gungnir 307 项、Stormhammer 671 项及全部既有套件、17 XDB、net472 Release（0 warning / 0 error）与 108 条目打包通过；产物 3,497,393 bytes，SHA-256 `1F7EAE1C5EB91D4D6F82302A748E81906313D836FC984C0C821910587787A0E1`，modinfo `0.40.0`，AssemblyVersion `0.40.0.0`。Android 7723 连续投掷手感、铭牌可读性、巨枪帧率与双机联机仍为待验证。见 [V6 审查记录](GUNGNIR-V6-THROW-HEAVENLY-SPEAR.md)。

## 0.37.0 双神器操作与强化（离线已确认，Android/联机待验证，2026-10-27）

从 x25.01.17 `ComponentPlayer.Update` 与原生 `SubsystemThrowableBlockBehavior.OnAim` 反编译确认：蓄力阶段 `OnAim` 返回 `true` 会被解释为立即取消，原生投掷物在 `InProgress` 返回 `false`、仅终态返回 `true`。昆古尼尔已采用该契约，包 244 布局不变。物品栏统一由一个槽位装饰器保存/恢复原色，妙尔尼尔保持蓝紫，昆古尼尔新增暗青/古金背景，避免快速换装残色。昆古尼尔近战神圣二段延迟 0.4 秒结算并发送金色 `ComponentHealthPackage`/本地 `HitValueParticleSystem`，与原生白色首段分离；神佑/裁决冷却改为 8/50 秒。真妙尔尼尔装备时地面移动 ×1.20，原生近战门限由 0.66 秒按 ×1.10 换算为 0.60 秒；死亡、骑乘、创造飞行、换装及 Dispose 会撤销移动增量。Gungnir 150、Stormhammer 671 项及全部既有套件通过，16 XDB、net472 Release、100 条目打包通过；产物 3,469,440 bytes，SHA-256 `A99251BCC465BED727EE8D4E53AA4DAA076AED5FE47953088DE58A05A412D494`。Android 7723 长按、多点触控、两段数字、移速/攻速手感及双机联机仍为待验证。

## 0.36.0 品牌更名「荒野探险家 / Wilderness Explorer」（离线已确认，真机待验证，2026-10-27）

本次仅调整模组对外品牌展示信息，不改任何游戏机制：`modinfo.json` 的 `Name` 由「SCIE-Port 移植集合 (Audit Build)」改为「荒野探险家」，`Author` 改为「zyan」，`Description` 换成中英双语——中文段落为玩家可见的游戏内简介（拓展原版生存世界 / 生态植物 / 陆海空载具 / 特色装备 / 神话神器 / 雷神之锤与昆古尼尔），英文段落保留 v0.35.0 Gungnir V5.2 天穹审判的技术要点以便审阅追溯。`PackageName` 仍为 `SCIEPort`，`AssemblyName`/`RootNamespace` 未动，内部 ID、GUID、网络包（Gungnir 244、Skywrath 243、vehicle activation 242 等）、方块 ID（Gungnir 948 等）、存档兼容逻辑与战斗/移动数值全部未改；`THIRD-PARTY-NOTICES.md` 保留上游 SCIE、Kelly Expansion 与各位贡献者的必要版权署名。发行版本按语义化次版本号 0.35.0→0.36.0（Gungnir V5.2 功能基线不动，本次仅品牌层）；csproj `Version`/`AssemblyVersion`/`FileVersion` 与 modinfo `Version` 一致。仓库工作区目录名仍为 `SCIE-Port`，README 主标题更新为「荒野探险家 / Wilderness Explorer」并加一行 subtitle 说明内部标识保留，下方全部既有开发记录原样保留。已运行 `scripts/Test-Environment.ps1 -GameDir 'D:\SCNET'`（ABI 全通过：2.4.0.0/API 1.42/SC 2.4.10.6/net472/原生方块 ID 260-263）与 `scripts/Build-NetMod.ps1 -Configuration Release`：全部离线规则套件、16 份 XDB 校验、net472 Release 0 warning / 0 error、包内容校验通过；产物 `artifacts/SCIEPort.netmod` 3,468,486 bytes / 100 entries，SHA-256 `E502A5881472164279D19E79F1F4C8F1BB358BCECD360E99FFFABC6F440C79EE`，包内 modinfo `Version=0.36.0`，DLL `AssemblyName=SCIEPort` / `AssemblyVersion=0.36.0.0`。已从包内解出 `modinfo.json` 模拟加载器读取：UTF-8 无 BOM（首三字节 `7b 0d 0a`）、CRLF 一致（11 CR / 11 LF）、JSON 合法，中文名称 `荒野探险家` 正确解析。Android 7723 冷启动进入 Mod 管理确认可见新名称与简介、真机加载与联机仍为待验证。

## 0.35.0 昆古尼尔 V5.2 天穹审判（离线已确认，Android/联机待验证，2026-10-27）

新增九秒本地天空/雾色状态和四阶段世界演出；天空状态按 Project/施法者隔离，死亡、快照超时、玩家移除和 Dispose 清理。与妙尔尼尔雷暴采用 screen 强度合成及明确色相权重，雷暴代码/状态不被覆盖。包 244 字段及序列化未改；长按投枪在既有字段内增加开始/完成/取消信号并由服务端计时验证，修复普通客户端依赖隐式瞄准重放的偶发失败。HUD 额外矩形框删除。Gungnir 146 项、Stormhammer 667 项、全部既有套件、资源、16 XDB 和 net472 Release（0 warning/0 error）通过；包 3,468,275 bytes / 100 entries / SHA-256 `F82C8F4309BD412FB6A0866B35ED72346ECABE293FFCEA2BD12CAE1A95B20A94`，modinfo `0.35.0`，AssemblyVersion `0.35.0.0`。没有天气/时间/地形写入或额外伤害；Android 7723、白天/夜晚/雨天、性能和双机联机待验证。见 [V5.2 审查记录](GUNGNIR-V5.2-SKY-JUDGMENT.md)。

## 0.34.0 主线合并状态（离线已确认，真机待验证，2026-10-27）

主线已合并昆古尼尔 V5.1/V5.1b、妙尔尼尔 V6/展示反馈和分支历史中的便携堡垒美术更新。唯一冲突为 `modinfo.json` 描述，已保留双方能力；版本、API 与 SC 标记一致。Gungnir 130、Stormhammer 667、Portable Fortress 319 项规则检查及全部既有构建套件通过，16 XDB 通过，net472 Release 0 warning / 0 error。最终包 3,465,578 bytes / 100 entries，SHA-256 `40C79967AE3FEEFF3785F4036BC934F53030BA88A8FB006E582AA944A9C70969`，modinfo `0.34.0`，AssemblyVersion `0.34.0.0`。Android 7723、真机视觉/触控/性能和双机联机仍为待验证。

## 0.34.0 妙尔尼尔展示与反馈（离线已确认，真机待验证，2026-10-26）

| 项目 | 状态 | 结论 |
|---|---|---|
| 槽位皮肤 | 已确认（源码/守卫） | `MjolnirDisplay` 只装饰 942；首触保存/离开恢复/Dispose 还原；`InventorySlotWidget.Update` 不回写颜色已取证。 |
| 伤害数字 | 已确认（反编译取证） | `health.Injure` 不出数字；`ShowHitValue` 复刻 `ComponentMiner` 的 `ComponentHealthPackage` + `HitValueParticleSystem` 模式。 |
| 伪锤附伤 | 已确认（源码/守卫） | 固定 5 点、×1 永不增幅、延迟 0.4 s。 |
| Release 构建/包 | 已确认 | exit 全绿：667 断言、15 XDB PASS、包 3,021,714 B / 92 条目，SHA-256 `C6DDD1462E0D8B596D149EF05430A976365199F05B85C89427C5367A63526502`，modinfo 0.34.0。 |
| 真机观感 | 待验证 | 槽位脉动、两段伤害节奏、联机远程数字。 |
## 0.33.0 雷神之锤·妙尔尼尔 V6（2026-10-26，分支 `feature/mjolnir-v6`）

| 项目 | 状态 | 结论 |
|---|---|---|
| 觉醒武器造型 | 已确认（源码 + 构建守卫） | `StormhammerAwakenedBlock` 改为加载 `Models/Stormhammer` + `Textures/Stormhammer`，缩放对齐 `0.265`；守卫禁止再引用 `Models/StormhammerAwakened`。 |
| 旧觉醒模型 | 已移除 | `Assets/Models/StormhammerAwakened.dae` 从包内与模型契约中删除（无任何加载点）。`Textures/StormhammerAwakened.png` 保留，雷矛拖尾仍在用。 |
| 锤体炫酷效果 | 已确认（源码 + 守卫） | `StormhammerVisuals` 新增 2–3 点环绕光环 + 环间真实带状电弧（走 `LightningScene`，非粒子）；超载时半径收紧、转速 `4.2→7.4 rad/s`、放电间隔 `0.17→0.08 s`、颜色转白蓝。 |
| 放电节流 | 已确认（设计约束） | 该逻辑运行在 `DrawBlock` 路径，逐帧生成会占满 48 条闪电池，因此按画质设 0.08–0.34 s 间隔。 |
| 改名 | 已确认（守卫） | 雷神之锤 → `雷神之锤·伪`；苍穹之怒 → `雷神之锤·妙尔尼尔`（CSV 名称 + 描述 + 伤害来源标签 + 蓄力提示）。 |
| 大招瞬时落雷 | 已确认（代码 + 守卫） | `MainStrikeTime` 4.85 s → **0.28 s**，与伤害结算共用同一常量；预警符文提前到 0.02 s。 |
| 重雷层 | 已确认（667 断言） | `LightningRules.BuildGreatBoltSchedule` 按 cast id 确定性铺满 0.55–11.20 s，每场 5/8/11 道（低/中/高）全尺寸分叉巨雷，锥 ±34°、距离 8–18 格；`Great` 标记 + 主雷落地后才消费。 |
| 重雷伤害 | 不支持（有意） | 重雷为纯视觉，不造成伤害。伤害仍只有主雷一次（220/120/70）+ 余震四次（18），未新增伤害源。 |
| 天幕密度阶段 | 已确认（源码） | 旧的"屏息"段删除，改为起势/压制/高潮/长尾，开场即高潮；天空闪烁延长到覆盖 11.6 s。 |
| 小技能 CD | 已确认（守卫） | 雷霆万钧 12 s → **8 s**；伤害表、0.42 s 权威蓄力、单次结算不变。 |
| HUD 精简 | 已确认（守卫） | 删除技能名 `LabelWidget` 与青色描边框；只保留图标 + 冷却数字 + 雷神降世计时。 |
| 构建脚本编码 | 已确认 | `Build-NetMod.ps1` 改为 UTF-8 **带 BOM**：中文名守卫在无 BOM 时被 Windows PowerShell 5.1 按 ANSI 解码成乱码并解析失败。 |
| 真机观感 | 待验证 | 光环在手视角的大小/位置、电弧密度、低画质帧率均未真机运行。 |
| 大招手感 | 待验证 | 0.28 s 落雷是否够"即时"、11 道重雷是否过载、与主雷的层次均未实机确认。 |
| 联机 | 待验证 | 重雷为本地演出不同步逐道；主雷与伤害仍走权威事件。未双机测试。 |
## 0.32.0 便携堡垒正式美术重构与入口修复（2026-10-26，分支 `feature/fortress-hearthguard-art`）

| 项目 | 状态 | 结论 |
|---|---|---|
| 「堡垒是黑的」根因 | 已确认（源码 + 生成物取证） | 旧贴图为 64×64、4 色、主色 `#2E3238`；旧 `Add-Box` 对每个面写 `uv=(0,0)..(1,1)`，全模型采样同一张近黑图；DAE 只有一个 Lambert 材质，无按部位材质通道。光照不是成因。 |
| UV 图集 | 已确认（构建守卫） | `PortableFortress.png` 1024×1024、16 个 256 px 材质区、全不透明；守卫校验尺寸、alpha 与网格采样颜色数（≥48）。 |
| UV 映射 | 已确认（生成器） | 每个面按约 1 格细分，映射到部件声明的区域；UV 内缩 2 texel，规避 `PointClamp` 串色。 |
| 生成器单一来源 | 已确认 | `scripts/New-PortableFortressArt.py` 一次产出模型、图集、玻璃/暖光贴图、物品调色带与 7 张预览；旧的 `New-PortableFortressModel.ps1` / `New-PortableFortressAssets.ps1` 已删除，避免重新覆盖正式资产。 |
| 模型契约 | 已确认（目标程序集） | 387 部件 / 11 网格 / 1 骨骼 / **13470 三角面**；`Engine.Media.ModelData.Load` 实测 1/11；包围盒精确 X `-5.5..5.5`、Y `0..9`、Z `-6.5..6.5`，越界部件在生成期直接抛异常。 |
| 内部面剔除 | 已确认（生成器） | 被另一盒体对面完全覆盖的面不再输出，三角面从 16248 降至 13470。 |
| 入口卡脚根因 | 已确认（反编译 + 断言） | 一层地板代理铺到 `z=6.5`，在门洞形成 0.36 高的 body 唇边；`HandleAxisCollision` 对地形做平滑抬升、对实体不做，故唇边不可跨越；三格隐藏门槛与唇边同深度，被完全遮蔽。 |
| 入口修复 | 已确认（离线断言 319 条） | 地板代理止于 `DoorInnerZ=6.20`；`StairRise=0.435` 使最后一级踏面与二层甲板齐平；新增入口条带辅助抬升，上限 0.36、仅门宽、跳跃不介入。 |
| 入口真机通行 | 待验证 | 未启动游戏，未真机走门。 |
| 室内家具 | 已确认（生成期断言 + 预览） | 一层双层床/储物柜/供暖炉/桌椅/书架/工作台/壁灯/挂图板；二层指挥台/长椅/海图台/吊灯/竖梯；屋顶护栏/舱口/设备底座。中央 4×4、门口走廊与楼梯路线保持畅通。 |
| 玻璃渲染 | 已确认（引擎源码 + 构建守卫） | `SubsystemModelsRenderer` 只有 opaque / alpha-test 两条通道；改为 `FortressGlazingRenderer` 独立 `BlendState.AlphaBlend` + `DepthStencilState.DepthRead` + `CullNone` 通道，按距离排序。 |
| 玻璃碰撞 | 已确认（离线断言） | `FortressZone.Glazing` 薄代理厚度 ≤ 0.09；斜面观察舱用两块角部块代理，不侵入甲板。 |
| 玻璃可读性 / 排序 / 安卓开销 | 待验证 | 未真机运行。 |
| 室内照明 | 已确认（机制）／待验证（观感） | 地形发光方块 API 已验证可用但**未采用**：实体外壳不挡地形光，会形成十余格外部亮斑。采用低强度暖色 `EmissionColor` 作为可见度下限 + 炉膛/壁灯/吊灯的附加混合暖光几何。逐房间照明在单点 `CalculateModelLight` 下不可实现。 |
| 诊断开关 | 已确认（构建守卫） | `SCIEPortFortressDiagnostics` / `SCIEPortFortressQuality` 持久化；开启后每 0.1 s 输出脚底 Y、局部坐标、速度、脚下代理索引、阻挡代理索引与抬升判定。 |
| 既有功能保护 | 已确认（守卫 + 断言） | 944/945/946 编号不变、地形写入仍为 4 格、`MaxSegmentHalfDiagonal=4` 不变、四向旋转群律、预检七种拒绝、回滚顺序均保留。分段上限由 80 提到 96（新增 7 个玻璃代理）。 |
| Android 7723 全项 | 待验证 | 未真机运行：外观、入口、楼梯、玻璃、夜间可读性、帧耗时、存读档、联机。 |
| Release 构建 / 打包 | 已确认 | `Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；net472 **0 warning / 0 error**；15 个 XDB `DATABASE VALIDATION PASSED`；15 组离线套件全绿（堡垒 319 断言）；`.netmod` **3,047,028 字节 / 93 条目**，SHA-256 `45C82E53CA318EECF3909B6315AA4024161D2AA54CB0F9EA648733DE280C19A0`，modinfo `0.32.0`。 |

| 储物 48 格 / 供暖功能 | 不支持 | 仅有模型与预留结构，无功能实现。 |

## 0.34.0 昆古尼尔 V5.1/V5.1b（离线已确认，Android/联机待验证，2026-10-27）

在 0.33.0 的真实 Look 锚定 HUD 上增加青金/白金状态反馈，并将投枪 Phase 1 去程与 Phase 2 回程拆为独立绘制：白金锐利去程，暖金螺旋/符文/轴环回程，枪体叠加能量脊和分段模型符文；命中/折返提示使用固定 24 槽池。130 项规则/布局、目标模型/资源、全部既有测试、16 XDB、net472 Release（0 warning/0 error）及包校验通过。产物 3,094,805 bytes / 99 entries / SHA-256 `CE23FA8C6933E1953D60DF7BD3009BFF157E6A76389E76533A9E7620B141B447`，modinfo `0.34.0`，AssemblyVersion `0.34.0.0`。ID948、包244/字段、伤害、速度、距离、贯穿、返回、冷却与权威逻辑未改；Stormhammer/Builder 未改。Android 7723、多点触控、帧率、截图和双机联机仍待验证。见 [V5.1 审查记录](GUNGNIR-V5.1-AUDIT.md)。

## 0.33.0 昆古尼尔 HUD / 符文演出重构（离线已确认，Android/联机待验证，2026-10-27）

目标程序集/真实 `GameWidget.xml` 已确认 `LookWidget`、Move/MoveRose、ShortInventory、`GlobalBounds` 与 `ScreenToWidget`；HUD 改为原生 Look 锚定、左手镜像、原生控件碰撞退让和仅图标命中，72/88 逻辑尺寸。1280×720、1920×1080、1.25×/1.5× UI 缩放的 105 项离线规则/布局检查均无 Look/快捷栏重叠。新增可复现透明八字形 `GungnirRunes.png`，`TexturedBatch3D` 刻印+辉光双层、枪杆逐段能量流、神佑/裁决分层演出及高/中/低 64/40/24 段降级；ID948、包244、协议与数值未改。完整构建 16 XDB、net472 0 warning/0 error；包 3,091,265 bytes / 99 entries / SHA-256 `B451A23802425F83B68B53C7B1E0DE09C6A69FE965B55A8874FB9C7F86295830`。Android 触屏、截图、性能和双机联机均为待验证。见 [0.33 审查与真机清单](GUNGNIR-HUD-VFX-0.33-AUDIT.md)。

## 0.32.0 合并候选（苍穹之怒 V5.0 + 昆古尼尔 V4，2026-10-27）

本版本合并两个分别占用 `0.31.0` 的并行开发成果。苍穹之怒 V5.0 与昆古尼尔 V4 的各自离线证据保留如下，不沿用任一 `0.31.0` 分支产物。合并后已重新执行环境/ABI 检查、昆古尼尔 88 项纯规则、苍穹之怒 580 项规则、既有离线套件、net472 Release 编译、16 份 XDB 与包内容校验，均通过；`.netmod` **3,009,161 字节 / 98 条目**，SHA-256 `12830CC3B0D43D77582B96DA720E727703DE627595B5E43BCF3B66760C9CF5B8`，modinfo `0.32.0`，DLL AssemblyVersion `0.32.0.0`。Android 7723 与联机状态仍为待验证。

## 0.31.0 苍穹之怒 V5.0「雷神降世」（源码/离线规则/打包已确认，真机待验证，2026-10-26）

V4.0 的带状雷电、巨型主雷、前方万雷架构、普攻双段、全身雷光、5 点反伤、HUD 与权威伤害链路全部保留。本轮只把「放完大招收工」改造成 12 秒强势期。

| 项目 | 状态 | 结论 |
|---|---|---|
| 雷神状态 | 已确认（源码 + 守卫） | 大招成功释放即授予 `ThunderGodState`，`EndTime = now + 12`；按实体 id 单实例，重复施法只覆盖到期时间，不累加。 |
| 伤害增幅 | 已确认（源码 + 守卫） | 普攻雷电追加 25→50、雷霆万钧 110/70→220/140 均 ×2；普攻物理 40、固定反伤 5、主雷 220/120/70、余震 18 均显式 ×1。日志记录 base / x / final / 生命变化。 |
| 冷却刷新 | 已确认（源码 + 守卫） | 仅 `SetCooldown(id, ThunderousJudgment, 0f)`，每次大招一次；大招自身 55 s 不动；广播 `Accepted` 同步 HUD。 |
| 强化视觉 | 已确认（源码） | 超载期电弧间隔 0.42 倍、芯 0.13 / 辉光 0.36、跨肩跨手连续分叉；每 0.42 s 脚下雷纹；锤头核心转白蓝高亮并放大 1.45 倍；首次雷霆万钧 14 条 / 更粗 / 额外符文（`Step=1` 触发，仅视觉）。 |
| 巨型主雷 | 已确认（源码） | 芯 2.45、辉光 5.6、分叉 7、寿命 1.05 s；新增两圈纯视觉冲击波符文与一圈粒子环；伤害仍单次结算。 |
| 天幕延长 | 已确认（源码 + 580 断言） | 五段密度覆盖 1.15–11.60 s（起势 5+18 / 蓄压 6+26 / 屏息 1+4 / 高潮 9+44 / 长尾 6+26，高画质 145 道）；中/低画质 ×0.62/×0.34。仍为本地确定性、冻结朝向、不加载区块、掉帧跳过。 |
| 天地异变 | 已确认（源码） | `StormReleaseTime = 12.0f`，淡出尾 1.6 s；`GetStormBlend` 仅本地施法者生效；多重视觉用 `1-(1-a)(1-b)` 合成，`Reset` 防残留。 |
| 状态清理 | 已确认（源码） | 死亡 / 断线 / 退出世界 / 卸武器由 `UpdateThunderGod` 逐帧校验并广播 `ThunderGodEnd`；`Dispose` 清字典与视觉状态。 |
| 编译 / 打包 | 已确认 | net472 Release 0 warning / 0 error；15 个 XDB PASS；`.netmod` **2,650,795 字节 / 91 条目**，SHA-256 `B3C5FFBC508F343A800ACF4E9A4E2AF5E3DCECCDD0175420E07AF9390593F3F5`，modinfo 0.31.0。 |
| Android 7723 | 待验证 | 12 s 强势期手感、超载电弧密度、HUD 计时可读性、低画质天幕帧率均未真机执行。 |
| 双机联机 | 待验证 | 旁观者天空不受染色、主雷同步、两端伤害数字一致均未实测。 |

详见 `docs/SKYWRATH-V5.md`。
## 0.31.0 昆古尼尔 V4 分支候选（2026-10-27）

已确认：固定Windows程序集ABI/哈希、ID948/包244原版冲突检查、net472 Release零警告零错误、目标模型加载1骨骼41网格1176面、88项纯规则、XDB与`.netmod`包内容检查。待验证：Android、游戏内表现、联机。当前不支持严格服务器远程玩家运动物理权威；当前只是服务器授予增益再同步到原生玩家移动。项目所有者明确要求本次构建不运行仓库中缺失的Stormhammer测试工程。新增包244（命令/快照/表现）、SCIEPortGungnir行为与独立Assets路径；原有Hook签名不变。见[审查记录](GUNGNIR-V4-AUDIT.md)，不能作为Android兼容结论。

## 0.30.0 苍穹之怒 V4.0（源码/离线规则/打包已确认，Android 真机待验证，2026-10-26）

二技能「雷霆万钧」与三技能「万象雷劫」重制，新增独立雷电几何系统。初始雷神之锤、觉醒武器模型/贴图、普攻双段伤害、全身雷光、5 点反伤、技能 HUD 冷却与权威伤害架构未改。

| 项目 | 状态 | 结论 |
|---|---|---|
| 旧版缺陷根因 | 已确认（源码） | `StormhammerParticleSystem.CreateArc` 沿路径撒 18–44 个 0.035–0.075 尺寸的相机朝向粒子；`EmitSkyBolt` 同样用粒子弧。视觉上只能得到亮点链，没有任何连续几何。 |
| 引擎绘制能力 | 已确认（反编译） | `SubsystemDrawing` + `IDrawable` 自动注册（`Project.Load` 先构造全部子系统再 Load）、`PrimitivesRenderer3D.TexturedBatch`、`TexturedBatch3D.QueueQuad`、`BlendState.Additive`、`DepthStencilState.DepthRead`、`RasterizerState.CullNone`、`SubsystemSky.CalculateFog/ViewFogColor`、`ComponentBody.ApplyShaking`、`SubsystemAudio.PlaySound`。 |
| 雷电几何 | 已确认（源码 + 277 断言） | `LightningRules.BuildPath` 相干值噪声双向扰动、端点锚定、同 seed 逐位可复现；`LightningScene` 池化 48 条，绘制"白蓝亮芯 + 青蓝辉光"双层带，主雷加交叉面，含角宽下限、雾融合、地面符文与命中爆闪。 |
| 二技能 | 已确认（源码 + 断言） | 0.42 s 权威蓄力后结算；半径 9；≤4 格 110、线性降至 9 格的 70；每目标单次施法只结算一次；10–12 条径向粗雷（芯 0.52 / 辉光 1.45，各 2 分叉）。 |
| 三技能 | 已确认（源码 + 断言） | 取消 24 目标扫射；改为单一权威主雷：4.25 s 预警、4.85 s 落地、220/120/70（≤2.5 / ≤5 / ≤8 格）、余震 4 次×18、10.8 s 收束。主雷芯宽 1.95 / 辉光 4.6，起点取真实地表 + 34 格。 |
| 前方天幕 | 已确认（源码 + 断言） | 仅施法者本机；朝向在施法瞬间冻结；近景 10 道（15–25 格、±43°）+ 远景 26 道（25–55 格、±49°）；不加载区块、不造成伤害、不查询目标；掉帧按时间轴跳过。 |
| 天地异变 | 已确认（源码） | 深靛蓝→紫黑渐变 + 落雷补光；`GetStormBlend` 过滤 `LocalCaster`，旁观者不被染色；`Dispose` 调 `Reset` 防止天空卡紫。 |
| 联机 | 已确认（代码边界）；真机待验证 | 伤害/冷却/落点均由权威端执行；主雷单次结算；天幕为本地确定性视觉；种子派生自广播 `CastId`；客户端视觉不参与伤害。 |
| 编译/打包 | 已确认 | net472 Release 0 warning/0 error；15 个 XDB PASS；`.netmod` **2,648,230 字节 / 91 条目**，SHA-256 `D2EE8300C12BC21DC69A5EBC0D5516B2EC8B0ADE146D738CC18B1A21CEDA3BFA`，modinfo 0.30.0。 |
| Android 7723 渲染/手感/帧率 | 待验证 | 未真机运行：主雷宽度亮度、天幕密度、爆闪时长、抖动强度、低端机帧率均未测。 |
| 双机联机 | 待验证 | 主雷同步、旁观者天空不受影响、天幕不外泄均未实测。 |

详见 `docs/SKYWRATH-V4.md`。

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
## 0.27.0 双人直升机移动端飞行控制 HUD（已编译/离线，Android 触控与双机待验证，2026-10-25）

本阶段只在既有直升机飞行/座次/网络路径上叠加移动端专属的独立上升/下降按键 HUD，未替换任何已验证逻辑；分层设计、反射取证与真机待验清单见 `HELICOPTER-HUD-V0.5-AUDIT.md`。

| 项目 | 状态 | 结论 |
|---|---|---|
| 控件 ABI 来源 | 已确认（反射实证） | 控件类型均在 `Game.*`（`Survivalcraft.exe`）而非 `Engine.Widgets.*`。逐一确认 `ClickableWidget.IsPressed{get;set}`（持续按压+多点触摸原生通道）、`BevelledRectangleWidget` 圆角半透明、`ImageWidget.RotateAngle`+`Subtexture`、`ContainerWidget.AddChildren/RemoveChildren/ClearChildren`、`CanvasWidget.SetPosition` 为静态、`WidgetAlignment{Near/Center/Far/Stretch}`、`ComponentGui.ControlsContainerWidget`/`ComponentPlayer.GuiWidget`（即 ContainerWidget）、`SettingsManager.UIScale`（公开静态）、`ButtonWidget` 无公开构造函数（故组合而非继承）。 |
| HUD 架构 | 已确认（代码） | 纯逻辑 `HelicopterControlSurface`（意图仲裁/可见性/响应式布局，零引擎依赖）+ 控件 `HelicopterFlightHud`（每键 CanvasWidget 根+面板+箭镞+命中层，一次构建、仅在几何变化时重排）+ 接入 `ComponentHelicopterDrive`（本地授权驾驶位 Attach/Refresh/读键）。专用按键优先于视角通道，跳跃/潜行为次级，六路经 `ResolveIntent` 归一后复用既有 `GetVerticalTarget`/`IntegrateVerticalSpeed`，绝不直写实体坐标。 |
| 响应式布局 | 已确认（29 条断言） | 右下角堆叠（上升居下降之上），短边驱动尺寸并夹取 Min96/Max190，间隙与右下边距按分数边距，X 居右半避开原生左摇杆，随 `SettingsManager.UIScale` 缩放并对零/过小值兜底；下机/乘客/远端/销毁态隐藏。 |
| 7723 安全回退 | 已确认（代码/守卫） | 所有控件与 `ContentManager.Get` 调用包 try/catch，异常仅记 `Log.Error` 并隐藏 HUD、按钮不可读，回退到已验证的视角升降通道，不打断驾驶循环；与 `VehicleActivationService` 同风格。 |
| 未替换既有路径 | 已确认（代码边界） | 飞行物理、双座角色、`ComponentTwoSeatManager` 输入重定向、`VehicleActivationService` 授权门、原生父 Body 一元位置同步与包 242 均未改；HUD 只新增本地输入源。 |
| Release 构建/包 | 已确认 | `Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release` exit 0；全量离线套件+ Helicopter 73 断言通过，net472 0 warning/0 error，12 个 XDB 通过，`HeliArrow.png` 与三源文件守卫纳入。`.netmod` **521,449 字节 / 65 条目**，SHA-256 `2CEEAA02429B5A7747E212999EDBB7065C8F7CB18244995EF982356EB60A8E80`；modinfo 0.27.0，DLL 0.27.0.0。 |
| Windows 游戏运行 | 待验证 | 未启动目标游戏；HUD 是否出现、按住升降、悬停、原生 UI 还原与保存重载未运行。 |
| Android 7723 单机 | 待验证 | HUD 是否真正附加到驾驶位控件层、圆角/箭镞朝向、单指持续按压、双手多点触摸与左摇杆并用、松开平滑悬停、`ImageWidget.RotateAngle` 是否绕中心、不同分辨率/横屏/UIScale 边距安全区、与快捷栏/菜单重叠，均需真机实测；若不可用则按回退保持视角可飞。 |
| 两台真实 Android 客户端 | 待验证 | 仅授权驾驶员出按键且可操控、乘客隐藏不可控、垂直输入经主机校验并随既有一元位置包同步、切换驾驶员清除旧输入无抖动/重复、远端表现，必须以相同最终包哈希验证。 |

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

## 2026-10-29 port 0.16.1 / 阿比斯V0.1.1

修正外向持镰、移除碎片长串，增加暗钢倒角方向高光与双面黑心紫光核心；修复UpdateInput Hook早于触屏输入导致长按失效，改为Input+1采样并显示蓄力百分比。952/949–951、战斗数值和权限不变，Android新包实效待用户复测。细节见ABYSS-REAPER.md与TEST-RESULTS.md。

## 0.15.0 吞噬之眼 / Eye of Oblivion V1（2026-10-28）

分支 `port/scnet-x25.01.17`；新增 ID 950，与全知之眼 949 分离。新增真实 UpdateInput Hook
将该物品普通 Hit/Dig 转为 Interact 确认流程，消除原生挖掘/攻击旁路。复用既有 GuiUpdate
注册，按 Project 管理确认窗口/队列/事务锁，Dispose 取消任务并释放 UI 引用。
不变更原生类、网络包、已有实体 GUID 或部署物品；T-34 新增只读 CanRecover 状态，
禁止丢弃仍在飞行的炮弹。目标 EntityTemplate 名/GUID、五个组件类型、持久化状态、
骑乘/父子/所有权依赖、移动/射击、待重生登记均复查。

已确认：Windows 固定程序集/API/net472 编译；真实模型导入 1 bone / 27 meshes /
816 triangles；Y_UP/identity 根变换/不透明 128×16 PNG；75 项纯规则与故障注入测试；
真实 Project 移除/保存成员列表、背包满额/回滚、创造槽、Terrain DATA 往返、原生 JSON
回执往返的隔离测试。实际坦克完整组件、Android、触屏、正常退出重进和多人待验证。

权限来自原生 `SubsystemBedrockBlockBehavior.CheckIsInTerritoriy/AllowPlayerAction`；
V1 保守拒绝所有原生领地（含本人领地）。没有通用第三方区域保护 API；额外保护模组
需要独立适配，不能据此宣称任意保护模组兼容。块修改还遵守原生 TerrainChangeCell Hook，
实际读取结果确认删除后才返还物品。细节与边界见 [DEVOURING-EYE.md](DEVOURING-EYE.md)。

## 0.14.0 全知之眼 V0.1（2026-10-28）

目标分支 `port/scnet-x25.01.17`，基线 `a7e313a`；没有合并 main 或修改 Builder Assistant。
新增物品 Block ID 949（扫描目标原生 260 个常量及本分支全部物品，未占用）。
新增 `SubsystemOmniscientEye`，通过标准 `.xdb` / CSV 注册；新增现有 ABI
`GuiUpdate(ComponentGui)` Hook，沿用当前 Loader 的一次注册入口，无额外网络包。
使用真实 `CanvasWidget` / `LabelWidget` / `RectangleWidget` / `BevelledButtonWidget` /
`Dialog` / `DialogsManager`。玩家 UI 属于当前 Project 的子系统，Dispose 移除 HUD、
关闭对话框并释放玩家引用；不注册静态世界退出事件。坐标/区块每 0.2 秒、气候每 1 秒。
温湿度来自 `Terrain.GetTemperature/GetHumidity(x,z)` 的 0–15 柱气候等级，
不是摄氏度、百分比或玩家体温；高度为世界 Y。未加载区块显示未就绪。

已确认：固定 Windows 程序集指纹/ABI，net472 Release；真实 Engine 模型导入
1 bone / 25 meshes / 784 triangles；Y_UP、identity 根变换、UV、不透明 128×16 PNG；
27 个 HUD 布局场景；现有规则套件、7 份 XDB、包资源守卫。
待验证：Windows 游戏内使用、Android 7723 冷启动/手持/触屏/帧率、多本地玩家与联机。
布局离线检查不能替代实机控件遮挡测试。清单见 [OMNISCIENT-EYE.md](OMNISCIENT-EYE.md)。

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


## 2026-10-29 阿比斯 V0.1 / port 0.16.0

指定port分支新增原创立体战镰952（自动扫描生成分配）、三连斩/蓄力重斩、四槽锁定UI。949/950及主线951保持不变，无地形/天气修改。真实手持动画API、编译与离线检查见SCIE-Port/docs/ABYSS-REAPER.md及测试记录；Android和联机实测待验证，普通客户端战斗不支持。
