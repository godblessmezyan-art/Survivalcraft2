# Builder Assistant 蓝图方块能力清单 v0.24.0

更新于2026-10-31。支持 **81种**原版材料、10种结构块家具模板；本轮新增18种地形和装饰材料。格式见[统一规范](BLUEPRINT-SPEC.md)，精确合法DATA见[代码生成矩阵](blueprint-block-capabilities.json)，提示词见[AI生成提示](AI-BLUEPRINT-PROMPT.md)。schema1/2/3继续可读，新输出使用schema3。

Windows固定哈希x25.01.17的ABI及隔离规则验证与Android7723实机分开记录；真实游戏、手机行为和联机同步均待验证。SCIE-Port是独立工程，神器/载具及其ID不能用于蓝图。

2026-10-31 SCIE-Port 0.64.0只改阿比斯军势/技能，夜魇战马为运行时原生实体，次位面保存非建筑能力；81种材料、族、DATA与蓝图限制保持，不新增可建造方块。

2026-10-31 SCIE-Port 0.63.0双权柄：阿比斯952、DATA及所有蓝图材料/族规则不变；新增死亡技能、运行时契约生物和252协议不属于可建造方块，不能写入蓝图JSON。无新增地形/建筑写入能力。

## v0.24.0 新增材料

| 类型 | f | 精确d | 支撑与自然行为 |
|---|---|---|---|
| DirtBlock | cube | 0 | 泥土；邻近草地可自然扩散到泥土 |
| IceBlock | cube | 0 | 冰；天气或其他原生机制可破坏/改变，不冻结环境 |
| SandBlock / GravelBlock | falling | 0 | 沙/砂砾；Y>0需下方完整顶面或耕地，按依赖先建支撑，完工复核 |
| GrassBlock | grass | 0/1 | 0普通、1积雪边缘标记；由上方积雪和光照等环境重算，不是颜色 |
| SoilBlock | soil | 0..7 | 位0湿润，位1..2肥力0..3；会受踩踏、覆盖、灌溉等影响 |
| SnowBlock | snow | 0 | 固定1/8格薄雪，不能用DATA改变厚度；支撑用原生CanSupportSnow |
| TallGrassBlock | plant | 0/8 | 0成熟高草，8幼草；下方需泥土/草地/沙/耕地，幼草可长大 |
| RedFlowerBlock / PurpleFlowerBlock / WhiteFlowerBlock | plant | 0/1 | 0成熟花、1幼花；土类支撑规则同上，幼花可长大 |
| DryBushBlock / LargeDryBushBlock | plant | 0 | 枯灌木；土类支撑规则同上，拒绝幼草/幼花的DATA |
| FallenLeavesBlock | groundcover | 0 | 固定1/16格落叶；需不透明支撑，受季节/天气影响 |
| CarpetBlock | carpet | 0..15 | 颜色索引；需不透明支撑，降雨可使其消失 |
| GravestoneBlock | gravestone | 0..15 | 低3位造型0..7，位3轴向0/1；每90°切换轴向，镜像保持轴向 |
| JackOLanternBlock | lantern | 0..3 | 南瓜灯四向；下方需不透明顶面或栅栏，保留原生发光 |
| CairnBlock | cairn | 0 | 石堆；本工具要求不透明地基，只开放0，不复制原生掉落变种DATA |

所有依附材料的支撑可来自蓝图，也可来自已加载世界；清障不绕过支撑要求。不透明地基不等于任意碰撞形状，沙/砂砾的活板门等特殊支撑暂不接受。不可悬空生成沙柱；Y=0按原生世界底边视为沙/砂砾隐式支撑。

**自然材料可以导入和建造，但不会被永久定格。** 本版不拦截天气、随机轮询、移动沙块、自然生长或掉落。撤销只对仍匹配已写值的格子执行，已自然变化/被玩家修改的格子保留；不保证回收移动方块、掉落物或撤回周围自然传播。`environmentMutable=true`、`undoTerrain=false`记录这一边界，并非禁用已有撤销。

[18种材料展台JSON](examples/blueprint-materials-v3.json)可直接导入；不需要新schema。该展台没有真实水、岩浆、电路、容器或实体状态。

## 家具模板（v0.19.0 / schema 3）

原生 `FurnitureBlock`（227）仍不支持写入 `cells`；不接受设计 ID 或原始家具 DATA。
`furniturePlacements` 是独立的模板调用接口，当前全部降级为本清单已支持的结构块。
不会产生家具实体、世界设计表条目或新方块族，不新增可作者化 DATA 范围。

