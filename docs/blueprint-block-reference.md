# Blueprint Block Reference

> 本文件是 Builder Assistant v0.16.0 蓝图导入的**唯一权威方块能力清单**。
> 任何 AI 或工具在生成 Survivalcraft NET x25.01.17 蓝图 JSON 前必须参考此文件。
> 每次 Mod 版本更新后必须同步更新本文件并提交到 git。
> 不在此文件中的方块一律**不支持**，导入会触发"未知材料键"干净拒绝。

> SCIE-Port 0.26.0 仅升级载具部署物品 `HelicopterBlock`（ID 941）的模型、动画、贴图与音频表现；它仍不是建筑材料，**不属于 Builder Assistant 蓝图能力**。本清单未加入该键，蓝图导入器应继续以未知材料拒绝。此次 SCIE-Port 更新没有新增、移除或改变任何 Builder Assistant 可导入方块族、DATA 范围或限制。

## 蓝图 JSON Schema

```json
{
  "schemaVersion": 2,
  "kind": "builderassistant.blueprint",
  "name": "建筑名称",
  "size": { "x": 10, "y": 8, "z": 12 },
  "materials": [
    { "id": 0, "key": "PlanksBlock", "block": "PlanksBlock" }
  ],
  "cells": [
    { "x": 0, "y": 0, "z": 0, "m": 0, "f": "cube", "d": 0 }
  ]
}
```

- `schemaVersion`：必须 ≤ 2（v1 无 `f`/`d` 字段时视为纯 cube）。
- `kind`：必须为 `"builderassistant.blueprint"` 或省略。
- `size`：advisory，导入后按 cells 包围盒重新计算；若声明与实际不一致则拒绝。
- `cells`：数组长度 ≤ **10000**（每事务硬上限，2026-10-20 由 2756 上调至 10000，历史 1024→2756→10000），坐标为相对值（最小角归一化到 0）。
- 每轴尺寸 ≤ **64**。
- 每格 `m` 为 `materials` 数组中的 `id` 索引。
- `f` 可选：方块族标签（见下表），缺省为 `"cube"`。
- `d` 可选：族内的 authored DATA 值（见合法范围），缺省为 `0`。
- `f` 必须与材料键在能力注册表中的真实族一致；`materials[].block` 若非空也必须与 `key` 的实际映射一致。任何冲突整份拒绝。
- DATA 采用严格校验，不做掩码降级：彩色方块、彩色楼梯/半砖/栅栏、横向原木或未知高位不会被静默变成默认状态。

## 支持的方块族与 DATA 合法范围

| 族标签 `f` | BlockFamily | 合法 `d` | DATA 位含义 | 备注 |
|---|---|---|---|---|
| `cube` | Cube | 仅 `0` | 无 | 普通实心/透明立方；PaintedCube 仅开放未染色 data 0。 |
| `stairs` | Stairs | `0`–`7` | [0-1]=rotation(0-3), [2]=upsideDown(0-1) | corner bits 3-4 可从世界复制时剥离；颜色位不支持并拒绝。 |
| `slab` | Slab | `0`–`1` | [0]=isTop(0=下半,1=上半) | 颜色位不支持并拒绝。 |
| `fence` | Fence | `0`–`15` | [0]=+X, [1]=-X, [2]=+Z, [3]=-Z 连接臂 | 颜色位不支持并拒绝；连接臂由相邻格行为复核。 |
| `door` | Door | `0`–`15` | [0-1]=rotation(0-3), [2]=open, [3]=rightHanded | **只写下半格**，引擎 SubsystemDoorBlockBehavior 自动补上半。 |
| `log` | Log | 仅 `0` | CutFace=Y（竖向原木） | 横向原木（face 1/2）**不支持**，未取证轴映射。 |

## 可导入的方块：具体原版类型名

以下 CLR 类型名均可直接作为 `key`（或 `materials[].block`）使用：

### Cube（无朝向，data 必须为 0）

