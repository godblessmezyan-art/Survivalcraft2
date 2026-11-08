# PixelFX V0.2：其它神器特效选型与实现

2026-11-06，基于main `db11fe8`，Mod版本0.78.0。按[神器特效开发规范](PIXELFX-DEVELOPMENT-STANDARD.md)先记录本次选型，再接入表现。

## 实现前的选型记录

已核对固定目录、候选源码/meta/依赖，并对采用内容实际离线导出；来源均为`patch-gg/pixelfx@a84159688af3ca37a4be05a0d422b94451b2e24e`，MIT，Git blob/SHA校验与完整许可保留。GitHub连接暂不可用，直接读取固定raw源码，以此前取得的该提交Git tree blob验证；没有使用浮动main。

| 神器 | 候选及结论 | 接点与保留的3D |
|---|---|---|
| 圣剑Excalibur | 采用holy/blessing短时光锥、holy/sunburst短时光晕；只取一次性启动/释放片段，不增加循环buff。 | 已接受施法/快照及原Release；按CastId去重、按Age跳过过时片段。剑体、刀光、真实35格光束及阿瓦隆空间几何保持。 |
| 昆古尼尔 | 采用holy/sunburst召唤光晕、holy/smite已落下冲击片段。 | 复用原Invoke/Impact本地及Kind3音效演出事件；同位置同类短间隔合并精灵，音效/包不变。真实神枪、投掷路径和天罚雷柱保持。 |
| 波塞冬 | 采用water/splash（stone=false）及water/wave-crash。 | 已接受Cast更新；浪在原0.3秒前摇后播放，水盾/海域只加启动水花。保留随方向前进的3D浪、漩涡和水盾。 |
| 阿比斯 | 采用shadow/dark-explosion释放后的紫色爆发；不采用shadow/soul-drain，其固定左右端/容器/循环不适配当前技能。 | 死亡蔓延Wave的原1.25秒冲击阶段，按Owner/Sequence去重并校正包龄；真实扩散面保持，不触碰私有虚夜/冻结门户算法。 |
| 全知之眼、吞噬之眼、神器图谱 | 本轮不采用。检查arcane/magic-circle-cast后，其地面长光柱/完整召唤前摇不适合打开识别/回收/阅读面板。 | 工具UI、回收事务和图谱权限保持，不为纯工具叠加遮挡世界的战斗演出。 |

导出的[实际帧预览](assets/pixelfx-v0.2/contact.png)已检查，移除人物/背景、水花的石头预览关闭，保留金/蓝/紫的神器主题；不是实机截图。6种新效果与原雷锤3种共用单图集、单世界池/门限/批次；Low/Medium/High全体合计实例8/16/32、quad6/12/24、覆盖1/1.5/2视口面积，不能每件神器另领一套。
图集1024×2048、RGBA8无mipmap，8MiB（比旧单雷锤增加4MiB），原78帧区域保留，新增帧有透明留边。运行资源沿用历史PixelFxStormhammer文件名，不改任何游戏物品/存档身份。

## 范围约束

只加表现调用、离线资产与通用播放器能力；不改伤害、冷却、默认移速/相乘攻速、库存主题、ID/DATA/GUID、存档、网络包和原3D算法。技能图标不改。
资源/源码不进入Builder Assistant。Android7723及真实Local/Server/Client单列验收，不能以桌面构建/GPU隔离冒充兼容。

属性核对：海神/阿比斯/圣剑保持默认+20%移速与+10%相乘攻速；昆古尼尔沿用[第三技能专项](ARTIFACT-THIRD-SKILLS.md)“原有技能、被动……一律保持”的既有例外（见GungnirRules.SpeedFactor/AttackInterval）：持枪1.5倍、神佑2倍，外部攻速相乘。本轮不把这些旧倍率改写成通用默认，也不追加新的数值例外；[库存规范](INVENTORY-STANDARD.md)及神枪专项已明确登记。

## 具体时间与事件

| 表现 | 源时间/帧数（24fps） | 运行端时间 |
|---|---|---|
| holy/blessing | 0.40起/16帧，种子7，空actor | 圣剑三技能的已接受起手；一次0.667秒，非持续状态。 |
| holy/sunburst | 0.20起/16帧，种子3 | 圣剑技能2原1.75秒Release之后，及神枪Invoke原演出。 |
| holy/smite | 0.54起/20帧，种子9 | 神枪Impact既有演出，省略源前摇，不延迟原冲击。 |
| water/splash | 0.16起/18帧，种子3/stone=false | 海神水盾、海域的已接受Cast启动，0.75秒。 |
| water/wave-crash | 1.20起/20帧，种子6 | 海神浪在原0.30秒前摇后，0.833秒局部余浪；不代替真实移动浪。 |
| shadow/dark-explosion | 0.52起/18帧，种子6 | 死亡蔓延原1.25秒冲击后，中心紫色余辉0.75秒；不增加伤害。 |

