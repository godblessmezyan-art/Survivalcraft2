# 金字塔埃及装饰方块方案 · 1.7.4（已实施）

## 1.10.5 法老主墓室

新生成的金字塔把大型石棺固定在主墓室后侧。占地 3×2 格，石台约长 2.8 格，棺盖最高 1.32 格。深色石材、内凹纹饰和低饱和矿物色突出石棺的体量。开盖时盖板沿长轴平移，棺口露出一道缝。玩家能从两侧绕行，进出藏器室和接近奖台。

两尊约 2.7 格高的站立阿努比斯石像夹住正中视线。大型火盆、石碑、供桌和成组埋葬罐分别布置在墓室侧边。只有一只火盆默认点燃，使用 9 级原生光。普通迷宫房间约四分之一保留一件小物，减少常亮石灯。旧墓室仍按其保存的布局版本读取。统一图集为石材、陶器和涂料换用了更克制的色调。

大型装饰使用 886 的世界生成专用 DATA。石棺 12 个占用格各有碰撞和点击范围，模型切分后分别从所在方块采样原生光照。棺盖的开合保存在根格，点中任意一段都走同一根格。234 请求仍只传坐标，主机或单机验证存活、距离、视线和组合完整性。四种大型装饰的各自占用都限制在同一区块内。

新站点保存 `Monumental`。未记录此标记的旧神殿按原型继续生成余下区块。Blender 的第一人称预览用于检查比例、轮廓、材质和空间关系。目标引擎已验证导入与地形交互，Android 实际亮度、触屏操作及真实联机待验证。[测试记录](TEST-RESULTS.md)

## 1.10.4 重制

九种装饰包含 14 份模型，四款卡诺匹斯罐分别用人、狒狒、胡狼和猎鹰头雕。石棺有贴地棺身和带彩绘的浮雕棺盖，打开时露出内腔与裹尸布。阿努比斯采用卧姿胡狼造型。陶罐与火盆使用凹入内壁，亡灵书铺展在双卷轴之间，方尖碑配金色尖顶。石灰岩、青金石、金饰、陶器和纸莎草共用原创 512×512 图集。

直接按 SCNET 的 Y 向上格式导出。模型水平轴以方块中心为原点，高度从 0 开始。每个造型将同一图集区域合入一个网格，使用单骨骼和独立的顶点、法线、UV 索引。模型和贴图可通过 `python SCIE-Port/scripts/New-PyramidDecorAssets.py` 重建，SHA-256 见 [资产清单](assets/pyramid-decor/manifest.json)。Blender 预览使用实际输出的 DAE 和 PNG，游戏运行时用每个方向和状态下的网格包围盒计算碰撞与点按范围。

886 的 DATA、世界生成位置、开棺、点火、光照等级和存档沿用。目标引擎导入已验证，Android 7723 和真实联机待验证。

## 历史记录

1.10.3 将墓室装饰迁到 886，以兼容使用 947 的山地庇护帐。以下为 1.7.4 当时的编号记录。[现行编号与迁移规则](PYRAMID-1.10.3.md)

状态：**已实施**（1.7.3 方块 + 1.7.4 地形生成接入）。考据基于真实古埃及墓葬习俗（石棺/套棺、卡诺匹斯罐、沙伯提俑、亡灵书、供桌、卡诺匹斯箱、阿努比斯像）。目标：提升环境表现力与《氛围增强建议》中的压抑、未知、危险感，**不改变战斗数值**。

> **实施架构修正**：原稿设想给九种装饰各分配独立方块 ID 1024–1032。实施时发现目标引擎 `Terrain.ContentsMask=1023`（方块 ID 硬上限 1023），且 903–1023 已被本工程全部占用，独立 ID 不可行。最终改为**九种共用一个方块 ID 947**（唯一空闲号），用 DATA 位编码：kind(bit0–4)/变体(bit5–6)/状态(bit7)/旋转(bit8–9)。DATA 有 18 位，容量充足；这与家居方块用变体共享 ID 是同一成熟模式。下文表格中的"ID 1024–1032"应理解为"kind 0–8，同属 ID 947"。

> **1.7.4 地形接入**：1.7.3 只在藏器室/墓室外环放了少量标志物。1.7.4 把装饰并入 `PyramidLayout` 的布局管线：三层迷宫的**每个房间**按种子在地面四角放 1–2 件，层间采用不同主题（陶罐/沙伯提/供桌/亡灵书 → 卡诺匹斯罐/雕像 → 石棺/方尖碑/火盆）；墓室与藏器室保留原标志摆件；旧版无迷宫布局也在主厅、桥下、迷宫、上层廊道和地宫补上同样的九种方块。选择用无状态哈希 `(seed, level, cell, slot)`，因此任意切片生成顺序、正反顺序结果一致；只写已完成布局中确认为空气的单格，房间中心、通道喉口、楼梯、机关与奖台均不占用。

