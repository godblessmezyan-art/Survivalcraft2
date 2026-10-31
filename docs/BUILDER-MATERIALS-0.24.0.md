# Builder Assistant v0.24.0：基础地形与常用装饰材料

日期：2026-10-31。目标SCNET x25.01.17/net472，Android7723实际行为待验证。按用户选择优先基础地形，并扩展状态简单的景观装饰；不直接解除所有原版方块的白名单。只修改Builder Assistant、测试、审计和规范；SCIE-Port不变。

## 新增18种，总计81种

| 用途 | 类型 |
|---|---|
| 土地 | DirtBlock、GrassBlock、SoilBlock |
| 砂石 | SandBlock、GravelBlock |
| 冰雪 | IceBlock、SnowBlock |
| 花草 | TallGrassBlock、RedFlowerBlock、PurpleFlowerBlock、WhiteFlowerBlock、DryBushBlock、LargeDryBushBlock |
| 地面装饰 | FallenLeavesBlock、CarpetBlock |
| 灯与石饰 | JackOLanternBlock、GravestoneBlock、CairnBlock |

新增材料进入已有导入、复制、旋转/镜像、存储、预览和施工管线，不新增工具ID、Hook、网络包、纹理或游戏资源。schema1/2/3兼容；新生成仍用schema3。一室一厅及旧10个家具模板保持原形状，不自动替换材料。

## 实际程序集取证

读取本机公开Windows x25.01.17：Survivalcraft.exe 2.4.0.0，SHA-256 `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`。只将反编译输出放在忽略的`artifacts/material-audit/`，不提交游戏代码或资源。

- Dirt/Ice仅原始DATA0；Grass DATA0/1由原生邻居回调重算雪边；Soil.GetHydration/SetHydration为位0、GetNitrogen/SetNitrogen为位1..2，精确0..7。
- TallGrass的幼苗位是8，Flower的幼苗位是1；两者不共用DATA。枯灌木仅0。未知高位与不符合具体类型的合法族DATA均拒绝，不掩码截断。
- Snow固定碰撞高度1/8、FallenLeaves固定1/16，不将DATA当厚度。Carpet的颜色0..15与cube染色编码分开。
- Gravestone有16种DATA：低3位造型、高1位轴向，原生只有0/90两种轴向；四分之一转切换位3，保留造型。JackOLantern模型数组4种朝向，每次几何旋转DATA减1 mod4。Cairn仅开放作者DATA0，不复现原生掉落变种。
- SubsystemCollapsingBlockBehavior处理沙7和砂砾6，支撑失效会创建MovingBlockSet。采用保守的完整顶面/耕地支撑子集，不接入移动沙块事务；原生Y<=0停止下落，故Y0可作隐式地基。
- Plant原生要求下方为泥土/草地/沙/耕地；Snow复用原生CanSupportSnow；Carpet、FallenLeaves、Gravestone要求不透明地基。石堆额外采用不透明地基限制。JackOLantern要求不透明顶面或栅栏。
- Grass扩散、Soil水肥/踩踏、幼苗长大、Weather融化/积雪、Carpet降雨删除、FallenLeaves季节消失均保留原生行为。树苗长树、作物跨格改土、季节树冠、电控门、容器、流体和设计表仍待专门接入。

## 实现

BlockOrientation增加falling/grass/soil/plant/snow/groundcover/gravestone/lantern/cairn，并启用既有carpet编解码。具体类型由VanillaBlockCapabilities限定DATA，由VanillaBlockRegistry核对真实类型/基类，旧类型与DATA不变。

AttachmentRules将新地面依附类型指向正下方；PlannedChange.HasSupport复用同一规则。既有BuildPlanner拓扑排序、BuildTask执行前检查/完成后分批复核、逆依赖撤销自动覆盖新类型。VanillaSupportRules抽出真实游戏支撑谓词，供生产Gateway和原生隔离探针共用，不修改目标游戏或加载器。

机器矩阵新增requiresSupport/environmentMutable字段。地形自然变化类型的undoTerrain和undoAllSubsystems均为false，表示不保证完整恢复环境副作用，已有撤销仍可用。蓝图管理窗口对含自然材料的蓝图显示环境提示。

## 自然变化与撤销边界

工具不会冻结世界，也不会拦截天气、重力或生长。自然变化发生后，撤销只恢复仍匹配已写值的格子，保留已经变化的格子；不会回收移动沙块、掉落物、撤回邻近草地传播或还原水肥/天气队列。新的材料支持不能被理解为完整环境事务。

建造前必须有有效支撑，清障不绕过此要求。完成时支撑失效或依附块消失会报失败并使用已有回滚；环境已经另行改变的格子仍按后续修改保护。真实回调时序、手机帧率与物理行为必须后续实测，本轮不宣称已经证明所有副作用恢复。

## 验证和复现

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-NetMod.ps1 -GameDir <本机SCNET目录>
dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --capabilities docs/blueprint-block-capabilities.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Audit-BuilderAssistantBlocks.ps1 -GameDir <本机SCNET目录> -OutputPath docs/blueprint-block-audit-x25.01.17.md
```

本机使用已安装SDK，net8规则测试由.NET9运行（DOTNET_ROLL_FORWARD=Major）。

- 每个具体类型的合法DATA穷举，复制/JSON往返、四次旋转、两次镜像及二面体关系，以及具体类型高位拒绝。
- 18种新增材料逐一预检、建造和即时撤销；清障不能绕过悬空；草花拒绝木地板，沙柱先支撑后上层，32格批次与逆序恢复，Y0隐式支撑、支撑中途移除和后续变化保护。
- 真实目标方法的独立fixture：原生Soil/TallGrass/Flower/Gravestone DATA、雪/落叶/植物/地毯支撑、南瓜灯栅栏例外和雪层碰撞高度。只使用已配置原生Block对象，不加载世界或GPU，不代表游戏内完整回调验证。
- 保留一室一厅、家具、蓝图库、区块等待、权限、取消/失败回滚和所有旧材料回归。
- [18种材料展台](examples/blueprint-materials-v3.json)通过真实导入和规划；旧schema保持可读。[规范](BLUEPRINT-SPEC.md)、[能力表](blueprint-block-reference.md)和[机器矩阵](blueprint-block-capabilities.json)保持一致。

Android7723与真实单机/房主/普通客户端行为：**待验证**。按照用户此前暂不真机测试的安排，本轮只做离线和构建验证。后续检查冰雪融化、草花状态、砂砾下落、自然变更后撤销、存读档和联机同步；普通客户端仍无世界写入权限。

实际结果：3193项离线检查、2014项目标API/隔离探针检查、环境哈希/ABI、模型导入、Release与包校验通过（0警告0错误）。产物`BuilderAssistant-0.24.0.netmod`，2402101字节；SHA-256 `4CB503EB7EA574DB9FA1E0B88814230DF49F1F286B34DD7F39E66D77C0D16732`。
