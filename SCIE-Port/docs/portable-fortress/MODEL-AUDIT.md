# Portable Fortress Mk.I — 模型与引擎审计（Hearthguard 版）

> **归属：** 由 **SCIE-Port** 维护。本文所有路径与结论都对应 SCIE-Port 目录下的版本。
> 最近一次重写：2026-10-26，正式美术重构（分支 `feature/fortress-hearthguard-art`）。

## 1. 为什么旧模型在游戏里是黑的

结论来自源码与生成物两侧的取证，不是猜测：

| 环节 | 取证 | 结果 |
|---|---|---|
| 贴图 | `Assets/Textures/PortableFortress.png` 为 **64×64**、只有 **4 种颜色**，主色 `#2E3238`（3488/4096 像素） | 整张贴图本身就是近黑 |
| UV | 旧 `New-PortableFortressModel.ps1` 的 `Add-Box` 对**每个面**都写 `uv = (0,0),(0,1),(1,1),(1,0)` | 所有面、所有部件都采样同一张整图 |
| 材质 | DAE 只有一个 `WhiteboxMaterial`（Lambert，diffuse `0.72 0.75 0.78`） | 没有任何按部位分配材质的通道 |
| 光照 | `SubsystemModelsRenderer.DrawInstancedModels`：`modelShader.Texture = componentModel.TextureOverride`，光照来自 `CalculateModelLight`（每模型单点采样） | 贴图不是丢失，是贴图本身黑 |

因此"调亮光照"不可能修好它：**没有任何一张亮贴图可供采样**。这就是本轮把贴图与 UV 一起重做的原因。

## 2. 新的资产管线

唯一的生成脚本：

```text
scripts/New-PortableFortressArt.py
```

它同时产出（全部可重复、无随机数状态）：

| 文件 | 内容 |
|---|---|
| `src/SCIEPort/Assets/Models/PortableFortress.dae` | 堡垒世界模型，1 骨骼 / 11 网格 |
| `src/SCIEPort/Assets/Models/FortressCore.dae` | 展开核心物品与已放置核心的模型 |
| `src/SCIEPort/Assets/Textures/PortableFortress.png` | **1024×1024 UV 图集**，16 个 256 px 材质区 |
| `src/SCIEPort/Assets/Textures/FortressCore.png` | 物品调色带（48×8） |
| `src/SCIEPort/Assets/Textures/FortressGlass.png` | 窗面叠加图（含真实半透明像素） |
| `src/SCIEPort/Assets/Textures/FortressGlow.png` | 暖光光斑（含真实半透明像素） |
| `docs/portable-fortress/previews/*.png` | 7 张软件渲染预览 |

旧的 `New-PortableFortressModel.ps1` 与 `New-PortableFortressAssets.ps1` **已删除**。若保留它们，任何人重新运行都会静默覆盖正式资产，这正是任务书禁止的情形。

### 图集分区

| 区 | 用途 | 基色 |
|---|---|---|
| `armor` | 雾蓝装甲板 | `#687B88` |
| `armor_dark` | 深色承重结构、炉体 | `#35444B` |
| `silver` | 银灰结构件、栏杆立柱 | `#A6B1AF` |
| `bronze` | 古铜机械边框、门框、把手 | `#AE8456` |
| `plaster` | 室内米灰墙面 | `#DDD2BC` |
| `wood` / `wood_dark` | 暖木地板、家具、梁 | `#996F4D` / `#6E4E36` |
| `leather` / `cloth` / `canvas` | 床铺、坐垫、书本、海图 | — |
| `metal` | 工作台台面、炉排、管线 | `#8C9499` |
| `fire` | 炉膛与灯芯 | `#E9AA68` |
| `roof` | 屋顶甲板与肩部封板 | `#4C5C66` |

每个面按约 1 格的尺寸细分后，映射到**该部件声明的区域**；UV 向内缩 2 texel，因为模型着色器用 `PointClamp`，边界上的 UV 会串到相邻材质。这条限制写在 `docs/VEHICLE-MODEL-GUIDE.md` 第 5 节。

### 内部面剔除

`emit_all()` 会丢弃被另一盒体对面**完全覆盖**的面。两个原因：一是 Android 上白交三角面，二是被埋的面与它上方可见面几乎共深度，任何画家算法预览都会画错序（重构过程中确实出现过"基座透过木地板显示为深色条带"）。

## 3. 生成物契约（目标引擎实测）

```text
scripts/New-PortableFortressArt.py
scripts/Test-PortableFortressModel.ps1 -GameDir E:\SCNET\SCNETx25.01.17
```

```text
Portable Fortress Hearthguard model: PASS
  Target importer: Engine.Media.ModelData.Load(Stream)
  Bones/Meshes/Triangles: 1/11/13470
  Bounds: 11 x 13 x 9 (X -5.5..5.5, Z -6.5..6.5, Y 0..9)
```

