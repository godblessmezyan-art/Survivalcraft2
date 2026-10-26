# Stormhammer 正式资源与引擎审计

审计日期：2026-10-26
目标：Survivalcraft NET `Net_x25.01.17` / Android 7723 / `.netmod`
状态：Windows 目标程序集静态 ABI、Release 编译、目标模型导入、包校验 **已确认**；Windows 游戏运行、Android 7723、多人联机 **待验证**。

## 1. 实际引擎资源能力

本结论来自本机目标程序集 `E:\SCNET\SCNETx25.01.17`，不是按原版 SC、SCAPI 或模板猜测。

| 能力 | x25.01.17 实际位置 | 结论 |
| --- | --- | --- |
| Mod 3D 模型 | `Engine.Media.ModelData.Load(Stream)`；运行时 `ContentManager.Get<Model>("Models/...")` | 已验证 COLLADA 1.4.1 `.dae`、`Y_UP`、三角形索引、法线、单套 `TEXCOORD set=0`。Stormhammer 由目标加载器实测为 1 bone / 30 meshes。 |
| 纹理 | `ContentManager.Get<Texture2D>`；现有 Android 构建约束 | 使用 `.png`。项目已知 WebP 在 x25.01.17 Android 物品栏路径崩溃，构建继续禁止打包非 PNG 纹理。 |
| 音频 | `Game.IContentReader.SoundBufferReader.DefaultSuffix = { wav, ogg }`；`SubsystemAudio.PlaySound` | Stormhammer 使用原创 PCM WAV，经包签名校验；运行播放仍待客户端实测。 |
| 网格材质 | `BlocksManager.DrawMeshBlock(... BlockMesh, Texture2D, Color, ...)` | 每次调用只有一个纹理采样器，没有法线/粗糙度/金属度/PBR 材质槽，也没有第二张 emissive sampler。所有结构共用一张正式 UV 图集。 |
| 动态发光 | `SubsystemGlow.AddGlowPoint()` / `GlowPoint`；`ParticleSystem<T>` additive batch | 待机使用 cyan `GlowPoint`；蓄力、电弧、雷击、冲击使用有上限、短寿命的 additive 粒子。`StormhammerGlow.png` 经真实 ContentManager 路径加载，但当前 `DrawMeshBlock` 不直接绑定第二采样器。 |
| 粒子生命周期 | `SubsystemParticles.AddParticleSystem`、`ParticleSystemBase.Simulate` | 自定义粒子在 0.12–0.55 秒内结束并由子系统自动移除；一次电弧 28 粒子、一次冲击 38 粒子，不写地形。 |
| 模型变换 | `Block.GetFirstPerson*`、`Block.GetInHand*`、`Block.GetIcon*` | CSV 与 `StormhammerBlock` 分别提供库存、第一人称、第三人称变换。掉落物沿用原生固定 0.3 缩放、Y 轴旋转与上下浮动。 |

## 2. 四条实际展示路径

- 物品栏：`Game.InventorySlotWidget` → `BlockIconWidget` → `Block.DrawBlock`。图标观察偏移由 `StormhammerBlock.GetIconViewOffset/GetIconBlockOffset` 返回。
- 第一人称：`Game.ComponentFirstPersonModel.Draw` 读取 `GetFirstPersonRotation/Offset/Scale`，设置 `DrawBlockMode.FirstPerson`，再调用 `Block.DrawBlock`。
- 第三人称：`Game.ComponentHumanModel.Animate` 读取 `InHandRotation/Offset/Scale`，从手骨矩阵生成 `DrawBlockMode.ThirdPerson` 后调用 `Block.DrawBlock`。
- 地面掉落物：`Game.SubsystemPickables.Draw` 对无 `StuckMatrix` 的物品使用绕 Y 轴旋转矩阵、0.04 高度浮动和固定 `0.3f` 尺寸调用 `Block.DrawBlock`。

四条路径最终复用同一个 `StormhammerBlock.DrawBlock` 和同一个正式网格/图集，不存在临时图标方块或另一套低质手持资源。

## 3. 正式模型结构

资源：`src/SCIEPort/Assets/Models/Stormhammer.dae`

- 30 个独立网格结构、1368 三角面、单根渲染骨骼，符合 800–2500 三角面建议范围。
- 主锤头使用真正的斜切角几何（主面、12 条边倒角、8 个角面），不是简单立方体。
- 独立结构包括：主锤头、银钢中脊、双侧锻造打击框/凹面、8 个暗金护角、双侧嵌入雷纹面板、双侧六边形雷晶、连接轴、上固定环、十二边黑色握柄、112 段连续螺旋缠带、4 个握柄固定环、尾端金属配重和尾晶。
- 模型 `extra/SCIEPORT` 保存 `Grip`、`StrikeLeft`、`StrikeRight`、`ArcFront`、`ArcBack`、`Pommel` 六个特效/握持挂点；构建脚本逐名验证。
- 外形范围约 `X ±1.45 / Y -2.17..3.06 / Z ±0.71`，细节密度集中在锤头、握柄轮廓和晶体。

