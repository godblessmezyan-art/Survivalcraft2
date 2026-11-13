# 书架与书籍方块：0.88.0

2026-11-14 新增纯装饰书架系列：单格书架、两格高书架、2×2 大书柜和地面书堆。四类根方块均可合成并在创造栏「建造与家居」（`SCIEPort.Structures`）取得；没有储物、坐姿、睡眠、照明或交互，不改变任何旧 ID/DATA/配方/网络包。

![说明：本页没有外部美术资源；模型为运行时程序化网格，尚未在 Android 实机截图]

## 内容与身份

| Block ID | 名称 | 创造 DATA | 占位 / 行为 |
|---|---|---|---|
| 973 | 书架 | 0 / 8 / 16 | 一格；浅木、深木、满书三款；纯装饰与碰撞 |
| 974 | 高书架 | 0 / 8 | 两格高；浅木、深木两款；上格复用隐藏 963，DATA bit7 标识高书架 |
| 975 | 大书柜 | 0 | 2×2 落地书柜；双面书格，四向旋转均留在四格占地内；纯装饰与碰撞 |
| 976 | 书堆 | 0 / 8 / 16 / 24 | 一格；书堆、散书、叠书、摊开的书四款；纯装饰与碰撞 |
| 977 | 大书柜占位 | 无 | 975 的三个隐藏辅助格；不可获得、不可独立放置、无独立掉落 |

全部属于 `SCIEPort.Structures` / 「建造与家居」，不新增 Tab，不添加神器移速、攻速或槽位主题。可创造外观共 10 个（973 三款、974 两款、975 一款、976 四款），新增运行时模型 10 个（每款四朝向共 40 个网格状态）。

## DATA 与掉落

- 低 2 位：朝向 0–3。
- bit2：保留未用。
- bits3–5：外观款式，按方块归一化为 `0..2`、`0..1` 或 `0..3`。
- 963 的上格：低 6 位沿用原家居 `rotation | (open << 2)`；bit7 为 `ShelfUpperBit`，仅高书架使用。原冰箱/窗帘的 bit6 语义不变，旧存档不必迁移。

`HomeRules.Normalize` 会把非法款式归零；掉落与创造值只保留外观款式，清除朝向和上格标记。

## 模型与渲染

模型不依赖 DAE、Blender 或 PNG 图集，而是 `BookshelfGeometry` 在 `Block.Initialize` 时按“款式 × 朝向”生成 `BlockMesh`：

- 木框、背板、层板、彩色书脊、米色书页和封底均由盒体组成；每本书 2 个盒体（书脊 + 书页），单模型三角形数远低于 6000 上限。
- 颜色写入顶点色，使用 1×1 白色 `Rgba8888` 纹理与 `(0.5, 0.5)` UV，不新增纹理资源，也不采样图集之外的像素。
- 各盒体面顺序、三角形绕序和 `BlockMeshVertex.Face` 与目标程序集原生立方体生成器一致；朝向只旋转水平面，顶/底面保持不变。
- 局部前方为 −Z；1×1 书架/高书架/书堆使用与现有单格家具相同的 `RotationY * Translation(.5, 0, .5)`。2×2 大书柜使用占地中心变换 `RotationY * Translation(cellCenter)`，保证四个朝向都不越出四格。
- 顶点色烘焙面明暗：顶面/正面 1.0、背面 0.92、左右 0.84、底面 0.55；实际世界光照仍由原生 `GenerateShadedMeshVertices` 计算。

书堆款式：0 五本平叠；1 三本散放；2 七本高叠；3 摊开的书加书签。

## 碰撞与多格生命周期

| 方块 | 碰撞（格坐标） |
|---|---|
| 书架 973 | `(0.04, 0, 0.28) - (0.96, 1.00, 0.96)` |
| 高书架 974 下格 | `(0.04, 0, 0.28) - (0.96, 1.00, 0.96)` |
| 高书架 974 上格（963） | `(0.04, 0, 0.28) - (0.96, 0.92, 0.96)` |
| 大书柜 975 / 977 | 每格 `(0.03, 0, 0.03) - (0.97, 1.90, 0.97)` |
| 书堆 976 | `(0.10, 0, 0.12) - (0.90, 0.34, 0.88)` |