## 一、为什么用新方块而不是复用贴图

现有金字塔只用 5 种原生石料 + 机关方块 1023（DATA 0–12）。机关块已承载封印/石灯/浮雕，继续往里塞 DATA 会让机关逻辑与装饰耦合、难以维护，且 16px 象形贴图无法表达石棺、陶罐的立体轮廓。用独立方块 + 独立 3D 模型能带来：真实体块与碰撞、朝向变体、可交互（开棺/点火）、独立光照与掉落——这是平面浮雕做不到的。

## 二、方块清单（考据对应，按优先级）

| 优先级 | 方块 | 真实依据 | 交互/光照 | 放置位置（现有坐标） |
|---|---|---|---|---|
| P0 | 石棺 Sarcophagus | 多层套棺，木乃伊居中；饰象形文字与死者名 | 可开启（掀盖），内部可放战利品；微光 2 | 地下墓室 `BurialDetails()` 石台 x58–64,z62–67,y-10；藏器室 x37–43,y-11 |
| P0 | 卡诺匹斯罐组 CanopicJars | 4 罐对应荷鲁斯四子：人首伊姆塞特(肝)、狒狒首哈碧(肺)、豺狼首杜阿穆泰夫(胃)、隼首凯贝克塞努弗(肠)，置于卡诺匹斯箱 | 4 个 DATA 变体（四种首），可打碎；无光 | 墓室石台四角、壁龛；法老墓室四角对称摆放 |
| P1 | 瓦罐/储粮罐 StorageJar | 墓中储食物、油、酒的陶罐 | 可打碎掉落；无光 | 墓道两侧、迷宫小间角落、藏器室沿墙成排 |
| P1 | 沙伯提俑 Shabti | 木乃伊形小雕像，“回答的人"，替死者服役；常成组、可数百 | 可拾取；微弱光 1 | 墓室成组排列（如 3×4）、楼梯转角 |
| P1 | 阿努比斯像 AnubisStatue | 胡狼首死神，守墓、称量心脏 | 站立守卫像，不可交互；无光 | 正门两侧、墓室入口、法老墓前 |
| P2 | 供桌 OfferingTable | 死者供品桌，常刻面包/水/牛腿浮雕 | 可放供品（缓冲）；微光 2 | 墓室石棺前、藏器室祭坛位 |
| P2 | 火盆 Brazier | 墓室照明与仪式火灾 | 可点燃/熄灭，发光 13，可熄灭做氛围事件 | 主厅柱间、墓道、墓室四角 |
| P2 | 亡灵书卷轴 BookOfDead | 随葬文本，指引来世、心脏称重 | 可拾取（线索文本）；无光 | 石棺旁、壁龛、法老墓 |
| P3 | 方尖碑/石碑 Obelisk | 墓碑与纪念柱 | 大型地标，不可交互 | 入口平台、主厅；体量大，最后做 |

## 三、实现链路（照抄现成文件，逐环节给路径）

