> 主线整合版本为 v0.19.0，保留 main v0.18.0 精细状态与依附施工能力；以下 v0.16.0 为原 port 分支实现与取证记录。

## v0.19.0 main 整合与验证（2026-10-28）

仅移植家具功能，不合并 port 分支的 SCIE-Port 历史。主线已有 63 种材料能力、颜色、
精细 DATA、梯子/火把支撑、门关联格及反向依赖分批恢复均保留。PlannedChange 同时保留
Family/Data 和家具 Context，依附拓扑排序后保持“全部结构（含梯子/火把）完成，再建家具”；
结构依附块若需要后建家具支撑则在规划时拒绝，避免跨阶段倒置。
撤销/回滚遵循 main 每批≤32、恢复失败即停的更严格语义，286 格示例需多批撤销完成。

实际执行 `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-NetMod.ps1`：
环境和固定哈希 ABI 检查通过，**2061/2061 离线检查通过**，**1909/1909 目标静态 API 检查通过**，
Portable Fortress 原有 Engine 导入守卫通过（1 bone / 11 meshes / 1116 triangles），
net472 Release 与包入口/资源/XDB/专有程序集排除守卫通过，0 警告、0 错误。
另执行 `scripts/Test-FurnitureAbi.ps1`，关键家具签名/固定哈希检查通过。
机器可读能力表经测试工具 `--capabilities docs/blueprint-block-capabilities.json` 重新导出，
版本 0.19.0，保留 63 种类型并加入十家具模板元数据。

最终 `artifacts/BuilderAssistant.netmod` 为 **2,387,457 字节**，SHA-256：
`1EA13F8A19655E313C07880AD15AF6F876A1B6A7A240E09E969871D6616E1CD9`。
这仍是 Windows 编译/离线和静态验证；Android 7723 与多人运行均待验证。

# Builder Assistant v0.16.0 家具模板

日期：2026-10-28。目标 SCNET x25.01.17 / Android 7723。

## 结论与边界

统一模板库、蓝图 schema 3、模板展开、顺序施工和共享撤销已经实现并通过离线测试。
**原生铁锤家具后端当前不支持；第一批模板采用结构块降级。** 这解决 AI 按名称调用布局和
事务安全问题，但还不能提供原生铁锤家具的微体素精细度。Android 7723 功能与多人同步均待验证。

## 本机程序集取证

只读来源为 `E:\SCNET\SCNETx25.01.17`，不提交游戏文件或反编译源码。
`Survivalcraft.exe` 2.4.0.0 / `.NETFramework,Version=v4.7.2`，SHA-256：
`3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`。
另外两个引用的哈希见 `COMPATIBILITY.md`；它们与已登记 Windows 发布包相符。

检查方法：`ilspycmd -t Game.FurnitureBlock`、`Game.FurnitureDesign`、
`Game.SubsystemFurnitureBlockBehavior`，参数末尾均为本机 `Survivalcraft.exe`。
仅将反编译输出保留在忽略的 `artifacts/furniture-audit/`；
`scripts/Test-FurnitureAbi.ps1` 用 ReflectionOnly 元数据重新检查关键签名，不执行游戏初始化。

| 取证对象 | 已确认的 Windows 实现 |
| --- | --- |
| FurnitureBlock | 继承 Block，实现 IPaintableBlock、IElectricElementBlock；Index=227。地形里保存一个普通 cell value，但其含义依赖世界设计表。 |
| DATA | 低 2 位为 rotation；设计索引低 10 位存于 DATA 2–11 位，高位存于 15 位以上；12–13 位为阴影强度、14 位为光源标记。`GetDesignIndex(data)=(data>>15<<10)\|((data>>2)&0x3FF)`，不能套用普通 cube 的 DATA=0。 |
| FurnitureDesign | 世界内 Index、Name、Resolution、体素 values、LinkedDesign、InteractionMode、FurnitureSet，以及几何/碰撞/交互/光源等缓存。`SetValues(int,int[])` 要求 resolution 2..256 且数组长度为 resolution³；这个引擎上限不是本 Mod 已验证的安全预算。 |
| 世界注册 | `SubsystemFurnitureBlockBehavior.m_furnitureDesigns` 为 65535 槽数组；`TryAddDesign` 先去重再分配，必要时 GC，满时返回 null。Index 不是可移植的模板 id。 |
| 持久化 | `FurnitureDesign.Save()` 返回 `TemplatesDatabase.ValuesDictionary`，记录 Name、TerrainUseCount、Resolution、InteractionMode、LinkedDesign 和 RLE Values。子系统单独 Save/Load 设计表与 FurnitureSet，并非只保存地形整数。 |
| 生命周期 | OnBlockAdded/Removed/Modified 等维护地形引用数；GC 还检查物品扫描结果、设计链、集合归属。撤销记录中保留的整数不是 GC 的设计保活引用。 |
| 状态与网络 | 可创建多状态/按钮/开关电路元素，交互能切换 LinkedDesign。Server 的 GC 会发送 FurniturePackage，Client 不执行该 GC；这不构成已经验证的跨端注册/施工/撤销协议。 |

