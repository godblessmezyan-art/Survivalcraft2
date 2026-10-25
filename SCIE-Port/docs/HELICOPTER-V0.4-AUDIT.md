# 双人直升机 V0.4 源码审计、实现与验证记录

日期：2026-10-25

目标：Android Survivalcraft 2.4 NET `Net_x25.01.17`

状态词仅使用：`已确认`、`待验证`、`不支持`

## 1. 基线与范围

仓库历史和所有可见分支中没有名为 V0.3 的独立实现。可验证基线是提交
`e5aad7e` 的 SCIE-Port 0.25.0 / 直升机 V0.1。本次将该实现的双座、驾驶
权限、飞行物理、生成、保存和联机路径视为必须保持的核心；没有虚构不存在
的 V0.3 API 或测试结论。

本次在 `feature/helicopter-v0.4` 分支工作。没有修改物品 ID 941、已有实体/
组件 GUID、座位包 ID 240、世界存档格式或其它载具。飞行计算仍隔离在
`Features/AirVehicles`，表现层只读飞行状态并写模型骨骼和本地声音。

## 2. 实际源码与资源路径

| 机制 | 准确路径 | 已确认代码证据 |
|---|---|---|
| 物品与创造栏模型 | `src/SCIEPort/Features/AirVehicles/HelicopterBlock.cs`、`src/SCIEPort/Assets/Blocks/Helicopter.csv` | `Index=941`；`Initialize` 读取 `Models/Helicopter`/`Textures/Helicopter` 并枚举网格；V0.4 只排除物品栏中的高速 blur 网格。 |
| 生成实体 | `src/SCIEPort/Features/AirVehicles/SubsystemHelicopterBlockBehavior.cs` | 继续调用目标版本已验证的 `SubsystemCreatureSpawn.SpawnCreature`，普通 Client 不自行生成。 |
| 实体与配置 | `src/SCIEPort/Assets/Database/Helicopter.xdb` | 独立 `SCIEPortHelicopter`，一个原生 Mount、`TwoSeatManager`、`HelicopterDrive`、SimpleModel、Spawn、Body、Damage；核心 GUID 和碰撞体未改。 |
| 飞行与表现 | `src/SCIEPort/Features/AirVehicles/ComponentHelicopterDrive.cs` | 本地主驾驶员仍是唯一输入源；原有速度/旋转物理移入等价的 `SimulateFlight`；新增 `UpdatePresentation` 只处理骨骼与声音。 |
| 纯飞行/表现规则 | `src/SCIEPort/Features/AirVehicles/HelicopterFlightRules.cs` | 原有重力、悬停、加减速/限速规则保留；新增 rotor state、角度回绕、视觉姿态阻尼和 blur 混合纯函数。 |
| 双座与权限 | `src/SCIEPort/Features/Vehicles/ComponentTwoSeatManager.cs` | 唯一 Driver/Passenger 状态；只缓存 Driver 输入；乘客输入不进入 Drive；ID 240 同步离散座位角色。 |
| 原生车体同步 | `docs/VEHICLE-MULTIPLAYER-AUDIT.md` | 驾驶员保持 `ChildBodies[0]`，沿用 x25.01.17 骑手父 Body 的位置/旋转/速度同步；不是严格服务器权威。 |
| 模型/贴图/音频 | `src/SCIEPort/Assets/Models/Helicopter.dae`、`Assets/Textures/Helicopter.png`、`Assets/Audio/SCIEPortHelicopterLoop.wav` | 均由 `scripts/New-HelicopterAssets.ps1` 从代码确定性生成，不包含外部样本。 |
| ABI/打包验证 | `scripts/Test-Environment.ps1`、`scripts/Build-NetMod.ps1` | 对真实目标程序集反射声音接口；用目标 Engine 实际导入 DAE、解码 WAV，并检查包内资源。 |

## 3. 已验证的目标引擎能力

固定 Windows 发布程序集哈希仍为：`Survivalcraft.exe`
`3D14B8D...A7AD`、`Engine.dll` `98F2035B...694`、`EntitySystem.dll`
`7A87E643...D19C`；完整值见 `docs/COMPATIBILITY.md`。

