# Portable Fortress — SCIE-Port 独立玩法

状态：**离线已确认 / 真机待验证**。目标运行时 SCNET `Net_x25.01.17`，Android 7723，产物 `.netmod`（`SCIEPort.netmod` 独立提供，不依赖 Builder Assistant）。

## 归属与迁移

Portable Fortress 原先被开发在 Builder Assistant 内，2026-10-26 起正式归入 SCIE-Port：

| 资产 | 现位置 |
| --- | --- |
| 模型 | `src/SCIEPort/Assets/Models/PortableFortress.dae` |
| 控制核心模型 | `src/SCIEPort/Assets/Models/FortressCore.dae` |
| 贴图 | `src/SCIEPort/Assets/Textures/PortableFortress.png`（1024 图集）、`FortressGlass.png`、`FortressGlow.png`、`FortressCore.png` |
| 设计规格 | `docs/portable-fortress/{DESIGN,LAYOUT,COLLISION,INTERACTION}.md` |
| 资产生成器（模型/图集/贴图/预览一次产出） | `scripts/New-PortableFortressArt.py` |
| 模型导入校验 | `scripts/Test-PortableFortressModel.ps1` |
| 预览（软件渲染，非截图） | `docs/portable-fortress/previews/` |
| 玩法代码 | `src/SCIEPort/Features/PortableFortress/` |
| 离线规则测试 | `tests/SCIEPort.PortableFortress.Tests/` |

迁移保持了 11×13×9 游戏格尺寸、双层空间、全景观察舱、八级楼梯、屋顶与护栏、储物与供暖预留位。唯一几何改动：原白模把 LAYOUT 中"屋顶通道竖井"的预留体积填成了一个实心盒子，导致预留的爬升体积**不可通行**；现在该体积内是梯级横档加两侧导轨。正式美术重构后为 387 个部件 / 13470 三角面 / 1 骨骼 / 11 网格，尺寸与朝向完全不变。

Builder Assistant 侧的旧副本仍然保留，仅作回滚参照，不参与 SCIE-Port 的编译与打包。

## 正式美术与入口修复（2026-10-26）

### 为什么旧版在游戏里是黑的

不是光照问题。旧贴图是 **64×64、只有 4 种颜色**（主色 `#2E3238`），而旧生成器对每个面都写 `uv=(0,0)..(1,1)`，于是整座堡垒的每个面都在采样同一张近黑图。修光照不可能救回来，必须换贴图与 UV。

### 现在的资产

- **1024×1024 UV 图集**，16 个材质区：雾蓝装甲 `#687B88`、深色承重 `#35444B`、银灰结构 `#A6B1AF`、古铜边框 `#AE8456`、米灰墙面 `#DDD2BC`、暖木 `#996F4D`、皮革/布料/帆布、金属、炉火 `#E9AA68`、屋顶。
- 每个面按约 1 格细分后映射到**该部件声明的区域**，UV 向内缩 2 texel（模型着色器用 `PointClamp`，边上会串色）。
- 内部面剔除：被另一盒体对面完全覆盖的面不再输出，省三角面并保证预览排序正确。
- 生成器对越界部件直接抛异常，包围盒 X `±5.5` / Z `±6.5` / Y `0..9` 是生成期硬约束。

### 室内

一层：暖木地板、双层床（床架/床垫/枕头/被褥/立柱/爬梯横档）、有门板与把手的储物柜、面向房间的机械供暖炉（炉膛带火光材质、烟道、炉排）、小桌与两把椅子、开放式书架（书可见）、带工具挂架的工作台、两盏壁灯、前墙挂图板。
二层：指挥台（台面/仪表/操纵杆）、左右休息长椅（坐垫/靠背/支腿）、海图台、吊灯、屋顶通道竖梯（9 级横档 + 导轨）。
屋顶：古铜护栏（立柱 + 双道横杆）、通行舱口（带盖板）、设备底座（带两颗导轨与信标桅杆）。

中央 4×4 活动区、门口走廊与楼梯通行路线保持畅通，所有家具与碰撞代理同源设计。

### 入口卡脚的根因与修复

真机现象是"进后门必须跳一下"。根因在碰撞而不在模型：

1. 一层地板代理原本一直铺到包封后缘 `z=6.5`，在门洞里形成一道 **0.36 高的 body 唇边**。
2. `ComponentBody.HandleAxisCollision` 会把"地形推回"和"实体推回"分别算出来取较大者；**地形会被 `MaxSmoothRiseHeight` 平滑抬升忽略，实体永远不会**。所以这道唇边是不可跨越的墙。
3. 三格隐藏地形门槛确实存在，但它们与那道 body 唇边同深度，被完全挡住，从来没起过作用。

修复分三层，缺一不可：

| 层 | 改动 |
|---|---|
| 几何 | 地板代理止于后墙内表面 `DoorInnerZ = 6.20`，门洞地面改由隐藏地形块承担 |
| 楼梯 | `StairRise` 改为 `(DeckTop - FloorTop) / 8 = 0.435`，最后一级踏面与二层甲板**齐平**，消除顶部 0.12 凸缘 |
| 兜底 | `FortressTraversalRules` 新增入口条带目标高度（`|x| ≤ 1.45`、`z ∈ 5.45..6.50`），上限就是地基面 0.36，只抬不压、跳跃时不介入 |