静态家具本身通常不是一个独立的 EntitySystem 实体；它仍然需要额外的世界级设计对象，
交互家具还涉及电路状态。不能将“没有独立实体”推导为“可像石块一样复制整数”。

现有 Builder Assistant 不能直接支持的具体原因：

1. `VanillaBlockPalette.SupportedBlocks` 无 FurnitureBlock，复制会跳过，导入材料预检会拒绝。
2. `BlockFamily` 没有家具族，普通 Cube 的 Normalize 会清掉设计索引/朝向等 DATA。
3. `BlueprintCell` 原先只存材料键与 authored DATA，没有体素设计和世界注册依赖。
4. `BlockUndoRecord` 只保留 OldValue/WrittenValue，不能注册、保活、回滚或重映射世界设计表。
5. 覆盖已有原生家具后，即使留有旧 cell value，设计仍可能在 GC 后不可用。故不能以整数撤销
   推断原生设计撤销安全；本版不新增这种行为，也不声称修复历史覆盖任意复杂方块的全部状态问题。

本轮未证明“原生机制永远不可实现”。要启用后端，必须另行完成：确定性设计数据与预算、
按世界分配/去重、设计表满的无副作用处理、撤销历史引用保活、失败注册补偿、保存重载、
设计链/电路状态边界、主机权威同步及 7723 程序集指纹/真机验证。不能直接把模板 id 当 designIndex，
也不自动把同一个现有模板 id 改为微体素后端，以免旧蓝图尺寸或外观语义改变。

## 实现与施工

`Core/Furniture/` 包含 FurnitureTemplateRegistry、FurnitureTemplate、FurniturePlacement、
FurniturePlacementResolver。注册表按精确 id 查找并拒绝重复；模板是只读的已支持块形状，
元数据含名称、尺寸、BoundsMin/BoundsMax、四向支持与 backend=`structure-blocks-v1`。

`Blueprint.Cells` 是供现有预览、材料预检、BuildPlanner 和 BuildTask 使用的展开序列；
`StructureCells` 和 `FurniturePlacements` 保留作者数据供导出及旋转/镜像重组。
展开顺序固定为结构 cells 原顺序，然后 placement 数组顺序，每件家具内部按 Y/X/Z 排序。
不对合并结果全局排序，保证先结构后家具。整份蓝图以联合包围盒最小角归一化。

模板完整包围盒参与内部冲突检查，连同原生门隐式上半格一起检查。
包围盒空隙做规划期世界净空检查，不写空气、不扩大实际清障范围；执行时每个写入格仍按原规则
重新检查。清障模式仅放宽世界障碍，不能允许蓝图内部重叠。新放入空隙而不在写入点的物体
不会被模板覆盖；本版不提供跨帧锁定整个家具空间的机制。

无需另起 FurnitureBuildTask：结构与家具均交给现有单个 BuildTask，避免两笔事务之间出现
结构成功但家具失败无法整体撤销的问题。保留 10000 格总限额、64 格单轴限额、32 格每批、
240 次连续零进度停等、权限闸、写入前读原值、失败/取消回滚、延迟恢复以及保护玩家后续修改。
给 PlannedChange 增加家具来源上下文，使执行失败消息包含模板 id 和相对锚点。
回滚/撤销沿用旧执行器行为，本轮没有重写旧执行器或扩大其恢复预算承诺。