- `Engine.Media.ModelData.Load(Stream)` 实际导入 V0.4 DAE 为 **6 bones / 5 meshes**。
- `ComponentModel.SetBoneTransform(int, Matrix)` 存在，故使用现有分部件矩阵，未引入或声称存在骨骼动画播放器。
- `Game.SubsystemAudio.CreateSound(string)`、`CalculateListenerDistance(Vector3)`、
  `CalculateVolume(float,float,float)` 存在。
- `Engine.Audio.Sound` 的 `State/Volume/Pitch/IsLooped/DisposeOnStop` 和
  `Play/Stop/Dispose` 存在。
- `Engine.Media.SoundData.Load(Stream)` 实际把新增 WAV 解码为 1 声道、22050 Hz、22050 样本。
- 支持格式枚举包含 WAV/Ogg；本阶段只交付已经过目标解码器验证的 WAV。

上述是 Windows x25.01.17 ABI 结论。7723 同版本号可能被重打包，Android
资源加载与 OpenAL 行为仍须真机验证。

## 4. 模型、材质与枢轴

模型为项目生成的低多边形民用双座轮廓，共 **758 triangles**。静态机体合并
到一个 `VisualRoot` 网格，避免把座椅、门框等每个零件变成独立 draw mesh。
模型包含弧面下机身、渐缩机鼻、深色不透明风挡、左右门框/开口、双座、仪表
台、尾梁、尾翼、滑橇、桅杆、装饰条和红/绿/暖色航行灯几何。

骨骼层级和局部平移如下：

```text
HelicopterRoot
└─ VisualRoot                  translate (0, 1.00, -0.55)
   ├─ MainRotor               translate (0, 0.80, 0.25)
   ├─ TailRotor               translate (0.30, 0.20, 2.92)
   ├─ MainRotorBlur           translate (0, 0.80, 0.25)
   └─ TailRotorBlur           translate (0.30, 0.20, 2.92)
```

所以主旋翼世界枢轴为 `(0,1.80,-0.30)`，绕 Y；尾桨世界枢轴为
`(0.30,1.20,2.37)`，绕 X。`VisualRoot` 以两座之间为姿态中心，倾斜不会
修改 `ComponentBody` 的服务器/碰撞位置。物品栏使用同一模型和贴图，但隐藏
预建 blur 网格，显示静止实桨。

`Helicopter.png` 是 96×16、全不透明、12 色的单行材质调色板，区分白/红
油漆、深色玻璃、深灰机械结构、橡胶、金属、座椅、仪表灯、航行灯和 blur
桨。当前管线没有证据证明单一 `ComponentSimpleModel` 内可可靠排序半透明
玻璃，因此按约束降级为不透明深色玻璃；没有臆造逐材质透明 API。航行灯是
低成本可识别颜色几何，不是动态光源，因为已验证的 `EmissionColor` 作用于
整个 ComponentModel，而非单独网格。

## 5. 动画、视觉姿态与音频

旋翼状态机由现有 `EngineRunning` 和归一化 `RotorSpeed` 派生：Stopped、
Starting、Running、Stopping。加减速仍使用既有飞行状态；动画角速度使用
`dt`，角度以完整取模处理负值和大跨帧。solid/blur 是启动时已导入的固定
网格，通过骨骼缩放交叉切换，更新阶段不创建网格。

视觉俯仰来自车体前向速度，视觉侧倾来自横向速度和最短角 yaw rate；指数
阻尼与角度上限均由 XDB 配置。接地时目标回到中立。该矩阵只写
`VisualRoot`，不写 Body 的 pitch/roll、Position 或碰撞体，动画不改变物理。
座位继续跟随权威 Body；因表现倾斜不施加给玩家，极限姿态下玩家不会随视觉
机身完全倾斜，但枢轴位于座舱中央以最小化视觉偏差。

音频是 1 秒、mono PCM16、22050 Hz 的无缝整数周期合成循环，包含旋翼脉冲
与发动机谐波，不使用第三方录音。组件 Load 创建并缓存一个 Sound；状态变化
只 Play/Stop，逐帧按监听距离、转速和水平负载更新音量/音调；Dispose 明确
Stop+Dispose。远端实体基于其复制到本机的车体/座位状态产生本地空间音效，
没有新增音频网络包。

