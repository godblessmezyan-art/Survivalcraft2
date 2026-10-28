# Builder Assistant v0.18.0 精细建筑能力开发报告

日期：2026-10-28。基线：main `349db87`（已有60种原版目录、元数据0.17.0）。本轮在独立 `codex/builder-detail-states` 工作树继续现有蓝图、运行时注册表和事务管线；未修改 SCIE-Port、其他端口、世界存档、工具编号、资源、Hook 签名或 TargetFramework。

## 交付范围

1. 新增材料：`WoodenLadderBlock`（59）、`IronLadderBlock`（213）、`TorchBlock`（31），总计63种。梯子/火把的依附、复制、变换、JSON往返、施工再验和下一次更新复核走现有管线。
2. 新增 DATA：九种 PaintedCube 的未染色/16色；九种楼梯的24种几何×17种颜色状态；九种半砖的2×17状态；五种栅栏的16×17状态；五种原木的Y/Z/X三轴；梯子四面和火把四壁/地面。楼梯合法角形完整保留，未定义corner=3拒绝；原木setter不生成的别名3拒绝。
3. 活板门、栅栏门、地毯、藤蔓已完成目标类型、DATA和行为取证，门类还实现研究用旋转/镜像codec，但材料/导入仍不支持。原生电路、天气删除、藤蔓延迟队列缺少完整事务证明，不能把codec测试当成放置能力。
4. schemaVersion 1/2保持读取，继续导出2。`type/family/d` 已可无歧义表达本轮状态；不引入多余的schema3。旧Mod不能重建新DATA时应升级，不能删字段降级。
5. 每事务10000格、每批32格、每轴64格和240连续无进度等待限制没有扩大。默认不覆盖；Client权限拒绝保留。门自动上半格纳入事务预算/高度、预占和写前快照；为保证同步关联恢复，已有门和非空气门落点即使开清障也拒绝。
6. 撤销和回滚改为32格分批；首次恢复失败立即停止，保持逆依赖顺序，逐帧继续，不先移除尚未恢复依附块的支撑。界面/工作台在恢复进行中拒绝交叉施工。

逐类型能力与精确合法DATA数组见 [机器矩阵](blueprint-block-capabilities.json)；AI生成规则与可读矩阵见 [方块参考](blueprint-block-reference.md)；165个可放置类型的最新分类见 [审计](blueprint-block-audit-x25.01.17.md)。

## 目标证据与复现

证据仅是开发者本机 `E:\SCNET\SCNETx25.01.17` 公开Windows包，程序集版本2.4.0.0、SCNET标记2.4.10.6/API1.42、目标net472。Windows SHA-256：