## JSON 与首批模板

规范、各模板尺寸/格数和方向表以 [blueprint-block-reference.md](blueprint-block-reference.md)
为准。提供可直接导入的 [书屋全十模板示例](examples/bookhouse-furniture-v3.json)：
12×16 的木板地面加上书架、桌椅、床柜与灯架，共 286 个实际写入格。

schema 1/2 继续读取；纯 cells 导出保持 schema 2，有模板则导出 schema 3。
schema 3 必须保留 cells 数组，但可为 []；另有非空家具时可单独导入家具布局。
`templateId`、整数 x/y/z、rotation 必填，rotation=0..3。位置是旋转后包围盒最小角。
可选 `mirrorX`（bool，旋转后镜像）、`color`（仅 natural）、`variant`（仅 default）。
未知 id、错误类型、分数朝向、非法颜色/变体、越界/超量/内部冲突均拒绝整份导入。
不接受家具 props、原生 designIndex 或原始 FurnitureBlock 单元。

第一批精确 id：TallBookshelf_A、WideBookshelf_B、CornerBookshelf_C、ReadingDesk_A、
WritingDesk_B、WoodChair_A、Bench_A、Cabinet_A、Bed_A、LampStand_A。
全为装饰：砖块表示书，柜不储物、床不睡觉、椅不坐人、灯不发光。没有新增美术资源、
Block ID、XDB、CSV、Hook 或网络包；也未引用 SCIE-Port 源码或资源。

## 文件变更

- 新增 `Core/Furniture/` 四类、`scripts/Test-FurnitureAbi.ps1`、`tests/.../FurnitureTests.cs`、本说明与示例。
- `Core/Blueprints/Blueprint.cs`：展开视图、来源元数据、保留家具引用的旋转/镜像。
- `Core/Blueprints/BlueprintJson.cs`：schema 3 解码/校验/导出，保留 v1/v2。
- `Core/BuildEngine/BuildPlanner.cs`：家具净空预检与来源标记；`BuildPlan.cs` / `BuildTask.cs`：失败上下文。
- `Features/WorldEdit/WorldEditRuntime.cs`：导入提示显示模板件数与结构块降级。
- 项目/元数据升级 0.16.0，测试工程与入口接入新测试；README、兼容性和 AI 方块清单更新。

## 验证记录

Windows 本机实际命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-FurnitureAbi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-NetMod.ps1
```

GameDir 使用脚本默认本机路径，亦可显式传 `-GameDir` / 使用 SCNET_GAME_DIR。
Build-NetMod 会执行环境与现有 ABI 检查、全部离线测试、真实引用 net472 Release 构建和
包入口/资源/专有 DLL 排除/方块类名唯一性/XDB 合并守卫。最终结果见兼容性本次记录。
测试包括十模板注册/查找/重复拒绝、四朝向及组合旋转镜像、JSON 往返、结构/家具/门冲突、
世界障碍/净空、共享总数量和轴/高度限制、32 格分批、先结构后家具、取消/故障回滚、
延迟回滚/撤销重试、玩家后续改动保护和实际 AI 示例经导入入口后的完整施工。

## Android 7723 待验证

仅在新建单人世界和可丢弃备份测试，先比对渠道程序集名称、版本与哈希/API。

1. 安装 0.16.0、903 通过剪贴板/蓝图库导入示例，901 两次触屏确认，检查降级提示与小屏可读性。
2. 结构先完成、模板再执行，四方向、非方形模板、全局旋转/镜像与局部锚点是否一致。
3. 门附近和桌下障碍拒绝；清障 OFF/ON、软障碍及非写入空隙范围是否与文档一致。
4. 每批 32 格的帧耗时、取消中止、902 整体撤销；移动导致区块未就绪后的延迟回滚/撤销。
5. 保存退出重进后结构块外观和碰撞正常；书架砖块替代、床/灯/柜无原生功能是否可接受。
6. 单人、房主、普通客户端分别记录：房主写入与撤销同步待验证；普通客户端必须继续拒绝主动写入。
7. 这不验证原生 FurnitureDesign 注册、保存、GC 或网络；原生后端须另立测试记录。