```json
{
  "schemaVersion": 3,
  "kind": "builderassistant.blueprint",
  "name": "书屋布局",
  "cells": [{"x":0,"y":0,"z":0,"key":"PlanksBlock"}],
  "furniturePlacements": [
    {"templateId":"TallBookshelf_A","x":1,"y":1,"z":1,"rotation":0},
    {"templateId":"ReadingDesk_A","x":1,"y":1,"z":4,"rotation":0},
    {"templateId":"WoodChair_A","x":2,"y":1,"z":6,"rotation":2}
  ]
}
```

`templateId` 必须精确匹配（区分大小写）。`x/y/z` 是相对联合蓝图原点的整数，
表示旋转后包围盒最小角；`rotation` 必填且为 0/1/2/3，对应几何旋转 0/90/180/270 度，
一次旋转 `(dx,dz)→(-dz,dx)`。模板默认前向为 -Z，依次转向 +X、+Z、-X。
这不是原生方块 DATA 的方向编码。`mirrorX` 可省略，若提供必须为布尔值，在旋转后镜像 X。
`color` 可省略或为 `"natural"`；`variant` 可省略或为 `"default"`；其他值明确拒绝，
不会悄悄忽略颜色请求。家具蓝图相对坐标须在 ±1000000 内以防溢出。

| 精确模板 id | 名称 | 未旋转尺寸 X×Y×Z | 实际写入格数 |
| --- | --- | --- | --- |
| TallBookshelf_A | 高书架 | 3×4×1 | 12 |
| WideBookshelf_B | 宽书架 | 5×3×1 | 15 |
| CornerBookshelf_C | 转角书架 | 3×4×3 | 20 |
| ReadingDesk_A | 阅读桌 | 3×2×1 | 5 |
| WritingDesk_B | 写字桌 | 4×2×1 | 6 |
| WoodChair_A | 木椅 | 1×2×2 | 3 |
| Bench_A | 长椅 | 3×2×2 | 9 |
| Cabinet_A | 木柜 | 2×4×1 | 8 |
| Bed_A | 床榻 | 2×2×3 | 12 |
| LampStand_A | 灯架 | 1×4×1 | 4 |

所有模板支持四向旋转；奇数 rotation 交换 X/Z 尺寸。占地包围盒为
`[x,x+旋转尺寸X-1] × [y,y+尺寸Y-1] × [z,z+旋转尺寸Z-1]`（含端点）。
家具的整个包围盒不能与结构 cells、门隐式上半格或其他家具包围盒重叠，清障模式也不能放宽
蓝图内部重叠。世界中包围盒空隙也做施工前净空检查；开启原有覆盖/清软障规则时按该规则校验，
但空隙不产生写入，原有物体不会被清掉。执行中仍对每个实际写入格重新检查。

`cells: []` 可用于纯家具布局；结构和展开家具合计 **≤10000 格、每轴≤64**，仍每批≤32，
全局归一化后同时平移结构和家具坐标，先结构、后家具，共享一次取消、失败回滚和撤销。
JSON 导出保留模板引用；整份蓝图旋转/镜像也保留引用。
书架中的“书”用砖块表示，床不能睡觉，木柜不储物，灯架不发光，椅子不提供原生坐下功能。
这些是整格尺度的装饰组合，精细度低于原生铁锤微体素家具；**Android 7723 效果待验证**。
完整取证和后续原生后端条件见 [FURNITURE-TEMPLATES.md](FURNITURE-TEMPLATES.md)。


## JSON 格式与兼容性

继续读取 schemaVersion 1/2/3，新生成和保存统一导出 3（包括纯 cells）。`d` 是该类型/族的原生 DATA 整数，已经能够无歧义表达颜色、角形、轴向和朝向，家具模板以独立 furniturePlacements 列表使用 schemaVersion 3，精细方块仍沿用原有 DATA 编码。旧文件中的合法值含义不变；新状态文件不能由旧 Mod 完整重建，应升级 Mod，不能删掉 `d` 降级。

```json
{
  "schemaVersion": 3,
  "kind": "builderassistant.blueprint",
  "name": "painted-sample",
  "size": {
    "x": 1,
    "y": 1,
    "z": 1
  },
  "materials": [
    {
      "id": 0,
      "key": "BricksBlock",
      "block": "BricksBlock"
    }
  ],
  "cells": [
    {
      "x": 0,
      "y": 0,
      "z": 0,
      "m": 0,
      "d": 7
    }
  ]
}
```