| 类型名 | 用途说明 |
|---|---|
| `CobblestoneBlock` | 鹅卵石 (PaintedCube) |
| `StoneBrickBlock` | 石砖 (PaintedCube) |
| `PlanksBlock` | 木板 |
| `BricksBlock` | 砖块 (PaintedCube) |
| `GlassBlock` | 玻璃 (AlphaTestCube) |
| `BasaltBlock` | 玄武岩 |
| `GraniteBlock` | 花岗岩 |
| `MarbleBlock` | 大理石 |
| `SandstoneBlock` | 砂岩 |
| `ClayBlock` | 粘土 |
| `LimestoneBlock` | 石灰岩 |
| `MalachiteBlock` | 孔雀石 |
| `CoalBlock` | 煤块 |
| `CoalOreBlock` | 煤矿石 |
| `CopperBlock` | 铜块 |
| `CopperOreBlock` | 铜矿石 |
| `DiamondBlock` | 钻石块 |
| `DiamondOreBlock` | 钻石矿石 |
| `GermaniumOreBlock` | 锗矿石 |
| `IronBlock` | 铁块 |
| `IronOreBlock` | 铁矿石 |
| `SaltpeterOreBlock` | 硝石矿石 |
| `SemiconductorBlock` | 半导体块 |
| `SulphurOreBlock` | 硫矿石 |
| `FramedGlassBlock` | 镶框玻璃（AlphaTestCube） |
| `WindowBlock` | 原版窗块（AlphaTestCube；不是猜测的 GlassPane 类型） |
| `WickerLampBlock` | 藤编灯（静态 data 0，默认发光属性来自 BlocksData） |
| `SpruceLeavesBlock` | 常青云杉叶（AlphaTestCube，无季节 DATA；仍受叶片腐坏规则约束） |
| `TallSpruceLeavesBlock` | 高云杉叶（EvergreenLeavesBlock，无季节 DATA；仍受腐坏规则约束） |

### Stairs（data 0–7）

| 类型名 |
|---|
| `WoodenStairsBlock` |
| `CobblestoneStairsBlock` |
| `StoneStairsBlock` |
| `StoneBrickStairsBlock` |
| `BrickStairsBlock` |
| `BasaltStairsBlock` |
| `MarbleStairsBlock` |
| `SandstoneStairsBlock` |
| `DirtStairsBlock` |

### Slab（data 0–1）

| 类型名 |
|---|
| `WoodenSlabBlock` |
| `CobblestoneSlabBlock` |
| `StoneSlabBlock` |
| `StoneBrickSlabBlock` |
| `BrickSlabBlock` |
| `BasaltSlabBlock` |
| `MarbleSlabBlock` |
| `SandstoneSlabBlock` |
| `DirtSlabBlock` |

### Fence（data 0–15）

| 类型名 |
|---|
| `WoodenFenceBlock` |
| `StoneFenceBlock` |
| `BrickFenceBlock` |
| `BasaltFenceBlock` |
| `IronFenceBlock` |

### Door（data 0–15，只写下半格）

| 类型名 |
|---|
| `WoodenDoorBlock` |
| `IronDoorBlock` |
| `CellDoorBlock` |

### Log（data 仅 0 = 竖向）

| 类型名 |
|---|
| `OakWoodBlock` |
| `BirchWoodBlock` |
| `SpruceWoodBlock` |
| `MimosaWoodBlock` |
| `PoplarWoodBlock` |

## 可导入的方块：逻辑材料键

除上面的具体类型名外，以下逻辑键同样接受（通过 `MaterialCatalog` 映射到具体类型）：

| 逻辑键 | 映射到 | 族 |
|---|---|---|
| `StoneBase` | CobblestoneBlock | Cube |
| `StoneWall` | StoneBrickBlock | Cube |
| `WoodWall` | PlanksBlock | Cube |
| `WoodTrim` | PlanksBlock | Cube |
| `Roof` | BricksBlock | Cube |
| `Glass` | GlassBlock | Cube |
| `Floor` | PlanksBlock | Cube |
| `RoofStairs` | WoodenStairsBlock | Stairs |
| `InteriorStairs` | WoodenStairsBlock | Stairs |
| `EntryDoor` | WoodenDoorBlock | Door |
| `WindowSill` | StoneSlabBlock | Slab |
| `FlatDeck` | StoneSlabBlock | Slab |
| `GardenFence` | WoodenFenceBlock | Fence |
| `StoneRailing` | StoneFenceBlock | Fence |
| `InteriorWall` | PlanksBlock | Cube |
| `SupportBeam` | PlanksBlock | Cube |
| `LogTrunk` | OakWoodBlock | Log |
| `LogBranch` | OakWoodBlock | Log |
| `Leaves` | SpruceLeavesBlock | Cube |

