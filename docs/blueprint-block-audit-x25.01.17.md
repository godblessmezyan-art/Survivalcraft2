# SCNET x25.01.17 原版可放置方块审计

> 此表由 `scripts/Audit-BuilderAssistantBlocks.ps1` 从目标程序集与同包 `Content.scpak/Assets/BlocksData.txt` 生成。分类依据是实际 CLR 继承链、静态 `Index`、`IsPlaceable` 与交互字段；“开放”还必须进入代码能力目录并通过族/DATA校验。

- `Survivalcraft.exe` SHA-256：`3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`
- 具体 `Block` 类型：258；`BlocksData.IsPlaceable=TRUE`：165。
- 第一批开放：60 种；其余方块仍拒绝，不以类型名称猜测行为。
- 本证据对应公开 Windows x25.01.17 包，不替代 7723 Android 重打包哈希与真机验证。

## 六个核心组件审计结论

| 组件 | 审计发现 | v0.16.0 处置 |
|---|---|---|
| `VanillaBlockPalette` | 39 项硬编码表同时承担能力声明与运行时解析；解析只收 key+data，无法核对 cell 的 `f`。 | 拆成游戏无关 `VanillaBlockCapabilities` 与核对真实 `BlocksManager` 类型/基类的 `VanillaBlockRegistry`；palette 只负责严格解析。 |
| `BlockOrientation` | `FamilyFromTag` 对未知值回退 Cube；`Normalize` 会掩掉非法高位，可能隐藏颜色/状态损失。 | 增加严格 tag 解析、每族 authored DATA 范围与世界 DATA 解码；只允许剥离已证实的楼梯 corner 派生位。 |
| `BlueprintJson` | 只拒绝未知 `f` 和负数 `d`，未检查 `f` 与材料真实族，也忽略 `materials[].block` 冲突。 | 对族、DATA、key/block 映射逐格拒绝；schemaVersion 1/2 均保留。 |
| `BlueprintCopier` | 受支持类型的 live DATA 会经 Normalize 截断；彩色块和横向原木可能被低质量复制。 | 复制前按族解码；颜色、横向原木或未知位明确跳过并按 `Type(DATA=n)` 报告。 |
| `BuildPlanner` | 最终门只调用 `TryResolve(key,data)`，JSON 外构造的错误 family 可绕过前置一致性检查。 | 最终计划阶段改为 `TryResolve(key,family,data)`，任何入口都必须通过相同能力校验；10000/32/64、取消、撤销、区块与权限机制未改。 |
| `GameTerrainGateway` | `ChangeCell` 主线程写入、区块 Valid+Cells 检查和 exact terrain-value 撤销适合普通方块；不能保存容器实体、电路队列或流体级联。 | 第一阶段不改写入路径；复杂状态/实体/流体继续拒绝，直至能做跨子系统事务与撤销。 |