原0–77帧RGBA逐格与0.77.1完全一致；新增78–185，共186帧。矩形图集保持96×96帧格/10列/2像素透明留边、PointClamp、直通alpha/Additive与无mipmap。暗影片段只叠加发光颜色；原3D黑色扩散面继续负责暗色空间，精灵不对世界作减色或改天光。
圣光/水浪/暗影共用同一世界的`LightningScene.SpriteFx`。沿用它的Load、Update、Draw105与Clear，无新子系统/XDB/Hook。`ArtifactSpriteClips`为九种效果的通用C#表，旧`StormhammerSpriteClips`仅保留前三种的别名；运行PNG/JSON名称不变，不多装第二张纹理。

`SpriteFxPlayer.PlayAtAge`按当前世界时钟回推Birth，不移动池的最后更新时间；晚到快照直接从剩余帧播放，已过期片段直接丢弃。圣剑原Release才触发爆发，取消施法不会留下排队的未来爆发；Poseidon/Abyss在旧Cast更新阶段检查原时间阈值，结束后立即跳过表现查询。
`SpriteFxEvents`固定128条原始数值事件记录，使用owner/sequence/family/phase组合键；过期优先复用、满时淘汰最早租约、回退/退出清空。记住因池满被丢弃的事件，避免后来空闲时补播。事件去重只管精灵，不改原网络鉴权/去重/音效和包。
海神局部Cast对象哈希仅作为该端表现身份，不写存档/不发包；圣剑用原CastId，死亡蔓延用原Owner/Sequence。神枪旧Kind3音效事件没有独立施法身份，采用1/4格位置+Invoke/Impact类型、0.12秒合并，允许多人同点演出合并但各自原技能/声音结算不变。

## 构建与来源

```powershell
node SCIE-Port/tools/pixelfx/export.mjs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:/SCNET/SCNETx25.01.17
dotnet run --project SCIE-Port/tests/SCIEPort.PixelFx.Tests -c Release --nologo
python SCIE-Port/scripts/Test-PixelFxAssets.py
powershell.exe -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -Configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-PixelFxGpu.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -AssemblyPath SCIE-Port/artifacts/build/SCIEPort.dll -OutputDir SCIE-Port/artifacts/pixelfx-0780-gpu-final
```

固定31文件：原22个模块加6种新效果及holy/water/shadow三辅助模块，全部保留未修改源码、Git blob与SHA-256；完整MIT许可和双方署名随包，工具/JS不入包。图集PNG936,298字节，SHA-256 `0A5F6ECB559BCFF0027A833AAD1ABCD0F2E996C4B9FE7149B12A4F1A82BD1BE8`。JSON和两份C#表离线生成并逐字节重导出验证。
保留原35文件守卫，增加以db11fe8为基线的32文件守卫，剥离明确的6行表现调用后，四神器子系统/死亡权柄、规则与相关3D代码须完全一致。原源码基线不被改写。
具体断言数量、最终DLL/包指纹、资源原生导入与全量回归结果见[测试记录](TEST-RESULTS.md)。

最终通过493项播放器/事件门（各10万次稳态0字节）、1193项资源/来源/原帧/原实现保留及1351项目标Engine GPU隔离；与0.77.1同名旧GPU画面逐像素对照见测试记录。已检查[四神器实际原生合成绘制](assets/pixelfx-v0.2/engine.png)，不是游戏或Android录屏。完整Release/包已通过；产物`荒野探险家-0.78.0.netmod`，仅本地main提交。

## Android/实际联机验收（单列，待验证）

没有本轮Android/7723、完整游戏世界或真实房间测试；Windows固定ABI/net472和Engine隔离不能代替手机。各端统一0.78.0，原握手保持，旧版混装不支持。

| 场景 | 必须记录 | 状态 |
|---|---|---|
| 新建可丢弃Local世界、四神器与原雷锤 | 先解放原图谱权限，检查新精灵/旧3D并存、颜色/锚点、伤害/冷却/默认属性对照 | 待验证 |
| 圣剑取消/晚到快照/重传、阿比斯冲击 | 取消无未来爆发；只播放剩余帧；原1.75/1.25秒时间与结算同步，无补播或重复 | 待验证 |
| 神枪同点多人、海神20秒海域 | 同位置短合并仅影响新精灵，原音效/结算保持；水浪前摇0.3秒，海域不无限重复水花 | 待验证 |
| Server/普通Client/旁观者、晚加入/高延迟 | 仅复用旧已接受事件/快照，各端不新增包；观察无重复/历史爆发、无越权 | 待验证 |
| 三档/减闪/20:9和其它触屏分辨率 | 全部神器共池且quad/覆盖总量不超预算，边缘不误删、不新增UI遮挡 | 待验证 |
| 多人10分钟交替/同放，前后台/重进/设备恢复 | 8MiB图集可加载，P95帧时/峰值内存/发热、纹理恢复和池/引用释放 | 待验证 |

手机先备份存档并记录精确客户端程序集名称/版本/SHA、Mod DLL MVID、设备/系统/分辨率，不能让7723重打包自动继承公开Windows结论。