- `cells` 最多 10000 格，任何轴最多 64 格；引擎生成的门上半格也计入事务、尺寸和批次限制。每批最多 32 格，撤销和回滚也分批。
- 坐标必须是整数、唯一；规范化后最小角为 (0,0,0)。`size` 若提供必须与计算结果一致。
- 材料可用 `materials`+`m`，也可用 cell 的 `key`。`materials[].block` 若提供，必须与材料映射一致。
- `f` 缺省为 `cube`，`d` 缺省为 0；任何非 cube 材料必须显式写对应 `f`（见机器矩阵）。非字符串 `f` 拒绝。
- 非法 DATA、无颜色标志但带颜色负载、未定义的楼梯 corner=3、原木别名 3 和未知高位全部拒绝，不做掩码截断。
- `props`、复杂实体状态、可执行 `buildOps` 继续拒绝。不得写数字方块 ID、Mod 工具 ID 或未开放类型。

## DATA 精确定义

颜色 `c` 为 0..15 的调色板索引；未染色与染色 0 是不同状态，必须保留颜色存在标志。下列公式中的“未染色”只存几何值 `g`；不要把有间隙的合法集合当连续范围。

| f | 未染色/基础 DATA | 染色 DATA | 状态含义 |
|---|---|---|---|
| cube | 0 | 仅九种 PaintedCube：`1+2*c` | 其他 cube 包括 Window/FramedGlass 只能为 0 |
| stairs | `g=0..23` | `g+32+64*c` | rot=g&3；upsideDown=(g&4)!=0；corner=(g>>3)&3，只允许 0/1/2 |
| slab | `g=0..1` | `g+2+4*c` | g=1 为上半砖 |
| fence | `g=0..15` | `g+16+32*c` | 位0=+X、1=-X、2=+Z、3=-Z；连接由引擎重算，颜色不变 |
| door | 0..15 | 不支持染色 | 低2位旋转，位2开合，位3铰链；JSON 只放下半格 |
| log | 0/1/2 | 不支持染色 | 0=Y轴竖直，1=Z轴，2=X轴；3虽可被 getter 别名解读，但 setter 不生成，拒绝 |
| ladder | 0..3 | 不支持染色 | 安装面编号，背面需不透明支撑面 |
| torch | 0..4 | 不支持染色 | 0..3 为侧面，4 为地面；5 天花板不允许 |

九种 PaintedCube：`CobblestoneBlock`、`StoneBrickBlock`、`PlanksBlock`、`BricksBlock`、`BasaltBlock`、`GraniteBlock`、`MarbleBlock`、`SandstoneBlock`、`ClayBlock`。例如 Bricks DATA=7 是染色索引3；WoodenStairs DATA=995 是旋转3、普通直楼梯、颜色15；WoodenSlab DATA=63 是上半砖、颜色15。

坐标旋转 `(dx,dz)->(-dz,dx)`；旋转编号每次减1 mod4。镜像反转 X；普通朝向编号 `(-r)&3`；楼梯角形镜像为 `(1-r)&3`；门镜像还切换位3。横向原木每次90度互换1/2，X镜像不变。所有颜色位保留。

## 支持的具体类型（由当前机器矩阵列出）

### cube

`CobblestoneBlock`, `StoneBrickBlock`, `PlanksBlock`, `BricksBlock`, `GlassBlock`, `BasaltBlock`, `GraniteBlock`, `MarbleBlock`, `SandstoneBlock`, `ClayBlock`, `LimestoneBlock`, `MalachiteBlock`, `CoalBlock`, `CoalOreBlock`, `CopperBlock`, `CopperOreBlock`, `DiamondBlock`, `DiamondOreBlock`, `GermaniumOreBlock`, `IronBlock`, `IronOreBlock`, `SaltpeterOreBlock`, `SemiconductorBlock`, `SulphurOreBlock`, `FramedGlassBlock`, `WindowBlock`, `WickerLampBlock`, `SpruceLeavesBlock`, `TallSpruceLeavesBlock`, `DirtBlock`, `IceBlock`

### stairs

`WoodenStairsBlock`, `CobblestoneStairsBlock`, `StoneStairsBlock`, `StoneBrickStairsBlock`, `BrickStairsBlock`, `BasaltStairsBlock`, `MarbleStairsBlock`, `SandstoneStairsBlock`, `DirtStairsBlock`

### slab

`WoodenSlabBlock`, `CobblestoneSlabBlock`, `StoneSlabBlock`, `StoneBrickSlabBlock`, `BrickSlabBlock`, `BasaltSlabBlock`, `MarbleSlabBlock`, `SandstoneSlabBlock`, `DirtSlabBlock`

### fence

`WoodenFenceBlock`, `StoneFenceBlock`, `BrickFenceBlock`, `BasaltFenceBlock`, `IronFenceBlock`

### door

`WoodenDoorBlock`, `IronDoorBlock`, `CellDoorBlock`