| 项 | 值 |
|---|---|
| 格式 / 轴向 / 单位 | COLLADA 1.4.1 / Y_UP / metre=1 |
| 场景节点 | 1 个 `PortableFortress`，identity 矩阵 |
| 导入骨骼 / 网格 | 1 / 11 |
| 部件数 | 387 |
| 三角面 | 13470 |
| 精确包围盒 | X `-5.5..5.5`、Y `0..9`、Z `-6.5..6.5` |
| 顶点流 | position / normal / UV，交错 offset 0/1/2 |
| 开口 | 大门、观察窗、楼梯井、屋顶舱口都是真实洞口 |

生成器对每个盒体做包封断言：任何部件超出 X `±5.5`、Z `±6.5`、Y `0..9` 会直接抛异常。因此包围盒不是"事后核对"，而是生成期的硬约束。

## 4. 玻璃：为什么不放进 DAE

`SubsystemModelsRenderer.DrawInstancedModels` 只会在 `ShaderOpaque` 与 `ShaderAlphaTested` 之间二选一（由 `alphaThreshold` 是否为空决定），**没有混合通道**。所以：

- 把玻璃做进 DAE → 要么被 alpha test 裁成洞，要么变成不透明蓝板挡住视线，两者都不合格；
- 用单独的不透明方块冒充 → 明确禁止。

实现方式是独立绘制通道：`Features/PortableFortress/FortressGlazingRenderer.cs`，用 `PrimitivesRenderer3D.TexturedBatch` + `BlendState.AlphaBlend` + `DepthStencilState.DepthRead` + `RasterizerState.CullNone`，按到相机距离由远及近排序后提交。同一通道顺带绘制炉火与壁灯的暖光光斑（`BlendState.Additive`）。

碰撞由 `FortressZone.Glazing` 的薄代理承担（厚度 ≤ 0.09），既不挡视线也不允许穿过。前部斜面观察舱无法用轴对齐代理表达 42° 斜面，改用两块角部块代理（`NoseGuard*`），它们落在玻璃转角内侧，不会侵入甲板。

## 5. 照明：验证过什么，放弃了什么

**已验证可用**的引擎能力：`Block.DefaultEmittedLightAmount` + `Block.GetEmittedLightAmount(value)` 会被 `TerrainUpdater.GenerateChunkLightSources` 读取并注入地形光照传播，因此隐藏发光方块确实能点亮地形。

**最终没有采用**，原因具体：实体外壳是模型，不是地形方块，**不参与挡光**。一颗 15 级光源放在堡垒内部，会把半径十余格的外部地面一起照亮，形成一圈明显不属于原世界的亮斑，违反"不修改世界"的要求。要消除泄漏需要给堡垒做地形级别的遮光外壳，代价远超收益。

实际采用的是两条互不冒充的手段：

1. **可见度下限**：`ComponentPortableFortress.Load` 给模型组件设 `EmissionColor = (0.10, 0.062, 0.034)`。这是"夜里不至于纯黑"的保底，不是照明。
2. **真实可见的暖光源几何**：炉膛、两盏壁灯、二层吊灯由附加混合通道绘制暖光光斑，模型里也有对应的炉口与灯罩实体。

限制必须说清楚：`CalculateModelLight` 每个模型只采样一次（`body.Position + 0.95 × 模型高度`），模型着色器也没有逐材质自发光，所以**无法**做到"只有室内亮、室外不受影响"。逐房间照明在 x25.01.17 的模型管线上不可实现。

## 6. 预览

| 文件 | 视角 |
|---|---|
| `previews/front.png` | 正面（全景观察舱） |
| `previews/side.png` | 东侧立面 |
| `previews/rear.png` | 背面（主入口） |
| `previews/entrance.png` | 入口近景 |
| `previews/interior-lower.png` | 一层生活空间 |
| `previews/interior-upper.png` | 二层观察舱 |
| `previews/roof.png` | 屋顶甲板 |

这些是生成器自带的软件渲染输出（画家算法 + 背面剔除），**不是游戏截图**。它们用于核对体量、分区与材质分配，不能代替真机验收。

## 7. 验证状态

| 项 | 状态 | 依据 / 剩余工作 |
|---|---|---|
| 图集与 UV 分区 | 已确认（构建守卫） | 1024×1024、全不透明、16 区、网格采样 ≥ 48 色 |
| DAE 结构与目标导入 | 已确认（目标程序集） | `Engine.Media.ModelData.Load` 返回 1 骨骼 / 11 网格 / 13470 三角面 |
| 精确包封与开口 | 已确认（生成期断言 + 校验脚本） | 生成器对越界部件直接抛异常 |
| 玻璃渲染路径 | 已确认（引擎源码） | `SubsystemModelsRenderer` 无混合通道；改用独立 alpha 混合通道 |
| 玻璃观感 / 排序 / 安卓开销 | 待验证 | 未真机运行 |
| 内部照明观感 | 待验证 | 未真机运行；逐房间照明不支持（见第 5 节） |
| 模型在游戏内的朝向与比例 | 待验证 | 未启动游戏 |
| 入口无需跳跃 | 待验证（逻辑已确认） | 离线断言覆盖几何与辅助边界，真机未跑 |
| 7723 渲染与帧耗时 | 待验证 | 无设备会话 |
| 联机部署 / 存读档 | 待验证 | 无两端联机 |
| 储物 48 格 / 供暖功能 | 不支持 | 仅有模型与预留结构，没有功能实现 |