## 6. 核心兼容性与联机边界

- 已确认：ID 941、实体/组件 GUID、单 Mount 双座结构、驾驶员唯一输入、乘客
  无控制权、飞行参数与自动降落代码没有被重写或改协议。
- 已确认：实体保存仍只保存既有 EngineRunning/RotorSpeed 和原生 Body/父子
  关系；新增姿态角、动画角和 Sound 均为瞬态，不污染世界存档。
- 已确认：没有旋翼附属实体，不会因表现层重复生成网络实体。
- 已确认（代码/ABI）：远端机体位置继续使用原生父 Body 同步，座位继续用
  包 240；表现层不发送位置或座位包。
- 待验证：两个 Android 客户端能否看到一致的旋翼状态、姿态和听到合理声音，
  以及上下机、切世界、断线重连后的资源清理。
- 不支持：严格服务器权威飞行/抗作弊；V0.4 未替换目标游戏的客户端父 Body
  状态源。

## 7. 可配置参数

新增但不参与核心物理的 XDB 参数：`VisualPitchLimit`、`VisualRollLimit`、
`VisualResponse`、`VisualLateralReferenceSpeed`、`MainRotorAngularSpeed`、
`TailRotorSpeedMultiplier`、`RotorBlurThreshold`、`SoundMinDistance`、
`SoundRolloffFactor`。原有所有飞行参数和值保持不变。

## 8. 已执行验证与未执行验收

已执行结果记录在 `docs/TEST-RESULTS.md`：环境 ABI、41 项直升机规则、全量
离线回归、net472 Release、12 个 XDB、目标 DAE/WAV 导入及 `.netmod` 内容均
通过。静态预览还检查了机体、旋翼、滑橇和灯的相对位置；它不等同于游戏内
渲染。

以下仍为`待验证`，不得写成通过：Android 7723 创造栏/世界渲染、触屏起飞
与完整飞行、真实 OpenAL 声音、昼夜明暗、退出重进、两台真实客户端的位置/
座位/运动/旋翼/姿态/音效观察、断线重连及性能帧率。

## 9. V0.5 建议

1. 按真机截图微调机鼻风挡深度、座椅挂点和物品栏镜头；在有可靠逐材质透明
   证据后再考虑透明玻璃。
2. 用合法来源或项目自录的启动/怠速/飞行/停机多段资源替换单循环，并先验证
   Android 同时声源预算；当前单循环是低风险降级实现。
3. 两台 Android 完成矩阵后再决定是否需要显式同步 rotor state；当前设计只
   从已有座位/飞行状态派生，不新增协议。
4. 若要求玩家身体也随视觉倾斜，须先验证 Rider 视觉挂点和碰撞分离方案；不应
   直接旋转权威 Body 来追求画面效果。

## 10. 本次修改文件清单

- `README.md`
- `THIRD-PARTY-NOTICES.md`
- `docs/COMPATIBILITY.md`
- `docs/HELICOPTER-V0.4-AUDIT.md`
- `docs/TEST-RESULTS.md`
- `docs/VEHICLE-MULTIPLAYER-AUDIT.md`
- `scripts/Build-NetMod.ps1`
- `scripts/New-HelicopterAssets.ps1`
- `scripts/Test-Environment.ps1`
- `src/SCIEPort/Assets/Audio/SCIEPortHelicopterLoop.wav`
- `src/SCIEPort/Assets/Database/Helicopter.xdb`
- `src/SCIEPort/Assets/Models/Helicopter.dae`
- `src/SCIEPort/Assets/Textures/Helicopter.png`
- `src/SCIEPort/Features/AirVehicles/ComponentHelicopterDrive.cs`
- `src/SCIEPort/Features/AirVehicles/HelicopterBlock.cs`
- `src/SCIEPort/Features/AirVehicles/HelicopterFlightRules.cs`
- `src/SCIEPort/SCIEPort.csproj`
- `src/SCIEPort/modinfo.json`
- `tests/SCIEPort.AirVehicles.Tests/Program.cs`
- 仓库根 `docs/blueprint-block-reference.md`（明确 ID 941 仍不属于蓝图材料）。
