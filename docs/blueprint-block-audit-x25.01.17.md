# SCNET x25.01.17 原版可放置方块审计

> 此表由 `scripts/Audit-BuilderAssistantBlocks.ps1` 从目标程序集与同包 `Content.scpak/Assets/BlocksData.txt` 生成。分类依据是实际 CLR 继承链、静态 `Index`、`IsPlaceable` 与交互字段；“开放”还必须进入代码能力目录并通过族/DATA校验。

- `Survivalcraft.exe` SHA-256：`3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`
- 具体 `Block` 类型：258；`BlocksData.IsPlaceable=TRUE`：165。
- v0.24.0 开放：81 种；其余方块仍拒绝，不以类型名称猜测行为。
- 本证据对应公开 Windows x25.01.17 包，不替代 7723 Android 重打包哈希与真机验证。

## 六个核心组件审计结论

| 组件 | 审计发现 | 当前处置 |
|---|---|---|
| `VanillaBlockPalette` | 39 项硬编码表同时承担能力声明与运行时解析；解析只收 key+data，无法核对 cell 的 `f`。 | 拆成游戏无关 `VanillaBlockCapabilities` 与核对真实 `BlocksManager` 类型/基类的 `VanillaBlockRegistry`；palette 只负责严格解析。 |
| `BlockOrientation` | `FamilyFromTag` 对未知值回退 Cube；`Normalize` 会掩掉非法高位，可能隐藏颜色/状态损失。 | 严格 tag/类型/DATA；保留颜色与合法角形，原木开放0/1/2，研究族不进入导入白名单。 |
| `BlueprintJson` | 只拒绝未知 `f` 和负数 `d`，未检查 `f` 与材料真实族，也忽略 `materials[].block` 冲突。 | 对族、DATA、key/block 映射逐格拒绝；保留schemaVersion 1/2/3读取，新输出统一3。 |
| `BlueprintCopier` | 受支持类型的 live DATA 会经 Normalize 截断；彩色块和横向原木可能被低质量复制。 | 复制前按具体类型能力解码；颜色/角形/横向原木无损保留，未知/非规范值明确跳过并报告。 |
| `BuildPlanner` | 最终门只调用 `TryResolve(key,data)`，JSON 外构造的错误 family 可绕过前置一致性检查。 | 最终计划按类型/族/DATA校验，梯子/火把及新增地面依附材料按支撑排序；门上半格计入10000/32/64，写前及完工复核支撑。 |
| `GameTerrainGateway` | `ChangeCell` 主线程写入、区块 Valid+Cells 检查和 exact terrain-value 撤销适合普通方块；不能保存容器实体、电路队列或流体级联。 | 回读实际写入值；原值和同步门关联格在写前捕获，异常也返回实际变更日志；回滚/撤销最多32格并在失败处停止。复杂及异步级联事务仍不支持。 |

