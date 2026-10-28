# 四圣兽 V3 第一阶段：青龙冠（0.42.0）

后续0.43.0已按用户要求补齐其余三件并改为统一验收，见FOUR-SAINTS-V3-FULL-ARMOR-AUDIT.md。下文保留0.42.0阶段的历史证据。

日期：2026-10-28。阶段二至四未执行。用户要求由其亲自完成游戏内与 Android 验收，本次只交付可测试候选包。

## 实际交付

- `Assets/Models/QinglongCrown.dae`：真实封闭厚度网格，1 骨骼、31 个结构网格、680 三角面。运行时合并为一个缓存 BlockMesh/共享材质批次。
- 双后掠渐尖龙角、青铜角座、凸起额鳞和青玉、两侧龙须护颊、三段后脑甲片和短鳍；脸中央保持开放。八色色板纹理 128×16，不靠发光平面形成外形。
- `QinglongCrownRenderer.cs`：常驻附件与物品栏模型缓存。现有 `OnModelRendererDrawExtra` 直接接入，早于 V2 技能符纹逻辑，穿戴原生方块 203 / 数据 200 时独立绘制，不依赖包245技能事件。模型图标经现有 ClothingBlock.DrawBlock 覆盖，仅影响数据200；其他装备仍使用原生图标，留待后续阶段。
- `.clo`、基础层贴图、角色规则、数值、伤害、技能、物理、GUID、网络包240–245均未修改。换装/死亡立即停止提交；第一人称近头0.45米、48米外以及人物不可见/淡出时裁剪。纹理通过 ContentManager 缓存，模型合并及人物绑定修正只执行一次，不逐帧创建网格、纹理或状态列表。
- 用户要求直接测试，`PreviewEnabled` 默认 true。`SCIEPortQinglongCrownV3Preview` 可关闭候选附件，`SCIEPortQinglongCrownDiagnostics` 默认 false；在 Settings.xml 的 Settings 根属性开启后，可显示头骨局部 XYZ（红X/绿Y/蓝前方-Z）。先用调试轴校准，再关闭调试轴检查候选模型。未增加玩家常规操作界面。

## 目标 API 与坐标证据

从 SCNET 项目 GitHub `x25.01.17` 发布页下载 Windows ZIP，解压至仓库外 `D:\Games\SCNET-x25.01.17`。

- Survivalcraft.exe SHA256：`3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`
- Engine.dll SHA256：`98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`
- EntitySystem.dll SHA256：`7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`
- SCIE 上游基线：`4864d956793b6e670413019f1f6b1cc5c8bb0970`（本次模型原创，不使用上游装甲资源）。

目标 `SubsystemModelsRenderer.DrawInstancedModels` 在人物实体网格提交后调用现有 OnModelRendererDrawExtra，动画已经写入 `AbsoluteBoneTransformsForCamera`。`m_headBone`、`Opacity`、`IsVisibleForCamera`、DrawMeshBlock 显式纹理重载与 FlatBatch3D.QueueLine 均从实际程序集反射核实。

人物 HumanMale 的 Body 绑定线性矩阵含0.02413缩放及Z-up到Y-up旋转，Female含0.0229235缩放。直接给头骨叠加米制附件或只归一化骨骼坐标轴都会错。缓存 `inverse(headAbsoluteBind 的无平移线性部分)`，绘制矩阵为：

    authorMetersToHeadLocal * currentHeadCameraSpace * camera.InvertedViewMatrix

保留完整动画矩阵，不使用躯干位置估算头部，不做滞后插值。男女头骨绑定、四种合成角度及非单位相机矩阵共8次往返误差小于0.0001米；这些是矩阵数学测试，**不证明跑步/跳跃/游泳/骑乘游戏动画通过**。

目标 DrawMeshBlock 会在 environment.ViewProjectionMatrix 非空时先在CPU投影。附件保持此项为 null，在 Flush(camera.ViewProjectionMatrix) 只投影一次，避免重复投影。地形光照从头骨所在格获取，模型不使用自发光标志或常驻粒子。

## 可复现与构建

`node scripts/New-QinglongCrownAssets.mjs` 仅依赖 Node 内置 fs/path/zlib，输出DAE、PNG、六视图与白模审查图。生成器的灰色头部是近似审查代理，非游戏资产、非游戏截图，不能代替实机校准。

`Test-QinglongCrown.ps1 -GameDir ...` 校验每个真实三角面非退化、索引、结构预算、材质尺寸/不透明度、目标 Engine.ModelData.Load 与真实男女骨骼矩阵。Build-NetMod 已接入此校验，并强制验证包内两个新增资源路径。

本机使用.NET SDK 9.0.300。因无管理员权限安装全局Targeting Pack，使用Microsoft官方NuGet包 Microsoft.NETFramework.ReferenceAssemblies.net472 1.0.3，设置 FrameworkPathOverride 到仓库外的 v4.7.2 引用目录；环境脚本仍校验mscorlib.dll与FrameworkList.xml。

完整构建首次发现旧PNG字节重现守卫受本机GDI+编码差异影响：青龙基础贴图443字节，重新编码420字节，逐像素ARGB差异0。已改为完整解码ARGB逐像素比较，未修改任何基础贴图，也未跳过像素验证。

实际命令与最终包证据见 TEST-RESULTS.md。没有修改Builder Assistant或方块能力，因此blueprint-block-reference.md内容无需变更。

## 验收状态

| 项目 | 状态 | 证据/限制 |
|---|---|---|
| 真实几何/导入/原点基变换/编译打包 | 已确认 | 目标引擎1骨骼/31网格/680三角面；完整构建通过 |
| Windows原版启动 | 已确认 | 首次启动至MainMenu；用户Escape停止界面操作后不再自动操作 |
| 带Mod加载、正常穿戴/图标/多姿态/卸装 | 待验证 | 未把原版主菜单误写成装甲验证；用户要求自行实测 |
| Android7723单机、触控、帧率 | 待验证 | 未连接Android设备 |
| 主机/普通客户端/观察者分别观察附件 | 待验证 | 使用现有衣物同步，尚无双机证据 |
| 玄武/白虎/朱雀独立立体附件 | 待验证 | 第二阶段尚未实施 |

用户测试时请确保NetMods只留一个SCIEPort包；用可丢弃备份世界先单独穿戴万里青龙，再与其他三件混穿，男女角色分别检查前后左右、站立/奔跑/跳跃/游泳/骑乘、第一人称、换装/死亡/读档以及主客机。该候选包默认启用冠体，但不代表这些验收项已经通过。