逻辑键的方块族由 `MaterialCatalog` 固定，不能用 `f` 冒充其他族；`d` 仍可在该族的合法范围内取非零值（例如 `RoofStairs` + `f:"stairs"` + `d:3`）。无论逻辑键还是具体类型名，省略 `f`/`d` 都只表示 `cube` + data 0，因此朝向族必须显式写出正确的 `f`。

## 明确不支持的方块

以下类型的方块**绝不可出现在蓝图 JSON 中**，出现则整份导入失败：

- 所有流体：WaterBlock、LavaBlock
- 所有植物（除 SpruceLeavesBlock / TallSpruceLeavesBlock）：花、高草、枯丛、树苗、落叶树叶
- 所有雪/冰：SnowBlock、IceBlock
- 受重力影响的方块：SandBlock、GravelBlock
- 带实体/容器/随机状态的方块：箱子、熔炉、TNT、音符盒、附魔台、酿造台等
- 木/铁梯子（带依附面、支撑完整性与攀爬行为）
- 红石/按钮/杠杆/压力板/中继器等电路组件
- 任意已开放 WoodBlock 的横向原木（data≠0）
- 所有染色/涂色状态（PaintedCube、Stairs、Slab、Fence 的颜色 DATA）；当前复制会明确跳过并报告，不会丢色粘贴
- 火把 TorchBlock（带实体）
- 任何 x25.01.17 中不存在或本 Mod 未逐一确认的类型

## 安全限制（生成时必须遵守）

| 参数 | 值 | 来源 |
|---|---|---|
| 每份蓝图最大格数 | **10000** | `BuildSafetyLimits.MaxBlocksPerOperation`（2026-10-20 由 2756 上调，历史 1024→2756→10000） |
| 每轴最大尺寸 | **64** | `BuildSafetyLimits.MaxSelectionAxisLength` |
| 每批写入最大格数 | **32** | `BuildSafetyLimits.MaxBlocksPerBatch`（运行时执行器用，不影响导入） |

超过这些值的蓝图文件在 `BlueprintJson.Decode` 阶段即被拒绝，不会部分导入。

> **注意（大型结构）**：上限升至 **10000** 后，默认大型树屋蓝图（2765 格）已**落在单事务上限之内**，因此它现在**可以**作为一份扁平蓝图 JSON 直接导入（仍受每轴 ≤ 64 约束）。不过 Mod 出厂默认仍走 Treehouse 运行模式按阶段（9 个 phase）逐段装配——这是为了让真机装配分步可读、可逐阶段撤销，而非受限于上限。你用单份 JSON 导入时 cells 须 ≤ 10000；超过请拆分为多份蓝图或改用分阶段装配。

## 建造期清障模式（v0.15.0 运行时开关，**不改变蓝图格式**）

这是一个**运行时（落建时）**的显式开关，默认关闭，通过手持撤销工具 `902` 点击天空进入模式循环、在 `Treehouse` 后再点一次“装填清障”开启。**它对蓝图 JSON 的 schema、可作者化的方块集合、`cells`/`materials` 结构没有任何影响**，AI 生成蓝图时**无需**为其写任何额外字段。它只改变“落建那一刻，蓝图 footprint 若被世界里已有的方块占据”时的处理方式：

- **关闭（默认）**：footprint 内若已有实心方块或水/岩浆，`BuildPlanner` 会**拒绝**该次建造并写明冲突（“实心方块”），一格都不写。此时若你想让蓝图落进去，须先手动清掉障碍。
- **开启**：footprint 内的**每一格被无条件替换**——已有的实心方块、以及**水/岩浆流体**一并被清掉并换成蓝图方块；**不留任何周边留白**（只动蓝图精确写入的那些格，footprint 之外一格不碰）。开启本身即“显式二次确认”，不再逐格中止、也不再走软清除的重复两按。
- **撤销**：无论开关状态，写入前都会捕获原值进撤销栈。清障模式下被覆盖掉的实心方块与**流体**都能经 `902` 精确还原到建造前的样子。

对 AI 的实际含义：**上面“明确不支持的方块”一节仍然成立**——你不能在水/岩浆/植物/实体方块等类型里挑选蓝图方块，蓝图里只能出现本文件列出的原版方块。但清障模式意味着：**你不必担心目标 footprint 在现实世界里已被地形或玩家建筑占据**——用户开启清障后，建造会无条件扫清 footprint 内的一切障碍（含流体）。生成器可放心按理想空地上产蓝图，落建障碍交由运行时开关处理。真机上流体清除与还原效果仍为**待验证**。