### log

`OakWoodBlock`, `BirchWoodBlock`, `SpruceWoodBlock`, `MimosaWoodBlock`, `PoplarWoodBlock`

### ladder

`WoodenLadderBlock`, `IronLadderBlock`

### torch

`TorchBlock`

### falling

`SandBlock`, `GravelBlock`

### grass

`GrassBlock`

### soil

`SoilBlock`

### snow

`SnowBlock`

### plant

`TallGrassBlock`, `RedFlowerBlock`, `PurpleFlowerBlock`, `WhiteFlowerBlock`, `DryBushBlock`, `LargeDryBushBlock`

### groundcover

`FallenLeavesBlock`

### carpet

`CarpetBlock`

### gravestone

`GravestoneBlock`

### lantern

`JackOLanternBlock`

### cairn

`CairnBlock`

## 逻辑材料键

| key | 真实类型 |
|---|---|
| `StoneBase` | `CobblestoneBlock` |
| `StoneWall` | `StoneBrickBlock` |
| `WoodWall` | `PlanksBlock` |
| `WoodTrim` | `PlanksBlock` |
| `Roof` | `BricksBlock` |
| `Glass` | `GlassBlock` |
| `Floor` | `PlanksBlock` |
| `RoofStairs` | `WoodenStairsBlock` |
| `InteriorStairs` | `WoodenStairsBlock` |
| `EntryDoor` | `WoodenDoorBlock` |
| `WindowSill` | `StoneSlabBlock` |
| `FlatDeck` | `StoneSlabBlock` |
| `GardenFence` | `WoodenFenceBlock` |
| `StoneRailing` | `StoneFenceBlock` |
| `InteriorWall` | `PlanksBlock` |
| `SupportBeam` | `PlanksBlock` |
| `LogTrunk` | `OakWoodBlock` |
| `LogBranch` | `OakWoodBlock` |
| `Leaves` | `SpruceLeavesBlock` |

## 支撑、施工与撤销

梯子/火把沿用既有安装面规则；新地面依附材料使用正下方一格。BuildPlanner按依赖先放支撑，BuildTask在写前和完工阶段分批复核；撤销按逆依赖移除，恢复失败保留队列。门关联格限制、10000格事务/64格单轴/32格批次、区块等待、主机权限和保护后续修改不变。

`preserveData`表示JSON/复制/旋转编解码无损，不保证引擎永不重算状态。原生楼梯/栅栏连接、草地积雪标记、幼苗生长、天气和耕地变化仍由游戏管理。没有全子系统撤销承诺。

## 逐类型能力矩阵

放置、复制、旋转、镜像和保存均为规则支持；手机实际效果待验证。完整allowedData数组、requiresSupport和environmentMutable由代码导出。