1. **C# 方块类**：照 `Features/Home/HomeBlocks.cs:163-189` 的 `HomeSofaBlock : HomeBlock { const int Index = ...; }`；变体用 `HomeRules.cs:24-27` 的 `Variant/Rotation(data)` 位运算（data>>3 选变体、data&3 选朝向）。
2. **ID 常量（已按实施修正）**：现有占用 903–1023，机关块=1023；`Terrain.ContentsMask=1023` 使 1024+ 越界截断，不可分配。九种装饰共用唯一空闲索引 **947**（kind=0..8，见 `PyramidDecorBlocks.cs` / `PyramidDecorRules.cs`），DATA 位编码 kind/variant/state/rotation。不扩 `scripts/Test-Environment.ps1` 的 `903..1023` 保留区间，`docs/PACKAGING-RULES.md`、`docs/INVENTORY-STANDARD.md`、`COMPATIBILITY.md` 已同步为 947 单 ID 方案。
3. **CSV 注册**：新建 `Assets/Blocks/PyramidDecor.csv`，列照 `Pyramid.csv` 与 `HomeFurniture.csv`：Class Name / DisplayName / Category=`SCIEPort.Structures` / Behaviors / DisplayOrder / CreativeData / DefaultEmittedLightAmount / DigMethod=Quarry / DigResilience / DefaultSoundMaterialName=Stone。隐藏占位块 CreativeData=-1。
4. **XDB**：**不需要 BlockTemplate**（方块靠 CSV Class + C# Index 注册）。只有交互块才在 `Assets/Database/Pyramid.xdb` 加 `BlockBehaviors/SubsystemTemplate`（`Class` 全限定名，`InheritanceParent=fefb9590-4972-4893-b02a-76063611b745`）+ `ProjectTemplate/MemberSubsystemTemplate`；可开启石棺另加 `Entities/EntityTemplate` + 存储组件（照 `HomeFurniture.xdb` 的 ComponentHomeStorage）。
5. **模型 .dae**：照 `scripts/New-HomeFurnitureAssets.py` 的 Blender `export()`（L294-328，手写 COLLADA）。硬约束（`Test-HomeGeometry.py:21-54`）：`up_axis=Y_UP`、`unit=meter`、单一 identity 节点、**Bones.Count==1**、位置/法线 `(x,z,-y)`、UV 的 V 翻转、p/n/uv 独立 offset 0/1/2、无退化三角形、每部件 UV 跨度 ≤0.23001、面数 100–6000、前方 -Z。
6. **纹理图集**：新建 `Assets/Textures/PyramidDecor.png`（512×512 全不透明），照 `scripts/New-PyramidAtlas.py` 逐像素写 PNG；C# `ContentManager.Get<Texture2D>("Textures/PyramidDecor")`。每块贴图 UV 落在自己 tile 内。
7. **地形放置**：在 `Features/Pyramid/PyramidLayout.cs` 对应方法内用现有 `Set/Fill`（L300-309）写入方块值；这些坐标是针对 80×80 遗址的全局坐标，`Fill` 已按切片 z0 裁剪，安全。**只在 `site.Tomb`/`site.Maze` 新墓室版布置**，旧版布局不动。隐藏占位与多格结构用 CreativeData=-1 + 空 `GetCreativeValues()` 保持旧存档兼容。
8. **验证脚本**：新增 `scripts/Test-PyramidDecorGeometry.py`（静态几何/图集/哈希，照 `Test-HomeGeometry.py`）与在 `Test-HomeFurniture.ps1`/`Test-TidePalace.ps1` 中加真实 Engine 导入校验；确认方块能加载、UV/朝向正确、不破坏原生地形白名单。

## 四、布置与玩法联动

- **石棺**：墓室与藏器室各一具，可开启取战利品；与法老守墓呼应，开棺可作为 `Solved` 触发条件之一（不改战斗数值）。
- **卡诺匹斯罐**：墓室按真实方位对称摆四只（人/狒狒/豺狼/隼首），呼应“荷鲁斯四子守护内脏"；打碎其中一只可作为氛围事件（声音+灯灭），接上一稿的死寂预警。
- **沙伯提俑**：成组排列，符合“数百替死者服役"；拾取给线索文本，暗示墓主身份与诅咒。
- **火盆**：主厅/墓道/墓四角；玩家或守卫可触发点燃/熄灭，直接支撑光影节奏（暗区+骤亮点）。
- **亡灵书/供桌**：叙事道具，低提示文字（符合项目“让玩家自己探索"约束），线索碎片化。
- 装饰方块一律**无新增网络包 ID**，沿用原生方块同步与存档；仅交互用现有 BlockBehavior 机制。

## 五、兼容与验收约束（不可省）

- 方块共用 ID 947，处于既有 903–1023 保留区间；`Terrain.ContentsMask=1023` 下不可改用 1024+，每种植被由 DATA kind 区分。
- 新模型/图集按 `Test-Environment.ps1`、`Test-*-Geometry.py`、真实 Engine 导入三关校验；SHA256 与来源记录归档。
- Android/7723 实机检查方块渲染、碰撞、朝向、交互触屏可达性；联机分别测 Local/Server/Client。未实测前不标“兼容"。
- 新方块不写入 `docs/blueprint-block-reference.md` 的可施工白名单（非建造用途），避免被蓝图系统误用。
- 版本号与 modinfo/csproj 同步升级（1.7.4），文档、COMPATIBILITY.md、TEST-RESULTS.md 一并更新。

## 六、实施顺序建议

1. 先确认方块 ID 硬上限与空闲索引（已由 1024+ 修正为共享 947）并跑环境断言。
2. P0 石棺 + 卡诺匹斯箱/四罐（核心题材，验证整条链路）。
3. P1 瓦罐/沙伯提俑/阿努比斯像（铺量、出氛围）。
4. P2 火盆 + 亡灵书 + 供桌（光影与叙事联动）。
5. P3 方尖碑（大体量地标，最后做）。
6. 全套验证 → 文档 → 版本 → 打包。

每完成一级即跑一次 `Test-TidePalace.ps1` 与原生测试，确保新方块未破坏金字塔生成。