## 生成蓝图 JSON 的注意事项

1. **坐标为相对值**：蓝图中所有 cells 的 x/y/z 是相对于包围盒最小角的偏移。最小角自动归一化为 (0,0,0)。
2. **门只写下半格**：`WoodenDoorBlock`/`IronDoorBlock`/`CellDoorBlock` 只需给出 bottom 那一格；引擎的 `SubsystemDoorBlockBehavior` 会在下半写入后自动补出上半。写上半格会因找不到对应的下半格而报错。
3. **原木仅竖向**：五种已开放 WoodBlock 的 `d` 字段都只能是 `0`（竖向/Y 轴朝向）。横向原木不支持。
4. **f 字段可省略**：若所有方块都是 cube，可以完全省略每个 cell 的 `f` 和 `d`（缺省为 cube + data 0）。
5. **材质表可去重**：同一类型名多次使用只需在 `materials` 数组中登记一次，cells 的 `m` 指向它的 `id`。
6. **两种云杉叶是常青的**：`SpruceLeavesBlock`/`TallSpruceLeavesBlock` 没有落叶树的季节 DATA；“常青”不等于不会腐坏。
7. **树叶腐坏规则**（程序集已取证，真机效果待验证）：上述叶块如果在 Chebyshev 距离 3 内找不到任何 WoodBlock（原木），`SubsystemWoodBlockBehavior` 会让它腐坏。生成蓝图时务必确保叶团有原木在半径 3 内贯穿。

## 能力架构与完整审计

- `VanillaBlockCapabilities` 是游戏无关的能力元数据目录；当前精确列出 60 个可作者化类型及其族。
- `VanillaBlockRegistry` 在游戏运行时逐项调用 `BlocksManager.FindBlockByTypeName`，并核对实际基类族；目标客户端缺类型或族不符时不会激活。
- `BlockOrientation` 是按族编解码器：JSON 走严格 authored DATA 校验，世界复制走 `TryDecodeTerrainData`，仅楼梯 corner 派生位允许剥离。
- 公开 Windows x25.01.17 的 258 个具体 Block 类型、165 个 `IsPlaceable=TRUE` 类型及逐项阶段结论见 [完整方块审计](blueprint-block-audit-x25.01.17.md)。

## 扩展策略

添加新的支持方块必须满足以下条件（按 AGENTS.md 铁律）：

1. 该方块在 x25.01.17 程序集中真实存在（反编译取证）。
2. 通过反射确认其 `BlockFamily` 归属与自有 DATA 位范围。
3. 加入 `VanillaBlockCapabilities`；运行时注册表必须能找到精确类型并核对其实际基类族。
4. 本文件对应表格追加该方块。
5. net472 Release 0 错误 0 警告。
6. 该方块在 7723 真机上的渲染标为**待验证**直到确认。
7. 带实体/容器/随机/流体属性的方块一律**不支持、不添加**。

## 机器可读摘要

以下 JSON 对象供 AI agent 程序化解析，与本文件上半部表格内容等价，若有出入以表格文字为准：