| 文件 | SHA-256 |
|---|---|
| Survivalcraft.exe | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` |
| Engine.dll | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` |
| EntitySystem.dll | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` |

先运行环境脚本固定三个程序集哈希，再使用本机ilspycmd 9.1.0.7988逐类型查看下面的方法；反编译代码仅存ignored的 `artifacts/evidence/`，不提交游戏代码/专有资源。

```powershell
ilspycmd --disable-updatecheck -t Game.PaintedCubeBlock E:\SCNET\SCNETx25.01.17\Survivalcraft.exe
# 同样逐类型检查本节列出的Block和Subsystem*Behavior。
```

| 类/方法 | DATA/几何结论 | 行为证据 |
|---|---|---|
| PaintedCubeBlock.GetColor/SetColor | flag=1，颜色位1..4；合法0或1+2*c | 九种实际继承者由审计/目标测试交叉核对，其他cube不得借用此状态 |
| StairsBlock.GetRotation/GetIsUpsideDown/GetCornerType/GetColor/SetColor | geometry0..23，flag32，颜色位6..9 | Initialize只有24种碰撞几何；角形镜像0↔1、2↔3由实际碰撞包围盒推导，普通楼梯是0/2不变、1↔3 |
| SlabBlock.GetIsTop/GetColor/SetColor | top位0，flag2，颜色位2..5 | 颜色与上下半砖独立保留 |
| FenceBlock.GetVariant/GetColor/SetColor | 四连接位0..3，flag16，颜色位5..8 | SubsystemFenceBlockBehavior.SetVariant只改连接，颜色持久；连接会按邻接重算 |
| WoodBlock.GetCutFace/SetCutFace | 0→face4(Y)，1→face0(Z)，2→face1(X)；3是只读别名 | CellFace真实向量验证横轴旋转1↔2，镜像轴不变 |
| WindowBlock / FramedGlassBlock | data0 | 真实继承AlphaTestCube；前者只声明Index，后者只改同类相邻面的生成，无新DATA状态 |
| LadderBlock.GetFace/SetFace | data0..3 | OnNeighborBlockChanged检查 `position-FaceVector` 的相应面IsFaceTransparent；透明即DestroyCell |
| TorchBlock.GetPlacementValue | data0..4；天花板face5返回无放置 | OnNeighborBlockChanged检查背后IsCollidable；OnAdded/Removed/Modified配对管理火焰粒子 |
| TrapdoorBlock（83/84） | rotation低2位、open位2、upsideDown位3 | 支撑为背面OR上下OR背面斜邻的不透明块；同时创建TrapDoorElectricElement |
| FenceGateBlock（166/194） | 低4位方向/开合/handed，paint与Fence相同；GetRightHanded为bit8==0 | 创建原生电路节点，可被电路异步改变；不能只凭terrain DATA保证完整撤销 |
| CarpetBlock（208） | 颜色0..15 | 下方IsTransparent则删除；OnPoll在降雨条件下25%概率无掉落删除 |
| IvyBlock（197） | 面0..3 | 正向FaceVector支撑或上方同面藤蔓；OnPoll排队向下生长，Update按60秒事件处理，递归支撑删除 |

所有公式均经目标程序集真实静态方法交叉检查。颜色编码API拒绝-1/16及未知几何；`null`未染色与索引0不混淆。游戏setter会掩码颜色的事实不能作为输入截断的理由。

## 施工与事务实现

- `VanillaBlockCapability`包含逐类型合法DATA、绘制状态和操作能力；运行时注册还核对真实CLR基类及PaintedCube资格，不能把Window/FramedGlass误当可染色。
- 导入、复制、规划、运行时palette使用同一能力校验。`BlueprintCopier`在进入cube简写分支前保留非零DATA；颜色不再被漏掉。未知tag、非字符串tag、非法高位及类型/族冲突均硬拒绝。
- 依赖图采用稳定的Kahn拓扑排序。即使梯子在同Y层坐标排序先于墙，也先建支撑；外部支撑必须已加载并满足真实支撑接口。执行每格前再检查支撑，完成后至少跨一个GuiUpdate再按批复核。实际原生事件的调用先后仍需真机确认。
- 新的可选 `IConstructionTerrainGateway`把目标引擎支撑判断与实际修改日志留在适配层，纯Core测试仍不引用游戏库。
- GameTerrainGateway写前抓取本格和已知同步门关联格原值；ChangeCell回调抛异常也在finally返回实际变更日志，BuildTask先记录日志再进入失败恢复。实际写入回读可检测领地/Hook拒绝。原生OnBlockAdded同步派生楼梯/栅栏几何时允许派生几何变化，但颜色必须一致。忽略光照位的值比较避免照明更新阻断撤销。
- 部分撤销保留整个逆序后缀，下一批从失败处继续；TransactionHistory返回真实恢复数量，WorldEditRuntime逐帧推进恢复并锁定交叉操作。单次失败中的前向写入与即时恢复合计也不能超过32格。

**事务范围限制必须保留：** 日志只覆盖本格和已取证的同步门关联格，不是全局游戏事件日志。异步的邻接楼梯/栅栏更新、掉落物、叶片腐坏、电路节点/队列、流体、天气和藤蔓队列未全部捕获。不能据此保证任意环境完整撤销。矩阵将楼梯/栅栏/门的一般terrain完整恢复标为不支持；新梯子/火把的受控fixture地形恢复已确认，外部拆支撑产生的掉落物完整恢复仍不支持。后续玩家编辑保持优先，不为“回滚成功”覆盖玩家修改。

## 实际验证命令与结果

在本工作树执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:\SCNET\SCNETx25.01.17
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Logic.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-BuilderAssistantStates.ps1 -GameDir E:\SCNET\SCNETx25.01.17
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Audit-BuilderAssistantBlocks.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -OutputPath docs/blueprint-block-audit-x25.01.17.md
dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --capabilities docs/blueprint-block-capabilities.json
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-XdbGuards.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release
```

| 验证项 | 结果 | 状态 |
|---|---|---|
| 依赖、程序集版本、三个SHA-256、net472/API1.42 ABI | 通过 | 已确认 |
| 不引用游戏的Core/生成器/工作台/事务逻辑 | 659/659检查通过 | 已确认 |
| net472程序直接调用目标真实静态API，codec颜色编码/解码、轴向/面向量、63类型注册资格 | 1909/1909检查通过，0警告0错误；不启动游戏/加载世界 | 已确认 |
| 165类方块审计重生成 | 63开放，六种被调查细节类型继续拒绝 | 已确认 |
| 原有独立堡垒白模Engine导入守卫 | 通过（原资源未修改） | 已确认 |
| XDB安全测试 | 7/7通过 | 已确认 |
| Mod net472 Release与包内容守卫 | 编译通过，warnings-as-errors；必需根条目、唯一CSV类名、XDB合并、无专有DLL通过 | 已确认 |
| Android7723/完整Windows游戏/多人 | 未运行 | 待验证 |