| ID | CLR 类型 | 直接基类 | 游戏分类 | 交互 | 第一阶段结论 |
|---:|---|---|---|:---:|---|
| 2 | `DirtBlock` | `CubeBlock` | Terrain | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 3 | `GraniteBlock` | `PaintedCubeBlock` | Terrain | 否 | 第一批已开放（受族/DATA限制） |
| 4 | `SandstoneBlock` | `PaintedCubeBlock` | Terrain | 否 | 第一批已开放（受族/DATA限制） |
| 5 | `CobblestoneBlock` | `PaintedCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 6 | `GravelBlock` | `CubeBlock` | Terrain | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 7 | `SandBlock` | `CubeBlock` | Terrain | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 8 | `GrassBlock` | `CubeBlock` | Terrain | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 9 | `OakWoodBlock` | `WoodBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 10 | `BirchWoodBlock` | `WoodBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 11 | `SpruceWoodBlock` | `WoodBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 12 | `OakLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 13 | `BirchLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 14 | `SpruceLeavesBlock` | `EvergreenLeavesBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 15 | `GlassBlock` | `AlphaTestCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 16 | `CoalOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 17 | `WickerLampBlock` | `AlphaTestCubeBlock` | Items | 否 | 第一批已开放（受族/DATA限制） |
| 18 | `WaterBlock` | `FluidBlock` | Terrain | 否 | 流体/水生子系统：暂不开放 |
| 19 | `TallGrassBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 20 | `RedFlowerBlock` | `FlowerBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 21 | `PlanksBlock` | `PaintedCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 24 | `PurpleFlowerBlock` | `FlowerBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 25 | `WhiteFlowerBlock` | `FlowerBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 26 | `StoneBrickBlock` | `PaintedCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 27 | `CraftingTableBlock` | `Block` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 28 | `DryBushBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 31 | `TorchBlock` | `Block` | Items | 否 | 依附/新朝向族：待编解码与支撑校验 |
| 39 | `IronOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 41 | `CopperOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 44 | `FramedGlassBlock` | `AlphaTestCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 45 | `ChestBlock` | `CubeBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 46 | `IronBlock` | `CubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 47 | `CopperBlock` | `CubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 48 | `CobblestoneStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 49 | `WoodenStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 50 | `StoneBrickStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 51 | `SandstoneStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 52 | `SandstoneSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 53 | `CobblestoneSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 54 | `StoneBrickSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 55 | `WoodenSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 56 | `WoodenDoorBlock` | `DoorBlock` | Items | 是 | 第一批已开放（受族/DATA限制） |
| 57 | `IronDoorBlock` | `DoorBlock` | Items | 是 | 第一批已开放（受族/DATA限制） |
| 58 | `CellDoorBlock` | `DoorBlock` | Items | 是 | 第一批已开放（受族/DATA限制） |
| 59 | `WoodenLadderBlock` | `LadderBlock` | Items | 否 | 依附/新朝向族：待编解码与支撑校验 |
| 60 | `WindowBlock` | `AlphaTestCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 61 | `SnowBlock` | `CubeBlock` | Terrain | 否 | 依附/新朝向族：待编解码与支撑校验 |
| 62 | `IceBlock` | `CubeBlock` | Terrain | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 63 | `ChristmasTreeBlock` | `Block` | Items | 否 | 专用几何/行为：待取证 |
| 64 | `FurnaceBlock` | `Block` | Items | 是 | 交互/复杂状态：暂不开放 |
| 65 | `LitFurnaceBlock` | `FurnaceBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 66 | `LimestoneBlock` | `CubeBlock` | Terrain | 否 | 第一批已开放（受族/DATA限制） |
| 67 | `BasaltBlock` | `PaintedCubeBlock` | Terrain | 否 | 第一批已开放（受族/DATA限制） |
| 68 | `MarbleBlock` | `PaintedCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 69 | `MarbleStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 70 | `MarbleSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 71 | `MalachiteBlock` | `CubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 72 | `ClayBlock` | `PaintedCubeBlock` | Terrain | 否 | 第一批已开放（受族/DATA限制） |
| 73 | `BricksBlock` | `PaintedCubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 75 | `BrickSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 76 | `BrickStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 83 | `WoodenTrapdoorBlock` | `TrapdoorBlock` | Items | 是 | 依附/新朝向族：待编解码与支撑校验 |
| 84 | `CellTrapdoorBlock` | `TrapdoorBlock` | Items | 是 | 依附/新朝向族：待编解码与支撑校验 |
| 86 | `SpikedPlankBlock` | `MountedElectricElementBlock` | Items | 否 | 电路/复杂状态：暂不开放 |
| 87 | `GrassTrapBlock` | `Block` | Items | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 92 | `MagmaBlock` | `FluidBlock` | Terrain | 否 | 流体/水生子系统：暂不开放 |
| 94 | `WoodenFenceBlock` | `FenceBlock` | Items | 否 | 第一批已开放（受族/DATA限制） |
| 95 | `BasaltSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 96 | `BasaltStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 97 | `WoodenPostedSignBlock` | `PostedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 98 | `WoodenAttachedSignBlock` | `AttachedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 99 | `LargeDryBushBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 100 | `SaltpeterOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 101 | `SulphurOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 105 | `SmallGunpowderKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 106 | `MediumGunpowderKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 107 | `LargeGunpowderKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 112 | `DiamondOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 119 | `SaplingBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 120 | `ThermometerBlock` | `Block` | Tools | 否 | 专用几何/行为：待取证 |
| 121 | `HygrometerBlock` | `Block` | Tools | 否 | 专用几何/行为：待取证 |
| 126 | `DiamondBlock` | `CubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 127 | `CactusBlock` | `Block` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 131 | `PumpkinBlock` | `BasePumpkinBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 132 | `JackOLanternBlock` | `Block` | Items | 否 | 专用几何/行为：待取证 |
| 133 | `WireBlock` | `Block` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 134 | `NandGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 135 | `NorGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 136 | `StoneSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 137 | `AndGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 138 | `BatteryBlock` | `Block` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 139 | `LightbulbBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 140 | `NotGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 141 | `SwitchBlock` | `MountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 142 | `ButtonBlock` | `MountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 143 | `OrGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 144 | `PressurePlateBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 145 | `DelayGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 146 | `SRLatchBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 147 | `DetonatorBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 148 | `GermaniumOreBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 150 | `CoalBlock` | `CubeBlock` | Minerals | 否 | 第一批已开放（受族/DATA限制） |
| 151 | `PhotodiodeBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 152 | `LedBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 153 | `WireThroughPlanksBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 154 | `WireThroughStoneBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 155 | `WireThroughSemiconductorBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 156 | `XorGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 157 | `RandomGeneratorBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 163 | `BasaltFenceBlock` | `FenceBlock` | Items | 否 | 第一批已开放（受族/DATA限制） |
| 164 | `BrickFenceBlock` | `FenceBlock` | Items | 否 | 第一批已开放（受族/DATA限制） |
| 166 | `WoodenFenceGateBlock` | `FenceGateBlock` | Items | 是 | 依附/新朝向族：待编解码与支撑校验 |
| 167 | `MagnetBlock` | `Block` | Items | 否 | 专用几何/行为：待取证 |
| 168 | `SoilBlock` | `CubeBlock` | Terrain | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 173 | `SeedsBlock` | `FlatBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 174 | `RyeBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 179 | `MotionDetectorBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 180 | `DigitalToAnalogConverterBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 181 | `AnalogToDigitalConverterBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 182 | `FourLedBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 183 | `SoundGeneratorBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 184 | `CounterBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 185 | `SevenSegmentDisplayBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 186 | `MemoryBankBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 187 | `RealTimeClockBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 188 | `TruthTableCircuitBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 189 | `GravestoneBlock` | `Block` | Items | 否 | 专用几何/行为：待取证 |
| 193 | `IronFenceBlock` | `FenceBlock` | Items | 否 | 第一批已开放（受族/DATA限制） |
| 194 | `IronFenceGateBlock` | `FenceGateBlock` | Items | 是 | 依附/新朝向族：待编解码与支撑校验 |
| 197 | `IvyBlock` | `Block` | Plants | 否 | 依附/新朝向族：待编解码与支撑校验 |
| 199 | `TargetBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 202 | `StoneFenceBlock` | `FenceBlock` | Items | 否 | 第一批已开放（受族/DATA限制） |
| 204 | `CottonBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 208 | `CarpetBlock` | `CubeBlock` | Items | 否 | 依附/新朝向族：待编解码与支撑校验 |
| 209 | `CampfireBlock` | `Block` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 210 | `IronPostedSignBlock` | `PostedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 211 | `IronAttachedSignBlock` | `AttachedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 213 | `IronLadderBlock` | `LadderBlock` | Items | 否 | 依附/新朝向族：待编解码与支撑校验 |
| 216 | `DispenserBlock` | `CubeBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 217 | `StoneStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 223 | `WireThroughBricksBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 224 | `AdjustableDelayGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 225 | `TallSpruceLeavesBlock` | `EvergreenLeavesBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 226 | `SeaUrchinBlock` | `BottomSuckerBlock` | Plants | 否 | 流体/水生子系统：暂不开放 |
| 227 | `FurnitureBlock` | `Block` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 229 | `StarfishBlock` | `BottomSuckerBlock` | Plants | 否 | 流体/水生子系统：暂不开放 |
| 231 | `SemiconductorBlock` | `CubeBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 232 | `KelpBlock` | `WaterPlantBlock` | Plants | 否 | 流体/水生子系统：暂不开放 |
| 233 | `SeagrassBlock` | `WaterPlantBlock` | Plants | 否 | 流体/水生子系统：暂不开放 |
| 234 | `SmallIncendiaryKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 235 | `MediumIncendiaryKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 236 | `LargeIncendiaryKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 237 | `PistonBlock` | `Block` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 238 | `PistonHeadBlock` | `Block` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 243 | `WireThroughCobblestoneBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 244 | `RottenPumpkinBlock` | `BasePumpkinBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 253 | `OneLedBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 254 | `MulticoloredLedBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 255 | `MimosaWoodBlock` | `WoodBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 256 | `MimosaLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 258 | `CairnBlock` | `Block` | Items | 否 | 专用几何/行为：待取证 |
| 259 | `DirtSlabBlock` | `SlabBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 260 | `DirtStairsBlock` | `StairsBlock` | Construction | 否 | 第一批已开放（受族/DATA限制） |
| 261 | `FallenLeavesBlock` | `CubeBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 262 | `PoplarWoodBlock` | `WoodBlock` | Plants | 否 | 第一批已开放（受族/DATA限制） |
| 263 | `PoplarLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |

