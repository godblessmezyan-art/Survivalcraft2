# 阿比斯·终焉之镰 V0.2（main 0.47.0）

> 0.49.0 在主线 A3 模型上合入断界斩和四技能开发预览。完整演出、远距离星门加载及普通客户端新技能尚未完成；见 [技能状态](ABYSS-V1-SKILLS.md)。下文保留旧版本记录。

## 当前主线：V0.2 / 荒野探险家 0.47.0（2026-10-29）

以下 V0.1/V0.1.1 为 port 历史；阿比斯现已接入 main，正式模型为设计目录的紫曜巨镰 A3。
单根 identity / Y_UP / 32 meshes / 9,260 triangles / 256×16 全不透明 PNG。
`New-AbyssReaperAssets.py` 调用原创设计生成器、完整拓扑验证后复制正式资源和审计 manifest；
`Test-AbyssGeometry.py` 检查正式 DAE/PNG/几何与设计源逐字节一致，防止旧模型重新进入包。
本机重生成与历史设计 DAE 差异仅为接近零的浮点舍入（最大约 8.42e-15），未修改造型参数。
当前精确哈希以 `docs/assets/abyss-reaper/manifest.json` 为准。

材质 0–5、10–11 保持非自发光深紫主体/握柄，6–9 为金属斜面方向高光，12–15 为窄能量嵌片，
后者占表面积约 3.29%；原生路径仍无 PBR 或环境反射。尺寸/持握 CSV 沿用 V0.1.1。
图标/第一人称/第三人称软件图由正式资源对应几何生成，不作为游戏截图。

ID 固定 952，吞噬之眼 950、波塞冬 951、全知之眼 949 不改。
分类 `SCIEPort.Artifacts`，`PortInventoryRules.Theme.AbyssReaper` 注册专属深紫/浅紫边框，
统一管理器负责换物品、关闭面板、退出世界恢复原槽位并清理引用。
保留主线原有全部 Loader Hook 和网络包 240–246；只在既有 GuiUpdate 中增加本地只读面板。
触屏采样仍在 Input+1=-9，四圣兽动作锁定时取消蓄力与连斩，避免合并后绕过锁定。
伤害 22/22/34/52、范围、保护/LOS、命中去重/预算和 Local/Server/Client 边界不变。
四个技能槽继续锁定，本轮未实现裂界星门等技能。

兼容性：Windows 固定哈希程序集离线导入/编译见测试记录；Android 7723 图标、持握、裁切、
低光、高光、长按、左右手/多分辨率与新模型性能均为待验证；普通客户端战斗不支持。
三角形从 2,480 增至 9,260，未测移动端帧耗时，不能宣称性能验收通过。
主机与单机游戏内伤害/四圣兽锁定组合仍须用可丢弃新世界验证。

当前版本：34 meshes / 2480 triangles，新增preview-*.png为当前预览。外观与长按修复说明见文末V0.1.1记录；下方保留初版参数用于追溯。

## V0.1 初版记录

基线：port/scnet-x25.01.17 69db2ff。原创资产由New-AbyssReaperAssets.py可重复生成，无外部美术来源。仅新增此神器；没有合入main或改动Builder Assistant。没有传送门、黑洞、天气或地形写入。

## 模型与文件

Assets/Models/AbyssReaper.dae：Y_UP、identity单根、32 meshes、930 triangles。暗星铁柄、独立连接器/核心、三段有真实间隙的厚弧刃、五片碎片、尾端星锥。128×16全不透明色板PNG；模型/纹理由物品Initialize缓存，枚举全部mesh。实际细分弧刃只需930面，低于初始预算。几何绕序、法线和UV守卫见Test-AbyssGeometry.py。

比例图docs/assets/abyss-reaper/proportions.png是软件预览，使用CSV的.64第一人称和.80第三人称缩放；第三人称旁1.77格人体标尺。不是目标游戏截图，不证明持握位置、透视投影、动画或Android显示正确。

## ID与注册

Resolve-AbyssId.ps1扫描目标原生260个方块常量、本分支全部const Index以及origin/main保留号；949全知之眼、950吞噬之眼、951主线波塞冬已占用，首次选出952并生成AbyssId.cs。之后只验证且拒绝碰撞，不随新扫描结果静默重编号。CSV/XDB有独立资源路径与新GUID。其他Mod仍可能占952，需安装组合验证。

## 普攻和蓄力

原生UpdateInput之后，仅持此物品时捕获世界Hit/Dig/Aim，清除原生攻击/挖掘/Interact旁路；不创建普攻UI按钮。轻触松开依次右向左(.35s)、反向(.42s)、举镰下斩(.58s)，末击结束后1.05s连击超时重置。长按至少.65s松开触发.85s蓄力重斩，不自动连发。忙碌时松开不排队。切物品、死亡、移出Project、模态UI/对话框取消。

目标真实API：ComponentFirstPersonModel.ItemRotationOrder及ComponentCreatureModel.InHandItemRotationOrder。通过游戏更新持续下达不同旋转轨迹，原生插值处理；不是替换玩家骨骼。远程人物未同步自定义轨迹。