| ID | CLR 类型 | 直接基类 | 游戏分类 | 交互 | 当前结论 |
|---:|---|---|---|:---:|---|
| 2 | `DirtBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 3 | `GraniteBlock` | `PaintedCubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 4 | `SandstoneBlock` | `PaintedCubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 5 | `CobblestoneBlock` | `PaintedCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 6 | `GravelBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 7 | `SandBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 8 | `GrassBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 9 | `OakWoodBlock` | `WoodBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 10 | `BirchWoodBlock` | `WoodBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 11 | `SpruceWoodBlock` | `WoodBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 12 | `OakLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 13 | `BirchLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 14 | `SpruceLeavesBlock` | `EvergreenLeavesBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 15 | `GlassBlock` | `AlphaTestCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 16 | `CoalOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 17 | `WickerLampBlock` | `AlphaTestCubeBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 18 | `WaterBlock` | `FluidBlock` | Terrain | 否 | 流体/水生子系统：暂不开放 |
| 19 | `TallGrassBlock` | `CrossBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 20 | `RedFlowerBlock` | `FlowerBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 21 | `PlanksBlock` | `PaintedCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 24 | `PurpleFlowerBlock` | `FlowerBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 25 | `WhiteFlowerBlock` | `FlowerBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 26 | `StoneBrickBlock` | `PaintedCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 27 | `CraftingTableBlock` | `Block` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 28 | `DryBushBlock` | `CrossBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 31 | `TorchBlock` | `Block` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 39 | `IronOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 41 | `CopperOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 44 | `FramedGlassBlock` | `AlphaTestCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 45 | `ChestBlock` | `CubeBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 46 | `IronBlock` | `CubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 47 | `CopperBlock` | `CubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 48 | `CobblestoneStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 49 | `WoodenStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 50 | `StoneBrickStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 51 | `SandstoneStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 52 | `SandstoneSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 53 | `CobblestoneSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 54 | `StoneBrickSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 55 | `WoodenSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 56 | `WoodenDoorBlock` | `DoorBlock` | Items | 是 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 57 | `IronDoorBlock` | `DoorBlock` | Items | 是 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 58 | `CellDoorBlock` | `DoorBlock` | Items | 是 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 59 | `WoodenLadderBlock` | `LadderBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 60 | `WindowBlock` | `AlphaTestCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 61 | `SnowBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 62 | `IceBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 63 | `ChristmasTreeBlock` | `Block` | Items | 否 | 电路/复杂状态：暂不开放 |
| 64 | `FurnaceBlock` | `Block` | Items | 是 | 交互/复杂状态：暂不开放 |
| 65 | `LitFurnaceBlock` | `FurnaceBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 66 | `LimestoneBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 67 | `BasaltBlock` | `PaintedCubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 68 | `MarbleBlock` | `PaintedCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 69 | `MarbleStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 70 | `MarbleSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 71 | `MalachiteBlock` | `CubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 72 | `ClayBlock` | `PaintedCubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 73 | `BricksBlock` | `PaintedCubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 75 | `BrickSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 76 | `BrickStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 83 | `WoodenTrapdoorBlock` | `TrapdoorBlock` | Items | 是 | DATA 已取证；原生电路事务未证明，不支持 |
| 84 | `CellTrapdoorBlock` | `TrapdoorBlock` | Items | 是 | DATA 已取证；原生电路事务未证明，不支持 |
| 86 | `SpikedPlankBlock` | `MountedElectricElementBlock` | Items | 否 | 电路/复杂状态：暂不开放 |
| 87 | `GrassTrapBlock` | `Block` | Items | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 92 | `MagmaBlock` | `FluidBlock` | Terrain | 否 | 流体/水生子系统：暂不开放 |
| 94 | `WoodenFenceBlock` | `FenceBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 95 | `BasaltSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 96 | `BasaltStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 97 | `WoodenPostedSignBlock` | `PostedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 98 | `WoodenAttachedSignBlock` | `AttachedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 99 | `LargeDryBushBlock` | `CrossBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 100 | `SaltpeterOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 101 | `SulphurOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 105 | `SmallGunpowderKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 106 | `MediumGunpowderKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 107 | `LargeGunpowderKegBlock` | `GunpowderKegBlock` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 112 | `DiamondOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 119 | `SaplingBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 120 | `ThermometerBlock` | `Block` | Tools | 否 | 专用几何/行为：待取证 |
| 121 | `HygrometerBlock` | `Block` | Tools | 否 | 专用几何/行为：待取证 |
| 126 | `DiamondBlock` | `CubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 127 | `CactusBlock` | `Block` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 131 | `PumpkinBlock` | `BasePumpkinBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 132 | `JackOLanternBlock` | `Block` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 133 | `WireBlock` | `Block` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 134 | `NandGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 135 | `NorGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 136 | `StoneSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
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
| 148 | `GermaniumOreBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 150 | `CoalBlock` | `CubeBlock` | Minerals | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 151 | `PhotodiodeBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 152 | `LedBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 153 | `WireThroughPlanksBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 154 | `WireThroughStoneBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 155 | `WireThroughSemiconductorBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 156 | `XorGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 157 | `RandomGeneratorBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 163 | `BasaltFenceBlock` | `FenceBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 164 | `BrickFenceBlock` | `FenceBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 166 | `WoodenFenceGateBlock` | `FenceGateBlock` | Items | 是 | DATA 已取证；原生电路事务未证明，不支持 |
| 167 | `MagnetBlock` | `Block` | Items | 否 | 专用几何/行为：待取证 |
| 168 | `SoilBlock` | `CubeBlock` | Terrain | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
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
| 189 | `GravestoneBlock` | `Block` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 193 | `IronFenceBlock` | `FenceBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 194 | `IronFenceGateBlock` | `FenceGateBlock` | Items | 是 | DATA 已取证；原生电路事务未证明，不支持 |
| 197 | `IvyBlock` | `Block` | Plants | 否 | DATA 已取证；60秒延迟生长/级联队列未事务化，不支持 |
| 199 | `TargetBlock` | `MountedElectricElementBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 202 | `StoneFenceBlock` | `FenceBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 204 | `CottonBlock` | `CrossBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 208 | `CarpetBlock` | `CubeBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 209 | `CampfireBlock` | `Block` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 210 | `IronPostedSignBlock` | `PostedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 211 | `IronAttachedSignBlock` | `AttachedSignBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 213 | `IronLadderBlock` | `LadderBlock` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 216 | `DispenserBlock` | `CubeBlock` | Items | 是 | 实体/容器/持久状态：暂不开放 |
| 217 | `StoneStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 223 | `WireThroughBricksBlock` | `WireThroughBlock` | Electrics | 否 | 电路/复杂状态：暂不开放 |
| 224 | `AdjustableDelayGateBlock` | `RotateableMountedElectricElementBlock` | Electrics | 是 | 电路/复杂状态：暂不开放 |
| 225 | `TallSpruceLeavesBlock` | `EvergreenLeavesBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 226 | `SeaUrchinBlock` | `BottomSuckerBlock` | Plants | 否 | 流体/水生子系统：暂不开放 |
| 227 | `FurnitureBlock` | `Block` | Items | 否 | 实体/容器/持久状态：暂不开放 |
| 229 | `StarfishBlock` | `BottomSuckerBlock` | Plants | 否 | 流体/水生子系统：暂不开放 |
| 231 | `SemiconductorBlock` | `CubeBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
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
| 255 | `MimosaWoodBlock` | `WoodBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 256 | `MimosaLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |
| 258 | `CairnBlock` | `Block` | Items | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 259 | `DirtSlabBlock` | `SlabBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 260 | `DirtStairsBlock` | `StairsBlock` | Construction | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 261 | `FallenLeavesBlock` | `CubeBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 262 | `PoplarWoodBlock` | `WoodBlock` | Plants | 否 | v0.24.0 已开放（受类型/DATA/支撑限制；Android 待验证） |
| 263 | `PoplarLeavesBlock` | `DeciduousLeavesBlock` | Plants | 否 | 重力/生长/季节/世界更新：暂不开放 |

## 证据边界

- `IsPlaceable` 只说明游戏允许放置，不说明仅靠 terrain DATA 就能完整复制；容器内容、方块实体、电路节点、随机生长、流体传播与依附完整性需要额外子系统。
- `CubeBlock` 继承关系也不等于“无状态立方体”；例如箱子、土壤、积雪、彩色方块仍有额外 DATA 或行为，因此不会自动开放。
- 本轮开放类型的精确族、合法 DATA 和复制限制以 `docs/blueprint-block-reference.md` 为准。

## 剩余覆盖路线图

1. **颜色与原木**：DATA已能无歧义表达；不需schema3。静态API与离线往返已确认，Android渲染/存读档/联机待验证。
2. **依附/细节**：梯子和火把已加入支撑排序、执行再验与完成后复核；地毯已开放并记录降雨变化边界，电控门类/Ivy仍待电路或延迟生长事务。详见 `docs/BUILDER-DETAIL-STATES-0.18.0.md`。
3. **世界更新型方块**：基础土沙、冰雪、花草、落叶已按自然变化范围开放；树苗长树、农作物跨格更新和季节树冠仍待接入。完整副作用撤销需要额外事务，不在本版保证内。
4. **容器/实体/文本**：Chest、Furnace、Dispenser、Sign、Furniture 等需要 `SubsystemBlockEntities`、对应 Behavior、组件库存/文本序列化和实体创建/删除事务；撤销必须同时恢复 terrain value 与实体 payload。
5. **电路**：需要 `SubsystemElectricity`/`ElectricElement` 的创建、连接、排队更新和持久状态接口；仅写 terrain DATA 不足以证明电路正确。
6. **流体与水生方块**：需要 `SubsystemFluidBlockBehavior`、`SubsystemWaterBlockBehavior`、`SubsystemMagmaBlockBehavior` 的传播/衰减边界和多格撤销模型；在能冻结传播并记录级联变化前保持不支持。