- 973/974/976 是单格根方块；974 由主机的 `SubsystemHomeFurniture` 在主格上方写入 963，拆除任一半都会清理仍匹配的另一半，只回收一份物品。
- 975 复用既有 `HomeRules.HasFootprint` / `SubsystemHomeFurniture` 的 2×2 占位流程：放置前检查四格为空、区块有效、y 在 1–254；先写根格，再由主机队列写入三个 977 占位格，失败时按原事务路径撤销。
- 拆除 975 根格或任一 977 占位格都会清理整套占位，只回收一件大书柜。占位格不进入创造栏、不产生掉落、没有独立模型。
- 974 上格/975 占位的数据不参与玩家手动放置：CSV `DefaultCreativeData=-1`，`IsPlaceable=FALSE`。

没有新增网络包。多格写入、拆除和掉落沿用现有家居协议与主机权限路径；客户端不在本地写地形。

## 合成

四种根方块均在运行时生成 2×2 配方，材料通过 `BlocksManager.FindBlockByTypeName(..., false)?.GetCraftingId(0)` 解析，避免不同重打包版本的原版材料编号差异。原版没有纸/书方块，因此使用木板、木棍和棉絮：

| 产物 | 材料 |
|---|---|
| 书架 973 | 木板 ×3 + 木棍 ×1 |
| 高书架 974 | 木板 ×2 + 木棍 ×2 |
| 大书柜 975 | 木板 ×3 + 棉絮 ×1 |
| 书堆 976 | 木板 ×1 + 棉絮 ×1 |

失败分支：目标版本找不到 `PlanksBlock` / `StickBlock` / `CottonWadBlock` 时该配方不生成，不抛异常、不使用硬编码材料字符串。所有配方 `RequiredHeatLevel=0`、`RequiredPlayerLevel=0`、单产物、无返还物。

## 验证与待验证

已执行：

- `SCIEPort.Home.Tests`：规则/款式/bit7 上格反例/占位映射，模型状态由 24 增至 34；全部通过。
- `Test-Bookshelf.ps1`：CSV 5 行 18 列、隐藏件不可创造、40 个网格状态的顶点/索引/面编号/UV/包围盒/三角形预算、碰撞、四朝向占位、四张 2×2 配方；输出 `40 meshes, 5 blocks, collision and four two-by-two recipes verified`。
- 完整 `Build-NetMod.ps1` 构建与包内 CSV 字节核对（见 [TEST-RESULTS.md](TEST-RESULTS.md)）。

待验证：

| 范围 | 状态 | 说明 |
|---|---|---|
| Windows net472 Release、CSV/DLL/包内容 | 已确认 | 见 TEST-RESULTS |
| Android 7723 触屏放置、旋转、拆除、图标 | 待验证 | 未启动游戏或设备 |
| 真实 Local/Server/Client 多格放置、并发拆除、掉落 | 待验证 | 仅静态/隔离检查 |
| 与其它 Mod 的 973–977 / 963 bit7 冲突 | 待验证 | 仅核对固定目标与本工程；963 bit7 为本版新增语义 |
| 同房混用 0.88.0 之前版本 | 不支持 | 旧包缺 973–977 与上格 bit7 处理 |

Android 首测使用可丢弃的新世界与备份：验证四类方块、全部款式与四朝向、973/974/976 放置与拆除、974 上下任一半拆除、975 四格完整放置与任一角拆除、顶界/占用拒绝、触屏旋转与图标、不同分辨率、房主与普通客户端各放一次并互相拆除。测试设备与 7723 渠道程序集哈希未提供前，Android/实际联机只能是待验证。

## 文件

- 新增源码：`src/SCIEPort/Features/Home/BookshelfGeometry.cs`、`BookshelfBlocks.cs`。
- 修改源码：同目录 `HomeRules.cs`、`HomeBlocks.cs`、`HomeExtensionBlocks.cs`、`SubsystemHomeFurniture.cs`。
- 新增注册：`src/SCIEPort/Assets/Blocks/Bookshelf.csv`；没有修改旧 CSV、XDB 或 GUID。
- 新增测试：`scripts/Test-Bookshelf.ps1`；修改 `tests/SCIEPort.Home.Tests/Program.cs` 与 `scripts/Build-NetMod.ps1`。
- 文档：本文、`INVENTORY-STANDARD.md`、`COMPATIBILITY.md`、`TEST-RESULTS.md`、`README.md`、根 `docs/blueprint-block-reference.md`。

Builder Assistant 仍不支持这些 ID、977 占位或 963 bit7，不得写入其蓝图 JSON；两个工程没有跨源码/资产引用。