诊断：`FortressOptions.Diagnostics` 打开后，每 0.1 秒输出一行
`zone/world/local/feet/vy/under=索引:名称/against=索引:名称/assist`，
可直接看出脚底 Y、水平位置、正好在脚下的代理、卡住身体的代理以及抬升是否生效。开关持久化在 `SCIEPortFortressDiagnostics`。

### 观察窗

`SubsystemModelsRenderer` 只有 `ShaderOpaque` 与 `ShaderAlphaTested` 两条通道，**没有混合通道**。所以玻璃不能做进 DAE（会被裁成洞或被画成不透明板）。改为独立绘制通道 `FortressGlazingRenderer`：`BlendState.AlphaBlend` + `DepthStencilState.DepthRead` + `CullNone`，按距离由远及近排序。碰撞由 `FortressZone.Glazing` 薄代理（厚度 ≤ 0.09）承担。

### 照明

实测可用的地形光源（`Block.DefaultEmittedLightAmount` → `TerrainUpdater.GenerateChunkLightSources`）**没有采用**：实体外壳不挡地形光，内部一颗灯会把半径十余格的外部地面一起点亮。实际采用 ① 低强度暖色 `EmissionColor` 只作"夜里不纯黑"的可见度下限；② 炉膛/壁灯/吊灯的附加混合暖光几何。逐房间照明在 `CalculateModelLight` 单点采样下不可实现，这一条写在 `MODEL-AUDIT.md` 里。

## 玩家流程

1. 创造物品栏 `Items` 分类 → **堡垒展开核心**（内容编号 944）。
2. 手持物品**对准方块顶面**点击。
3. 系统按视线水平方向吸附四向朝向，向前推 7 格定位堡垒原点（原点=地基中心，后门朝玩家）。
4. 预检通过后：在点击格放置**控制核心**（945），在门洞后侧一行放置 3 格隐藏地基（946），再分批建立锚点实体与碰撞代理。
5. 展开完成后才消耗道具；任何一步失败都回滚已写的格子并保留道具。
6. 从后门进入一层，沿楼梯上二层，经屋顶通道竖井上屋顶。

## 方块编号

| 内容 | 编号 | 说明 |
| --- | --- | --- |
| 堡垒展开核心（物品） | 944 | 创造栏可见，不可放置、无碰撞、堆叠 1 |
| 堡垒控制核心（已放置） | 945 | 无碰撞、不显示在创造栏、无掉落；回收入口 |
| 堡垒地基（隐藏） | 946 | 碰撞仅 0..0.36，不可手放、无掉落、不显示在创造栏 |

编号扫描依据：SCIE-Port 已占 903–943 与 995–1000；Builder Assistant 占 900–904（与本工程已存在重叠，属既有历史问题，见"已知限制"）。因此本玩法取 944–946，并在构建脚本里校验编号未漂移。

## 为什么需要 4 格地形写入

引擎证据（x25.01.17 反编译）：

- `Game.ComponentBody.HandleSmoothRise` 只把 `FindTerrainCollisionBoxes` 的结果喂给抬升判定：**玩家可以踩着地形格上台阶，但永远无法踩着另一个 `ComponentBody` 的碰撞盒上台阶**。
- `Game.ComponentBody.FindBodiesCollisionBoxes` 通过 `SubsystemBodies.FindBodiesAroundPoint(..., 4f)` 查找其它实体，因此**单个碰撞代理不能过大**，否则站在它边缘的玩家查不到它、会直接穿过。
- 方块的自定义碰撞盒是**格内局部坐标**（`FindTerrainCollisionBoxes` 用 `cell + box.Min/Max`），所以 0.45 高 / 0.5 深的原设计楼梯无法用 1 格颗粒度的地形方块复刻。

据此确定：

1. 一层地板 0..0.36、二层甲板 3.60..3.84、屋顶 7.15..7.41 用**分段实体碰撞代理**（每段半对角线 ≤ 4），保证站立与水平阻挡；
2. 入口 0.36 台阶用 **3 格隐藏地基**（唯一的地形台阶，玩家可以原生平滑抬升走入）；
3. 主楼梯与屋顶竖井用**有界的垂直抬升辅助**（`FortressTraversalRules`，上限 4.5 m/s，与原生 `HandleSmoothRise` 同一速率；只在玩家位于楼梯/竖井体积内、且未跳跃/坠落时抬升，绝不把玩家往下拉）。

## 碰撞代理清单

`PortableFortressGeometry.Segments` 由 `docs/portable-fortress/LAYOUT.md` 的同一组数字派生：