```json
{
  "modVersion": "0.16.0",
  "targetGame": "Survivalcraft NET x25.01.17",
  "schemaVersion": 2,
  "limits": { "maxCells": 10000, "maxAxis": 64, "maxPerBatch": 32 },
  "families": {
    "cube":   { "dataRange": [0, 0],  "tags": ["cube"] },
    "stairs": { "dataRange": [0, 7],  "tags": ["stairs"] },
    "slab":   { "dataRange": [0, 1],  "tags": ["slab"] },
    "fence":  { "dataRange": [0, 15], "tags": ["fence"] },
    "door":   { "dataRange": [0, 15], "tags": ["door"], "note": "only bottom half in JSON" },
    "log":    { "dataRange": [0, 0],  "tags": ["log"],  "note": "vertical only" }
  },
  "supportedBlocks": {
    "cube": ["CobblestoneBlock","StoneBrickBlock","PlanksBlock","BricksBlock","GlassBlock","BasaltBlock","GraniteBlock","MarbleBlock","SandstoneBlock","ClayBlock","LimestoneBlock","MalachiteBlock","CoalBlock","CoalOreBlock","CopperBlock","CopperOreBlock","DiamondBlock","DiamondOreBlock","GermaniumOreBlock","IronBlock","IronOreBlock","SaltpeterOreBlock","SemiconductorBlock","SulphurOreBlock","FramedGlassBlock","WindowBlock","WickerLampBlock","SpruceLeavesBlock","TallSpruceLeavesBlock"],
    "stairs": ["WoodenStairsBlock","CobblestoneStairsBlock","StoneStairsBlock","StoneBrickStairsBlock","BrickStairsBlock","BasaltStairsBlock","MarbleStairsBlock","SandstoneStairsBlock","DirtStairsBlock"],
    "slab": ["WoodenSlabBlock","CobblestoneSlabBlock","StoneSlabBlock","StoneBrickSlabBlock","BrickSlabBlock","BasaltSlabBlock","MarbleSlabBlock","SandstoneSlabBlock","DirtSlabBlock"],
    "fence": ["WoodenFenceBlock","StoneFenceBlock","BrickFenceBlock","BasaltFenceBlock","IronFenceBlock"],
    "door": ["WoodenDoorBlock","IronDoorBlock","CellDoorBlock"],
    "log": ["OakWoodBlock","BirchWoodBlock","SpruceWoodBlock","MimosaWoodBlock","PoplarWoodBlock"]
  },
  "logicalKeys": {
    "StoneBase":"CobblestoneBlock","StoneWall":"StoneBrickBlock","WoodWall":"PlanksBlock",
    "WoodTrim":"PlanksBlock","Roof":"BricksBlock","Glass":"GlassBlock","Floor":"PlanksBlock",
    "RoofStairs":"WoodenStairsBlock","InteriorStairs":"WoodenStairsBlock","EntryDoor":"WoodenDoorBlock",
    "WindowSill":"StoneSlabBlock","FlatDeck":"StoneSlabBlock","GardenFence":"WoodenFenceBlock",
    "StoneRailing":"StoneFenceBlock","InteriorWall":"PlanksBlock","SupportBeam":"PlanksBlock",
    "LogTrunk":"OakWoodBlock","LogBranch":"OakWoodBlock","Leaves":"SpruceLeavesBlock"
  },
  "notSupported": [
    "fluids","plants(except SpruceLeavesBlock/TallSpruceLeavesBlock)","snow","ice","gravity blocks",
    "entity-bearing blocks","ladders","redstone","torches","saplings",
    "horizontal logs","painted/colored DATA","furniture blocks"
  ],
  "buildBehavior": {
    "clearObstaclesMode": {
      "default": "off",
      "scope": "blueprint-footprint-only",
      "clears": "every existing block inside the footprint is unconditionally replaced, including solid terrain AND fluids (water/lava)",
      "margin": "none (cells outside the exact footprint are never touched)",
      "affectsSchema": false,
      "undoRestores": "cleared blocks including displaced fluids are captured before write and restored by the Undo tool (902)",
      "note": "runtime build-time toggle only; blueprint JSON needs no extra fields for it. AI generators do NOT need to worry about pre-existing terrain/player obstacles in the target footprint — with the mode armed the build clears them."
    }
  }
}
```

---

维护核对（2026-10-24）：本次 SCIE-Port v0.22.3 只修复车厢 Collada 导入和轨道载具物品网格枚举，
未新增、移除或改变 Builder Assistant 支持的方块、族标签、DATA 范围或蓝图限制；本清单的能力版本
仍为 v0.16.0。

最后更新：v0.16.0（2026-10-22），方块能力第一阶段——从真实 x25.01.17 程序集与 BlocksData 审计 165 个可放置类型，能力注册表由 39 扩到 60，加入严格族/DATA 校验与有损状态拒绝；Android 7723 渲染/存读档仍待验证。下次 Mod 版本更新时必须同步修改本文件。

## SCIE-Port 非蓝图物品（苍穹之怒 V3.2 / SCIE-Port 0.28.0）

- `941` 雷神之锤与 `942` 苍穹之怒均为 `IsPlaceable=FALSE` 的手持武器，不属于 Builder Assistant 可生成方块族；任何蓝图 JSON 不得把它们写入 `materials[].block` 或 cell。新增 942 不改变现有可放置方块/DATA 范围。