## 4. UV、贴图和材质层次

主图集：`src/SCIEPort/Assets/Textures/Stormhammer.png`（1024×1024 RGBA）

- 八个有边界的材质区：深铁灰、银钢、暗金、雷纹、黑色握柄、连接金属、蓝晶、凹刻。
- 每个几何面有显式 UV 三角形；构建校验 position/UV 一一对应，不使用“全模型单点取色”或无 UV 临时材质。
- 图集包含定向金属拉丝、随机细划痕、边框高光、分格磨损、皮革斜纹与晶体分面；抽样颜色数必须 ≥350，防止退化为纯色图。
- `StormhammerGlow.png`（1024×1024）仅保留雷纹和晶体发光区域。
- `StormhammerEffects.png`（256×256，4×4 粒子格）用于短电弧、火花、冲击和雷击轨迹。
- 目标引擎物品 `DrawMeshBlock` 只接受一张 diffuse texture；运行时发光采用“主图集亮色 + GlowPoint + additive 粒子”等效方案，不伪称支持多材质 emissive shader。

## 5. 动态视觉、声音与技能

- 待机：物品在世界/第一/第三人称绘制时触发单个呼吸式青蓝 `GlowPoint`；停止绘制超过 2 帧即移除。
- 蓄力：`SubsystemStormhammerBlockBehavior.OnAim(InProgress)` 每 0.09 秒最多生成一次短弧，并播放 `Charge.wav`。
- 普通挥击：真实 `ModLoader.OnMinerHit` Hook 仅在活动物品 ID=941 时触发短电弧、冲击火花和 `Swing.wav`；命中率固定为 1，攻击力下限 12。
- 召雷：蓄力至少 0.55 秒后，在视线目标上方 10 格绘制向下电弧和地面冲击粒子，播放 `Thunder.wav`。它不调用 `ChangeCell`、爆炸或天气雷击 API。
- 连锁：权威端从命中点选择半径 6 内最近生物，最多 3 跳，每跳即时重新选择且不重复目标；伤害递减。
- 冷却：粒子按各自寿命衰减，GlowPoint 回归待机脉冲。当前没有独立长冷却数值；真机手感验证前不伪称已调优。
- 音频：`Swing.wav` 0.48 秒、`Charge.wav` 1.35 秒、`Thunder.wav` 2.15 秒，44.1 kHz / mono / PCM16，均由项目脚本原创生成。

## 6. 联机和权限边界

- 普通客户端不会执行召雷伤害或耐久写入；会显示“需由主机执行”的明确提示。
- Local/Server 使用 `ComponentMiner.AttackBody` 结算首目标和链目标，复用原生友伤、护甲、生命和击退路径。
- 当前没有 Stormhammer 专用网络包，因此“客户端发起蓄力 → 主机执行 → 所有客户端同步自定义粒子/声音”尚未实现，联机状态为 **待验证**，不能标记兼容。
- 普通近战仍走 SCNET 原生命中/生命同步路径，但新增视觉是否在远端可见也尚未实测。

## 7. 可复现制作与截图

执行：

```powershell
python SCIE-Port/scripts/New-StormhammerAssets.py
```

会重建 DAE、三张 PNG、三段 WAV，以及基于真实三角形/UV/主图集的软件渲染预览：

- `docs/images/stormhammer-asset-preview.png`
- `docs/images/stormhammer-inventory-preview.png`
- `docs/images/stormhammer-concept-reference.png`（设计基准，不参与游戏打包）

预览证明资源文件结构和 UV 可渲染，但不等同于游戏截图。正式验收仍需在 Windows 客户端和 Android 7723 分别截取：创造栏 64px/128px、第一人称待机/挥击/蓄力、第三人称正侧背、地面掉落、晴天直射/阴影/夜间、召雷和三跳连锁；同时记录 10 分钟战斗帧时间、峰值粒子数、温度与内存。

## 8. 当前结论

- 已确认：实际 ABI 取证、模型/UV/贴图/遮罩/粒子图/音频文件、四路径共享渲染接线、动态效果代码、权威端技能边界、Release 编译、目标 `ModelData.Load` 导入、`.netmod` 内容和哈希。
- 待验证：Windows 游戏内视觉、图标缩小识别度、第一/第三人称手部贴合、掉落物比例、不同光照、WAV 实际播放、Android 7723 GPU/CPU/内存/发热、主机与客户端同步。
- 不支持：当前单纹理 `DrawMeshBlock` 中直接绑定第二张 emissive 材质；普通客户端自行执行召雷伤害。