| 区域 | 代理 |
| --- | --- |
| 一层地板 | 3×4 分块，顶面 0.36 |
| 二层甲板 | 3×3 分块，扣除楼梯井 `x[-4.3,-2.2] z[-4,0.25]` |
| 屋顶 | 分块，扣除舱口 `x[2.3,4] z[2.7,4.4]` |
| 外墙 | 前/后/侧墙、四角斜面、后门两侧与门楣 |
| 隔墙 | 气闸两道、供暖隔断 |
| 二层外壳 | 侧墙下槛/上楣、后墙上段、观察舱下槛/上楣（**窗洞不封**） |
| 楼梯 | 8 级踏步 |
| 屋顶护栏 | 前/后/左/右，舱口侧留通行口 |

门洞、观察窗、楼梯井、屋顶舱口在离线测试里逐项断言"没有被代理封死"。

## 部署安全

- 只允许单人（`WorkType.Local`）或联机房主（`WorkType.Server`）；`WorkType.Client` 直接拒绝。
- 预检覆盖**整个** 11×13×9 占地区域：143 列逐列检查区块就绪、脚下实地、垂直 9 格全为空气；任何非空气方块（含流体）即拒绝，默认绝不覆盖。
- 玩家/生物实体盒与占地（外扩 0.25）相交即拒绝。
- 高度越界、模型不可用、同实例重复一律拒绝。
- 地形写入只有 4 格，写入前先捕获原值；失败按逆序还原。
- 实体分批创建（每 tick 8 个），全部成功才把状态置为 `Deployed` 并消耗道具。
- 模型加载失败由 `ContentManager.Get<Model>` 在方块初始化阶段暴露，部署路径不会因此消耗道具。

## 存读档

- 锚点实体持久化 `FortressId`、朝向、原点、核心格、修订号与状态；碰撞代理持久化 `FortressId` + 段索引。
- 读档时 `Component.Load` 触发 `SubsystemPortableFortress.Attach/AttachSegment` 重建注册表。
- 每 tick 校正代理位置（漂移 > 0.02 时重设），代理质量 100000、关闭重力、速度归零。

## 回收接口（预留，本轮不拆除）

`FortressRecoveryRules` 只做判定，不执行拆除：

- 展开中 → `Busy`；故障实例 → `Faulted`；找不到实例 → `Unknown`；
- 已部署 → `Reserved`，控制核心交互回复"回收接口已预留，本轮不执行拆除"。

真实拆除需要 48 格储物刷写、占用检查、地形日志逆序还原与代理 ID 精确清理；在这些具备之前不做半成品拆除，避免吞掉玩家物资或重复返还展开核心。

## 构建与离线验证（2026-10-26）

```text
./scripts/New-PortableFortressModel.ps1
./scripts/New-PortableFortressAssets.ps1
./scripts/Test-PortableFortressModel.ps1 -GameDir <x25.01.17 目录>
./scripts/Build-NetMod.ps1 -GameDir <同上> -Configuration Release
```

- 模型导入：`PASS`，1 骨骼 / 11 网格 / 1248 三角面，包围盒 11 × 13 × 9。
- 离线规则：`SCIEPort.PortableFortress.Tests` 291 断言通过。
- 编译：net472 Release 0 warning / 0 error；15 个 XDB `DATABASE VALIDATION PASSED`。
- 产物：`artifacts/SCIEPort.netmod` 2,323,919 字节 / 87 条目，SHA-256 `89FCCE29A77224F98D915D3D645DF6BEDBA3D80903964296DF008CA54C4159CB`，modinfo `0.29.0`。

## Android 7723 实测步骤（待验证）

1. 备份存档，新建可丢弃的创造模式单人世界。
2. 把 `artifacts/SCIEPort.netmod` 放入该客户端实际使用的 `NetMods` 目录；日志应出现 `[SCIEPort]` 初始化信息。
3. 创造栏 `Items` 找到"堡垒展开核心"，在平整空地（≥11×13 且无树/水）对准顶面点击。
4. 核对：控制核心出现在点击格、堡垒朝视线方向展开、门的朝向正确；四个朝向各测一次。
5. 走入一层：不应被 0.36 台阶挡住，也不应浮空；沿主楼梯上二层，确认不卡台阶、不掉楼。
6. 经屋顶竖井上屋顶，检查护栏能挡住、舱口能上人、观察窗视野未被封。
7. 保存退出再进入：堡垒模型、代理碰撞、控制核心仍在；再走一遍楼梯与屋顶。
8. 反例：占地内有方块/树/水、站在占地内、贴着后门太近、以及作为普通联机客户端使用时，都应得到具体错误提示且**道具不被消耗**。
9. 记录帧时间（首次展开与行走）、是否有穿模、是否卡在代理接缝。

## 已知限制（全部待验证或明确不支持）

- 真机渲染、行走、存读档、四向旋转、帧耗时：**待验证**。
- 联机：普通客户端不展开、不同步，主机侧行为**待验证**。
- 储物 48 格、供暖、玻璃透明、原生对话框选择器：**不支持/未实现**。
- 回收：**预留**，不执行。
- Builder Assistant 与本工程在 900–904 存在编号重叠（历史遗留）。两者不应同时安装；Portable Fortress 已不依赖 Builder Assistant。
- 楼梯与竖井依赖本站的垂直抬升辅助；若真机手感不佳，替代方案是把主楼梯改为 7 级整格地形台阶（需调整楼梯井尺寸），或在竖井内改为原生梯子方块。
