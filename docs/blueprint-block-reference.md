# Builder Assistant 蓝图方块能力清单 v0.21.1

> 2026-10-29：SCIE-Port 0.54.0 调整阿比斯图像技能 UI、挥镰动作及范围；Builder Assistant 的方块、族标签、DATA 和蓝图能力不变。

> 2026-10-29：SCIE-Port 0.53.0 调整虚影瞬狩视听，不改变 Builder Assistant 方块、DATA、族标签或蓝图限制。

> 2026-10-29：SCIE-Port 0.52.0 新增阿比斯音效，不改变 Builder Assistant 方块、DATA、族标签或蓝图限制。

> 2026-10-29：SCIE-Port 0.51.0 调整阿比斯视觉预览；Builder Assistant 方块、DATA、族标签及蓝图限制均未变。

> 2026-10-29 主线合并审计：SCIE-Port 0.47.0 的阿比斯（952）与吞噬之眼（950）是独立 Mod 神器，
> v0.21.0 蓝图管理及快捷放置未改变方块、DATA、家具或 schema 的支持范围；v0.21.1 已移除清障目标类型白名单和邻近方块检查；蓝图自身支持范围不变。

> v0.20.0 工作台界面重构未改变方块、DATA、家具模板或 JSON schema 支持范围。

> 不属于 Builder Assistant 可建造白名单，也不得生成到蓝图 cells。Builder Assistant 保持 0.19.0，
> 本页类型、族标签、DATA 范围、家具与安全限制均沿用已验证主线，无新增支持。

> 2026-10-28 展示规范补充：SCIE-Port 0.44.0 的独立 Tab、神器/圣兽背景和中文包名
> 仅改变移植工程展示，不增加 Builder Assistant 的方块、族标签或 DATA 能力。
> 不得将 `SCIEPort.Artifacts` / `SCIEPort.Vehicles` 等分类键当作蓝图材料键使用。

> **SCIE-Port 0.43.0 审计**：四圣兽V3仅新增衣物数据200–203的立体附件、装备图标和美术资源，未新增、移除或修改Builder Assistant支持的方块、族标签、DATA范围或导入限制。本清单能力集合保持不变，四圣兽装备模型不能作为蓝图建筑材料。

> **SCIE-Port 0.40.5 审计**：本次只调整衣物块 203 的四圣兽衣物穿戴 Layer、昆古尼尔 HUD 与瞄准准心；没有新增、移除或修改 Builder Assistant 支持的方块、族标签、DATA 范围或导入限制。本清单能力集合保持不变。

> 2026-10-28 边界补充：SCIE-Port 0.43.0 新增的海神三叉戟·波塞冬（951、DATA 0、
> 非地形物品）不属于本清单材料；不允许在 Builder Assistant `cells`/家具模板中填写
> `PoseidonBlock`、951 或 `scieport-poseidon`，不增加蓝图族标签或 DATA 支持范围。

> 适用于 Survivalcraft NET x25.01.17 的 Builder Assistant。原 60 种保留，新增两种梯子和火把，共 63 种。Windows 固定哈希程序集/API 与离线规则已确认；Android 7723 真机行为均待验证。
> 本清单和 [逐类型能力矩阵](blueprint-block-capabilities.json) 是 AI 生成蓝图的能力依据。SCIE-Port 的代码、方块、载具、神器及家具不是本工程材料；不得据其他工程的版本推断本工程能力。

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

继续读取 schemaVersion 1/2/3，纯 cells 默认导出 2，含家具模板导出 3。`d` 是该类型/族的原生 DATA 整数，已经能够无歧义表达颜色、角形、轴向和朝向，家具模板以独立 furniturePlacements 列表使用 schemaVersion 3，精细方块仍沿用原有 DATA 编码。旧文件中的合法值含义不变；新状态文件不能由旧 Mod 完整重建，应升级 Mod，不能删掉 `d` 降级。