关键回归覆盖：逐类型穷举规范DATA解码→复制材料cell→JSON→解码等值；四次90度还原、两次镜像还原及MRM=R^-1；颜色越界拒绝；活板门与栅栏门研究codec方向还原；同层逆坐标支撑施工；无支撑、施工中支撑失效、完成后依附消失；写前拒绝/回调修改后失败的关联半格恢复；恢复失败停止/重试及玩家编辑保护；旧schema1/2；自动门半格不得突破10000上限。

4419格夹具实际导入为21×11×21内的4419个cell，139批建成；故意请求32000的调用仍最多写32格。撤销同样139批。大型任务的中途写入失败、取消和分批回滚还原全世界fixture，单次调用写入不超过32格。

产物：`artifacts/BuilderAssistant.netmod`，版本0.18.0。构建日志：`artifacts/build.log`；另有environment、target、logic、xdb日志（ignored）。最终包哈希和构建来源见报告末尾的交付记录。

## Android 7723 实机清单（全部待验证）

请使用新建单人创造世界和可丢弃备份：

1. 从7723实际包核对程序集名称/版本/哈希/API，不继承Windows包结论；安装netmod、检查初始化无异常。
2. 九种Cube、楼梯、半砖、栅栏分别放置未染色和16色，复制/导出/导入，旋转4次、镜像2次，检查角形和上下状态；Window/FramedGlass保持data0。
3. 五种原木逐一检查Y/Z/X轴端面；重进世界后再复制比较DATA。
4. 两种梯子四面、火把四壁和地面：先墙后梯/灯、攀爬/碰撞/粒子/光照，跨区块支撑，建成后拆支撑，取消和部分失败恢复；掉落物不能声称完整撤销。
5. 4419格导入，确认逐帧预算、内存/帧耗时、取消/回滚/逐帧撤销；恢复未完成时建造与工作台应阻止交叉操作。
6. 不同手机分辨率与纯触屏可达性；建造、恢复进度文字不得遮挡原生控件。
7. 单人、房主、普通客户端分别记录；普通客户端应拒绝主动写入，房主放置/颜色/撤销在所有端同步与重连后存读档尚未验证。联机还须遵守原生Mod握手的一致性要求。

## 下一阶段家具与复杂方块的准入条件

- 固定Android实际程序集证据，核验 `FurnitureBlock` 的设计索引/设计库、`SubsystemBlockEntities` 与组件库存/文本保存接口；先明确真正的持久payload，不能复用其他端口DLL/编号/结论。
- 建立有边界、预捕获、可检测越界因果影响的事务作用域；覆盖同步及延迟事件、实体/库存创建删除、拾取物、电路节点/拓扑/排队状态、世界卸载/重进和恢复失败；不能靠只读terrain快照冒充完整事务。
- 仍不扩大10000/32/64限制。超出作用域拒绝开始；取消和失败后必须验证完整状态恢复与后续玩家编辑保护。
- 在可丢弃单人世界与房主/客户端各通过放置→复制→变换→导出→导入→存读档→失败恢复→撤销后，才能逐类开放白名单。
- 家具设计/实体payload若不能由现有d表达，再设计schema3的明确state对象及1/2升级读取策略；不在本轮提前制造不受支持的payload。

## 交付记录

- 分支：`codex/builder-detail-states`，基于main `349db87`。
- 最后一次Release打包与检查：659/659离线、1909/1909目标静态API、XDB7/7通过；net472 Mod构建通过。
- 包：`artifacts/BuilderAssistant.netmod`，2,382,263字节，根DLL与最终构建DLL逐字节一致。
- SHA-256：`4067C69A35B8E36D01909D8EE3C974C829EC607D47445C5574FFC80084C4FFF6`。该哈希对应提交前以本分支最终源代码打包的文件，ZIP时间戳会使再次打包的哈希不同。
- 源码/文档纳入Git，artifacts、bin/obj、反编译证据及游戏资源不纳入Git。
- 最新参考文档、逐类型机器矩阵、165类型审计的类型集合/能力列已交叉核对为63/63；包版本0.18.0、无专有DLL。
- Android/7723、完整Windows游戏及多人均为待验证；未通过扩大白名单宣称完整跨子系统恢复。