| 类型 | f | 支撑 | 自然变化 | terrain完整恢复保证 | 全子系统恢复保证 |
|---|---|:---:|:---:|:---:|:---:|
| `CobblestoneBlock` | cube | 否 | 否 | 是 | 是 |
| `StoneBrickBlock` | cube | 否 | 否 | 是 | 是 |
| `PlanksBlock` | cube | 否 | 否 | 是 | 是 |
| `BricksBlock` | cube | 否 | 否 | 是 | 是 |
| `GlassBlock` | cube | 否 | 否 | 是 | 是 |
| `BasaltBlock` | cube | 否 | 否 | 是 | 是 |
| `GraniteBlock` | cube | 否 | 否 | 是 | 是 |
| `MarbleBlock` | cube | 否 | 否 | 是 | 是 |
| `SandstoneBlock` | cube | 否 | 否 | 是 | 是 |
| `ClayBlock` | cube | 否 | 否 | 是 | 是 |
| `LimestoneBlock` | cube | 否 | 否 | 是 | 是 |
| `MalachiteBlock` | cube | 否 | 否 | 是 | 是 |
| `CoalBlock` | cube | 否 | 否 | 是 | 是 |
| `CoalOreBlock` | cube | 否 | 否 | 是 | 是 |
| `CopperBlock` | cube | 否 | 否 | 是 | 是 |
| `CopperOreBlock` | cube | 否 | 否 | 是 | 是 |
| `DiamondBlock` | cube | 否 | 否 | 是 | 是 |
| `DiamondOreBlock` | cube | 否 | 否 | 是 | 是 |
| `GermaniumOreBlock` | cube | 否 | 否 | 是 | 是 |
| `IronBlock` | cube | 否 | 否 | 是 | 是 |
| `IronOreBlock` | cube | 否 | 否 | 是 | 是 |
| `SaltpeterOreBlock` | cube | 否 | 否 | 是 | 是 |
| `SemiconductorBlock` | cube | 否 | 否 | 是 | 是 |
| `SulphurOreBlock` | cube | 否 | 否 | 是 | 是 |
| `FramedGlassBlock` | cube | 否 | 否 | 是 | 是 |
| `WindowBlock` | cube | 否 | 否 | 是 | 是 |
| `WickerLampBlock` | cube | 否 | 否 | 是 | 否 |
| `SpruceLeavesBlock` | cube | 否 | 否 | 是 | 否 |
| `TallSpruceLeavesBlock` | cube | 否 | 否 | 是 | 否 |
| `WoodenStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `CobblestoneStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `StoneStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `StoneBrickStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `BrickStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `BasaltStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `MarbleStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `SandstoneStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `DirtStairsBlock` | stairs | 否 | 否 | 否 | 否 |
| `WoodenSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `CobblestoneSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `StoneSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `StoneBrickSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `BrickSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `BasaltSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `MarbleSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `SandstoneSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `DirtSlabBlock` | slab | 否 | 否 | 是 | 否 |
| `WoodenFenceBlock` | fence | 否 | 否 | 否 | 否 |
| `StoneFenceBlock` | fence | 否 | 否 | 否 | 否 |
| `BrickFenceBlock` | fence | 否 | 否 | 否 | 否 |
| `BasaltFenceBlock` | fence | 否 | 否 | 否 | 否 |
| `IronFenceBlock` | fence | 否 | 否 | 否 | 否 |
| `WoodenDoorBlock` | door | 否 | 否 | 否 | 否 |
| `IronDoorBlock` | door | 否 | 否 | 否 | 否 |
| `CellDoorBlock` | door | 否 | 否 | 否 | 否 |
| `OakWoodBlock` | log | 否 | 否 | 是 | 否 |
| `BirchWoodBlock` | log | 否 | 否 | 是 | 否 |
| `SpruceWoodBlock` | log | 否 | 否 | 是 | 否 |
| `MimosaWoodBlock` | log | 否 | 否 | 是 | 否 |
| `PoplarWoodBlock` | log | 否 | 否 | 是 | 否 |
| `WoodenLadderBlock` | ladder | 是 | 否 | 是 | 否 |
| `IronLadderBlock` | ladder | 是 | 否 | 是 | 否 |
| `TorchBlock` | torch | 是 | 否 | 是 | 否 |
| `DirtBlock` | cube | 否 | 是 | 否 | 否 |
| `IceBlock` | cube | 否 | 是 | 否 | 否 |
| `SandBlock` | falling | 是 | 是 | 否 | 否 |
| `GravelBlock` | falling | 是 | 是 | 否 | 否 |
| `GrassBlock` | grass | 否 | 是 | 否 | 否 |
| `SoilBlock` | soil | 否 | 是 | 否 | 否 |
| `SnowBlock` | snow | 是 | 是 | 否 | 否 |
| `TallGrassBlock` | plant | 是 | 是 | 否 | 否 |
| `RedFlowerBlock` | plant | 是 | 是 | 否 | 否 |
| `PurpleFlowerBlock` | plant | 是 | 是 | 否 | 否 |
| `WhiteFlowerBlock` | plant | 是 | 是 | 否 | 否 |
| `DryBushBlock` | plant | 是 | 是 | 否 | 否 |
| `LargeDryBushBlock` | plant | 是 | 是 | 否 | 否 |
| `FallenLeavesBlock` | groundcover | 是 | 是 | 否 | 否 |
| `CarpetBlock` | carpet | 是 | 是 | 否 | 否 |
| `GravestoneBlock` | gravestone | 是 | 否 | 是 | 否 |
| `JackOLanternBlock` | lantern | 是 | 否 | 是 | 否 |
| `CairnBlock` | cairn | 是 | 否 | 是 | 否 |

## 尚未开放

- 容器/库存/文本/方块实体：箱子、熔炉、发射器、告示牌等。
- 原生FurnitureBlock设计注册与设计表、复杂交互家具。
- 流体传播、火、爆炸物、活塞、电路与电控门类（活板门、栅栏门等）。
- 树苗长成整树、农作物跨格改土、季节落叶树冠和藤蔓延迟扩展等尚未逐类接入的生长方块。
- 未知自定义Mod方块。未进入本清单的类型仍拒绝，不能把“除超复杂都想扩展”理解为直接放开全部原版ID。

详细取证、验证和设备清单见[v0.24报告](BUILDER-MATERIALS-0.24.0.md)与[165类审计](blueprint-block-audit-x25.01.17.md)。批量生成前以当前机器矩阵为准。