```json
{
  "schemaVersion": 2,
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
- `f` 缺省为 `cube`，`d` 缺省为 0；楼梯、半砖、栅栏、门、原木、梯子和火把必须显式写相应 `f`。非字符串 `f` 拒绝。
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

## 支持的具体类型

### cube

`CobblestoneBlock`, `StoneBrickBlock`, `PlanksBlock`, `BricksBlock`, `GlassBlock`, `BasaltBlock`, `GraniteBlock`, `MarbleBlock`, `SandstoneBlock`, `ClayBlock`, `LimestoneBlock`, `MalachiteBlock`, `CoalBlock`, `CoalOreBlock`, `CopperBlock`, `CopperOreBlock`, `DiamondBlock`, `DiamondOreBlock`, `GermaniumOreBlock`, `IronBlock`, `IronOreBlock`, `SaltpeterOreBlock`, `SemiconductorBlock`, `SulphurOreBlock`, `FramedGlassBlock`, `WindowBlock`, `WickerLampBlock`, `SpruceLeavesBlock`, `TallSpruceLeavesBlock`

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

## 支撑、施工与撤销边界

目标程序集 `CellFace` 向量：0=(0,0,+1)、1=(+1,0,0)、2=(0,0,-1)、3=(-1,0,0)、4=(0,+1,0)。梯子/火把的支撑坐标为本格减此向量。梯子使用目标 `IsFaceTransparent` 判断接触面；火把检查支撑 `IsCollidable`。支撑可以来自蓝图，也可以来自已加载世界；计划先验证，再按依赖排序，执行前再验证，完成后跨一次更新按批复核。

撤销按依赖逆序进行，失败时停止并保留剩余队列。门上半格在修改前抓取原值；门目前只接受空气下半落点和空闲上半格，清障模式也不能绕过。已经存在的门不能通过覆盖模式替换。光照变化不被误判为玩家编辑；真正的后续方块/DATA 修改保留。

**能力矩阵中的 `preserveData` 是 JSON/codec 无损，不保证引擎永不重算形状。** 栅栏连接和直楼梯邻接角形可被原生事件更新；当前没有覆盖所有异步邻格、电路、拾取物和随机 tick 的事务日志。楼梯/栅栏/门的 `undoTerrain=false` 表示一般环境下不能保证完整 terrain 恢复，并非完全禁用已有撤销。两种梯子和火把有可逆的受控 fixture，但支撑被外部拆除产生的掉落物不属于地形快照。不得宣传全世界/全子系统完整撤销。

### 逐类型能力矩阵

下表由当前目录导出。放/复制/旋转/镜像/保存 DATA 为该版本规则支持；Android 均待验证。“terrain撤销”只针对无外部并发编辑的受控放置，“完整撤销”涵盖原生行为涉及的所有子系统。

| 类型 | f | 放 | 复制 | 旋转 | 镜像 | DATA | terrain撤销 | 完整撤销 |
|---|---|---|---|---|---|---|---|---|
| `CobblestoneBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `StoneBrickBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `PlanksBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `BricksBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `GlassBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `BasaltBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `GraniteBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `MarbleBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `SandstoneBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `ClayBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `LimestoneBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `MalachiteBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `CoalBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `CoalOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `CopperBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `CopperOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `DiamondBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `DiamondOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `GermaniumOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `IronBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `IronOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `SaltpeterOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `SemiconductorBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `SulphurOreBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `FramedGlassBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `WindowBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 是 |
| `WickerLampBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `SpruceLeavesBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `TallSpruceLeavesBlock` | cube | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `WoodenStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `CobblestoneStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `StoneStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `StoneBrickStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `BrickStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `BasaltStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `MarbleStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `SandstoneStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `DirtStairsBlock` | stairs | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `WoodenSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `CobblestoneSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `StoneSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `StoneBrickSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `BrickSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `BasaltSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `MarbleSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `SandstoneSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `DirtSlabBlock` | slab | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `WoodenFenceBlock` | fence | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `StoneFenceBlock` | fence | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `BrickFenceBlock` | fence | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `BasaltFenceBlock` | fence | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `IronFenceBlock` | fence | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `WoodenDoorBlock` | door | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `IronDoorBlock` | door | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `CellDoorBlock` | door | 是 | 是 | 是 | 是 | 是 | 不支持 | 不支持 |
| `OakWoodBlock` | log | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `BirchWoodBlock` | log | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `SpruceWoodBlock` | log | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `MimosaWoodBlock` | log | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `PoplarWoodBlock` | log | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `WoodenLadderBlock` | ladder | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `IronLadderBlock` | ladder | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |
| `TorchBlock` | torch | 是 | 是 | 是 | 是 | 是 | 是 | 不支持 |

机器矩阵包含逐类型 **精确 allowedData 数组**。更新代码后重新生成：`dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --capabilities docs/blueprint-block-capabilities.json`。

## 仍不支持的细节与复杂方块

| 类型 | 已取证 DATA | 拒绝原因 |
|---|---|---|
| WoodenTrapdoorBlock / CellTrapdoorBlock | 0..15：旋转/开合/上下翻转 | 实现 IElectricElementBlock；支撑有侧面、上下与斜邻 OR 条件，尚无电路节点/更新队列事务 |
| WoodenFenceGateBlock / IronFenceGateBlock | 几何0..15，可加16+32*c；handed位为反逻辑 | 原生电路元素和延迟开合没有完整快照恢复 |
| CarpetBlock | 0..15颜色 | 下方必须非透明；降雨随机轮询会删除，天气/掉落事务未证明 |
| IvyBlock | 0..3面 | 支撑方向与梯子相反，可挂同面上方藤蔓；60秒延迟生长队列及级联删除未事务化 |

上述门类完成研究用变换 codec 和离线方向还原测试，但不注册材料、不接受导入族标签。容器/文本/家具、电路、流体、重力、其他生长/季节方块及未知自定义方块继续不支持。

## Android 验证与下一阶段

Android 7723 待验证：渠道程序集哈希、所有16色及角形/轴向渲染、梯子攀爬、火把粒子与光照、支撑移除/重载、取消/分批撤销、不同触屏分辨率、4419格建造帧耗时、单机/房主/普通客户端。普通客户端仍拒绝主动写入；主机同步尚未实测。

家具与复杂块的前置条件：取得目标 Furniture/BlockEntities 的索引映射和持久 payload ABI，限制并观察完整影响范围，在修改前快照实体、库存、文本与电路队列，支持失败/取消/卸载后的恢复和冲突检测，再通过无玩家主存档的单人和联机实测。原生家具 payload 后端仍未实现；v0.19.0 的 schema3 仅执行结构块家具模板，不写入原生设计表。

## 机器可读摘要

```json
{
  "modVersion": "0.21.1",
  "furnitureTemplates": {
    "backend": "structure-blocks-v1",
    "rotationSteps": [0, 1, 2, 3],
    "anchor": "rotated bounding-box minimum corner",
    "color": ["natural"],
    "variant": ["default"],
    "nativeFurnitureBlock": "not supported",
    "templates": [
      {"id":"TallBookshelf_A","size":[3,4,1],"cells":12},
      {"id":"WideBookshelf_B","size":[5,3,1],"cells":15},
      {"id":"CornerBookshelf_C","size":[3,4,3],"cells":20},
      {"id":"ReadingDesk_A","size":[3,2,1],"cells":5},
      {"id":"WritingDesk_B","size":[4,2,1],"cells":6},
      {"id":"WoodChair_A","size":[1,2,2],"cells":3},
      {"id":"Bench_A","size":[3,2,2],"cells":9},
      {"id":"Cabinet_A","size":[2,4,1],"cells":8},
      {"id":"Bed_A","size":[2,2,3],"cells":12},
      {"id":"LampStand_A","size":[1,4,1],"cells":4}
    ]
  },

  "targetGame": "Survivalcraft NET x25.01.17",
  "schemaVersion": 3,
  "limits": {
    "maxCells": 10000,
    "maxAxis": 64,
    "maxPerBatch": 32,
    "maxCellsIncludesGeneratedCells": true
  },
  "families": {
    "cube": {
      "defaultData": 0,
      "paintedCubeData": "0 or 1+2*color",
      "colorRange": [
        0,
        15
      ],
      "paintOnlyOnTypes": [
        "CobblestoneBlock",
        "StoneBrickBlock",
        "PlanksBlock",
        "BricksBlock",
        "BasaltBlock",
        "GraniteBlock",
        "MarbleBlock",
        "SandstoneBlock",
        "ClayBlock"
      ]
    },
    "stairs": {
      "geometryRange": [
        0,
        23
      ],
      "paintedData": "geometry+32+64*color",
      "colorRange": [
        0,
        15
      ],
      "invalidCorner": 3
    },
    "slab": {
      "geometryRange": [
        0,
        1
      ],
      "paintedData": "geometry+2+4*color",
      "colorRange": [
        0,
        15
      ]
    },
    "fence": {
      "geometryRange": [
        0,
        15
      ],
      "paintedData": "geometry+16+32*color",
      "colorRange": [
        0,
        15
      ],
      "geometry": "engine-derived connections"
    },
    "door": {
      "dataRange": [
        0,
        15
      ],
      "note": "bottom only; reserve air upper; native electricity not fully undoable"
    },
    "log": {
      "dataValues": [
        0,
        1,
        2
      ],
      "axis": {
        "0": "Y",
        "1": "Z",
        "2": "X"
      },
      "invalidAlias": 3
    },
    "ladder": {
      "dataRange": [
        0,
        3
      ],
      "support": "position-CellFaceVector(data); support face must be opaque"
    },
    "torch": {
      "dataRange": [
        0,
        4
      ],
      "support": "position-CellFaceVector(data); support must be collidable"
    }
  },
  "supportedBlocks": {
    "cube": [
      "CobblestoneBlock",
      "StoneBrickBlock",
      "PlanksBlock",
      "BricksBlock",
      "GlassBlock",
      "BasaltBlock",
      "GraniteBlock",
      "MarbleBlock",
      "SandstoneBlock",
      "ClayBlock",
      "LimestoneBlock",
      "MalachiteBlock",
      "CoalBlock",
      "CoalOreBlock",
      "CopperBlock",
      "CopperOreBlock",
      "DiamondBlock",
      "DiamondOreBlock",
      "GermaniumOreBlock",
      "IronBlock",
      "IronOreBlock",
      "SaltpeterOreBlock",
      "SemiconductorBlock",
      "SulphurOreBlock",
      "FramedGlassBlock",
      "WindowBlock",
      "WickerLampBlock",
      "SpruceLeavesBlock",
      "TallSpruceLeavesBlock"
    ],
    "stairs": [
      "WoodenStairsBlock",
      "CobblestoneStairsBlock",
      "StoneStairsBlock",
      "StoneBrickStairsBlock",
      "BrickStairsBlock",
      "BasaltStairsBlock",
      "MarbleStairsBlock",
      "SandstoneStairsBlock",
      "DirtStairsBlock"
    ],
    "slab": [
      "WoodenSlabBlock",
      "CobblestoneSlabBlock",
      "StoneSlabBlock",
      "StoneBrickSlabBlock",
      "BrickSlabBlock",
      "BasaltSlabBlock",
      "MarbleSlabBlock",
      "SandstoneSlabBlock",
      "DirtSlabBlock"
    ],
    "fence": [
      "WoodenFenceBlock",
      "StoneFenceBlock",
      "BrickFenceBlock",
      "BasaltFenceBlock",
      "IronFenceBlock"
    ],
    "door": [
      "WoodenDoorBlock",
      "IronDoorBlock",
      "CellDoorBlock"
    ],
    "log": [
      "OakWoodBlock",
      "BirchWoodBlock",
      "SpruceWoodBlock",
      "MimosaWoodBlock",
      "PoplarWoodBlock"
    ],
    "ladder": [
      "WoodenLadderBlock",
      "IronLadderBlock"
    ],
    "torch": [
      "TorchBlock"
    ]
  },
  "logicalKeys": {
    "StoneBase": "CobblestoneBlock",
    "StoneWall": "StoneBrickBlock",
    "WoodWall": "PlanksBlock",
    "WoodTrim": "PlanksBlock",
    "Roof": "BricksBlock",
    "Glass": "GlassBlock",
    "Floor": "PlanksBlock",
    "RoofStairs": "WoodenStairsBlock",
    "InteriorStairs": "WoodenStairsBlock",
    "EntryDoor": "WoodenDoorBlock",
    "WindowSill": "StoneSlabBlock",
    "FlatDeck": "StoneSlabBlock",
    "GardenFence": "WoodenFenceBlock",
    "StoneRailing": "StoneFenceBlock",
    "InteriorWall": "PlanksBlock",
    "SupportBeam": "PlanksBlock",
    "LogTrunk": "OakWoodBlock",
    "LogBranch": "OakWoodBlock",
    "Leaves": "SpruceLeavesBlock"
  },
  "notSupported": [
    "WoodenTrapdoorBlock",
    "CellTrapdoorBlock",
    "WoodenFenceGateBlock",
    "IronFenceGateBlock",
    "CarpetBlock",
    "IvyBlock",
    "fluids",
    "plants except SpruceLeavesBlock/TallSpruceLeavesBlock",
    "snow",
    "ice",
    "gravity blocks",
    "entity-bearing blocks",
    "electric circuits",
    "saplings",
    "furniture blocks",
    "unknown custom blocks"
  ],
  "buildBehavior": {
    "defaultOverride": false,
    "clearObstacles": "explicit opt-in; native linked/dynamic state is not fully undoable; doors require air even with override",
    "attachmentOrder": "topological support-first; reverse for undo",
    "generatedDoorTop": "reserved and snapshotted before write; included in 10000/64/32 limits",
    "verification": "next update; at most 32 inspected cells per call",
    "restoration": "at most 32 cells per call; stop on failure and retry; do not overwrite later edits",
    "fullUndo": "see per-type matrix; terrain snapshots do not imply all-subsystem undo"
  },
  "capabilityMatrix": "blueprint-block-capabilities.json",
  "android7723": "待验证"
}
```

最后更新：2026-10-28，v0.19.0。详细取证、命令、测试及尚存限制见 [开发报告](BUILDER-DETAIL-STATES-0.18.0.md) 和 [165类方块审计](blueprint-block-audit-x25.01.17.md)。

> 2026-10-28 SCIE-Port 0.44.1合并审计：仅整合四圣兽V3显示与0.44.0主线；Builder Assistant方块、族标签、DATA与家具模板能力均沿用0.19.0，无新增支持项。


四圣兽 0.48.0 仅调整 SCIE-Port 衣物属性，不新增或移除 Builder Assistant 蓝图方块、DATA 范围或能力。

阿比斯 0.49.0 主线整合仅涉及 SCIE-Port 神器，不新增或变更 Builder Assistant 蓝图方块、DATA 范围或能力。

阿比斯 0.50.0 星门区块加载不改变 Builder Assistant 蓝图方块、DATA 范围或能力。