## 证据边界

- `IsPlaceable` 只说明游戏允许放置，不说明仅靠 terrain DATA 就能完整复制；容器内容、方块实体、电路节点、随机生长、流体传播与依附完整性需要额外子系统。
- `CubeBlock` 继承关系也不等于“无状态立方体”；例如箱子、土壤、积雪、彩色方块仍有额外 DATA 或行为，因此不会自动开放。
- 第一批开放类型的精确族、合法 DATA 和复制限制以 `docs/blueprint-block-reference.md` 为准。

## 剩余覆盖路线图

1. **颜色状态与更多静态候选**：为 PaintedCube/Stairs/Slab/Fence 设计显式颜色状态（优先预留 schema v3 的 `state` 对象），逐一验证存读档、旋转与复制；在此之前带颜色 DATA 一律拒绝。
2. **简单朝向/依附族**：Trapdoor、FenceGate、Ladder、Torch、Carpet、Ivy 等分别建立 codec，并取证 `SubsystemTrapdoorBlockBehavior`、`SubsystemFenceGateBlockBehavior`、`SubsystemLadderBlockBehavior`、`SubsystemTorchBlockBehavior` 的支撑检查、邻格删除和撤销顺序。
3. **世界更新型方块**：重力、植物、树苗、雪/冰、季节叶片需要验证随机 tick/轮询、移动方块及级联变化；撤销必须捕获由子系统产生的额外格，不能只保存蓝图 footprint。
4. **容器/实体/文本**：Chest、Furnace、Dispenser、Sign、Furniture 等需要 `SubsystemBlockEntities`、对应 Behavior、组件库存/文本序列化和实体创建/删除事务；撤销必须同时恢复 terrain value 与实体 payload。
5. **电路**：需要 `SubsystemElectricity`/`ElectricElement` 的创建、连接、排队更新和持久状态接口；仅写 terrain DATA 不足以证明电路正确。
6. **流体与水生方块**：需要 `SubsystemFluidBlockBehavior`、`SubsystemWaterBlockBehavior`、`SubsystemMagmaBlockBehavior` 的传播/衰减边界和多格撤销模型；在能冻结传播并记录级联变化前保持不支持。
