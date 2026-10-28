# Builder Assistant 蓝图方块能力清单 v0.18.0

> 适用于 Survivalcraft NET x25.01.17 的 Builder Assistant。原 60 种保留，新增两种梯子和火把，共 63 种。Windows 固定哈希程序集/API 与离线规则已确认；Android 7723 真机行为均待验证。
> 本清单和 [逐类型能力矩阵](blueprint-block-capabilities.json) 是 AI 生成蓝图的能力依据。SCIE-Port 的代码、方块、载具、神器及家具不是本工程材料；不得据其他工程的版本推断本工程能力。

## JSON 格式与兼容性

继续读取 schemaVersion 1/2，默认导出 2。`d` 是该类型/族的原生 DATA 整数，已经能够无歧义表达颜色、角形、轴向和朝向，因此本轮不引入 schemaVersion 3。旧文件中的合法值含义不变；新状态文件不能由旧 Mod 完整重建，应升级 Mod，不能删掉 `d` 降级。

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

家具与复杂块的前置条件：取得目标 Furniture/BlockEntities 的索引映射和持久 payload ABI，限制并观察完整影响范围，在修改前快照实体、库存、文本与电路队列，支持失败/取消/卸载后的恢复和冲突检测，再通过无玩家主存档的单人和联机实测。schema3 仅在 DATA 无法表达这些 payload 时设计。

## 机器可读摘要

```json
{
  "modVersion": "0.18.0",
  "targetGame": "Survivalcraft NET x25.01.17",
  "schemaVersion": 2,
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

最后更新：2026-10-28，v0.18.0。详细取证、命令、测试及尚存限制见 [开发报告](BUILDER-DETAIL-STATES-0.18.0.md) 和 [165类方块审计](blueprint-block-audit-x25.01.17.md)。