AbyssCombo的独立Impact边缘触发单次结算，命中与Draw解耦。48%时间点，普通3格/蓄力3.5格，水平扇区及1.7格高度限制，目标中心LOS墙体遮挡，实体HashSet去重，每击最多24实体，每次最多审查512个body。攻击力22/22/34/52，走原生AttackBody盔甲/伤害/联机路径。第三击及蓄力附带.22s锯齿裂纹线，不使用常驻光环。

## 配置与权限

XDB子系统参数AllowPlayers=false、AllowPassive=false、ShowRifts=true。默认仅LandPredator/WaterPredator，不攻击玩家及非掠食动物；此分类不等于实时仇恨，掠食类即使当下未追击仍会命中。玩家开关仍服从原生友伤设置。所有原生领地内目标保守跳过，不声称兼容第三方保护Mod。

Client从不结算自定义伤害；Server只允许本地IsMainPlayer输入，普通远程玩家战斗未开放，不新增网络包。客户端面板明确提示此限制。普通联机客户端战斗为不支持，主机伤害传播/动画仍待联机验证。

## UI及验收

非交互深黑紫金属边框面板，四个单色菱形槽标：裂界星门、断界斩、虚影瞬狩、终焉·万象坍缩，全部标明锁定，无释放逻辑。仅持物显示，近顶部中央，不拦截原生触控；卸装/Dispose移除引用。

待验收：Android7723冷启动、图标/第一人称与第三人称持握比例，左右手/不同UI分辨率遮挡，轻触三连轨迹、长按蓄力/模态取消，墙后及中立/玩家安全，原生领地、同一目标单击去重，关ShowRifts后伤害，主机伤害传播、切换世界与帧率。未操作游戏或连接设备，不能宣称Android兼容。

原创资产SHA256：DAE 1B0D0240C18B6BA60AF6EC263ECA5E98059CAA75434E54991604D6D708A5DF27；PNG 4F64390769ED1650503F93F50BCE5842FFBADE2BCE7CDF4443B13CABDFE71309。

## 2026-10-29 V0.1.1（port 0.16.1）外观与真实触屏修复

用户7723截图确认V0.1可见，但主刃朝外并出屏、碎片串累赘、核心被壳遮挡，且长按无法触发。原V0.1仅完成桌面编译/软件预览，不能据此视为设备验收。

- 用Y轴180度正行列式旋转烘焙全部顶点/法线，改为右手内向弧刃。去掉原FloatingShard/ShardRift长串，保留刃隙附近两颗小碎片。方柄换八棱暗钢柄、锻造接头与窄银灰倒角。两侧开放核心黑心、暗紫内唇与双层窄光环，避免再看到棕色壳面。34 meshes/2480 triangles、单根identity、Y_UP；256×16全不透明PNG。
- 原生BlocksManager.DrawMeshBlock使用预烘焙漫反射，不提供PBR/粗糙度/环境反射。本版仅对金属倒角做按朝向调制的方向高光，能量UV色带12–15使用真实BlockMeshVertex.IsEmissive，核心/刃缘缓慢呼吸，不伪称真实镜面或投射环境光。缓存网格/纹理/能量与高光顶点索引，绘制每次仅6组高光计算及原有顶点数组修改，单次DrawMeshBlock；不新增粒子或常驻大型光环。
- FirstPersonScale .64→.46，Offset .50,-.38,-.95，旋转0,-8,-8。预览脚本读取实际CSV，给出ICON/FP/THIRD/BACK，并检查横屏2772/1280、垂直FOV65/85静止刃片在视锥内；是软件假设镜头检查，仍不等于7723实际投影/动作通过。旧proportions.png保留为V0.1历史图，当前用preview-*.png。
- 真实反编译确认ComponentInput.Update先调用鼠标（其末尾才是UpdateInput Hook），再处理手柄和Widgets触屏。旧Capture太早，取不到触屏输入，并无法清理触屏后写入的原生攻击。现在只在Subsystem UpdateOrder Input+1=-9采样全部本地玩家，严格介于Input=-10与原生Player Default=0，移除Loader中的旧Capture，避免每帧重复采样。
- 手机Hold第一次要等0.2秒才提供，首次蓄力按TouchInput.Duration回溯真实按下时点，拖动后继续保留起点；面板显示百分比，>=0.65秒显示蓄力完成，松开触发重斩。死亡、非实体相机、失焦、睡眠、模态窗口、未就绪、暂停、换物品会取消。战斗数值、合法目标判定、领地/墙体保护及客户端不结算权限不变。
- Test-AbyssRuntime反射解码真实IL，确认原生调用顺序、Hook位置与新DLL的-9采样顺序；回归测试包含触屏延迟Hold、连续拖动、短按、冷却内松手与负时间起点保护。没有操作设备，更新后的朝向、光泽、蓄力与触屏UI仍待用户复测。

V0.1.1原创资源SHA256：DAE 48BF03C9667DC181BD31387ED3F9F6A40007997E05A4E097CA0EBA3FF17EC3A8；PNG 8F294F389E15D44FFD0102B9A01925BF435B58DDAA633B266DC565579374587F。官方Android Survivalcraft.dll（3520162AFB8DCCC51DD32A2FE22114D0325DD55D270B4B922AABF16A4AA4429B）额外反编译确认同样先MouseHook、后Gamepad/Widgets；仍不等于7723真机修复已通过。
