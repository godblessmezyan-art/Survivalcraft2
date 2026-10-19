# 兼容性矩阵

公开证据：

- [SCNET x25.01.17 发布页](https://github.com/survivalcraft-net/survivalcraft-net/releases/tag/x25.01.17)
- [GitHub Release API 元数据](https://api.github.com/repos/survivalcraft-net/survivalcraft-net/releases/tags/x25.01.17)（列出 Windows ZIP、Android APK、大小与 SHA-256）

状态定义：

- **已确认**：已从目标程序集或实际运行测试得到证据。
- **待验证**：有线索或已编译，但尚无目标 Android 客户端实测。
- **不支持**：加载机制不同，当前工程明确不产出对应格式。

## 运行环境矩阵

| 环境 | 包格式/目录 | 编译 | 加载 | 功能 | 证据与备注 |
| --- | --- | --- | --- | --- | --- |
| SCNET x25.01.17 Windows 公开包 | `.netmod` / `NetMods` | 已确认 | 待运行验证 | 仅加载日志 | 本地程序集检查与 Release 构建通过 |
| SCNET x25.01.17 Android 公开 APK | `.netmod` / 待设备确认 | 待验证 | 待验证 | 待验证 | 发布页确认有同标签 APK，但尚未提取/比对程序集 |
| SCNET x25.01.17 Android 7723 包 | `.netmod` / `NetMods` | Windows 引用构建已确认 | **已确认（最小 Mod）** | 仅初始化日志 | 2026-10-16 用户实机确认安装和加载；渠道程序集哈希仍待比对 |
| 其他 SCNET 2.4 版本 | `.netmod` | 待验证 | 待验证 | 待验证 | 不凭 `2.4` 主版本号推断兼容 |
| SCAPI 1.x | `.scmod` / `Mods` | 不支持 | 不支持 | 不支持 | 当前项目不构建 SCAPI 产物 |
| 官方原版 Survivalcraft | 无已确认加载器 | 不支持 | 不支持 | 不支持 | 未确认存在 SCNET ModLoader |

## x25.01.17 已验证 ABI

检查来源：本机 `D:\SCNET`，来自公开 x25.01.17 Windows 发布包。

| 项目 | 结论 | 状态 |
| --- | --- | --- |
| `Survivalcraft.exe` 程序集版本 | `2.4.0.0` | 已确认 |
| TargetFrameworkAttribute | `.NETFramework,Version=v4.7.2` | 已确认 |
| SCNET 内部 API 版本字符串 | `1.42` | 已确认（程序集字符串与日志模板） |
| SCNET 版本字符串 | `2.4.10.6` | 已确认（程序集字符串） |
| Mod 入口基类 | `Game.ModLoader` | 已确认 |
| 初始化入口 | `public virtual void __ModInitialize()` | 已确认 |
| 管理器 | 全局命名空间 `ModsManager` | 已确认 |
| Hook 注册 | `ModsManager.RegisterHook(string, Game.ModLoader)` | 已确认 |
| Mod 实体 | `Game.ModEntity` | 已确认 |
| 元数据类型 | `Game.ModInfo`，`Dependencies` 为 `List<string>` | 已确认 |
| 日志接口 | `Engine.Log.Information(string)` 等 | 已确认 |
| 目标目录 | Windows 包内 `NetMods` | 已确认 |
| 包结构 | ZIP 容器，根目录包含 `modinfo.json` 与 Mod DLL | 待 Android 实机验证 |
| 原生船组件 | `BoatBlock`、`ComponentBoat`、`ComponentMount`、`ComponentRider` | 已确认 |
| 原生触屏船控制 | `ComponentInput.PlayerInput.Move` -> `ComponentBoat.MoveOrder/TurnOrder` | 已确认 |
| GUI 更新 Hook | `GuiUpdate(ComponentGui)` / `ModsManager.RegisterHook("GuiUpdate", ...)` | 已确认 |
| 骑乘网络包 | `Game.NetWork.Packages.ComponentMountPackage` | 已确认存在；自定义载具行为待验证 |
| 骑乘包座位信息 | 仅 Rider 实体 ID、Mount 实体 ID 和 Mount/Dismount/Request 类型；无座位号，原生双座不支持 | 已确认 |
| Mount 乘员模型 | `ComponentMount.Rider` 返回船体第一个 Rider 子 Body；`ComponentRider.Mount` 从直接父 Body 实体取 Mount | 已确认 |
| 骑乘船体同步 | `ComponentPlayerPackage.BodyUpdate` 在骑乘时同步骑手的直接父 Body；`ComponentBody` 使用 `ChildBodies[0]` 参与权威判定；双座冲突待重写/实测 | 已确认 |
| 快艇 ABI 自动检查 | `scripts/Test-SpeedboatAbi.ps1` 验证 Boat/Mount/Rider/Body/生成与网络包公开签名；限上述 Windows 程序集 | 已确认 |
| 自定义方块注册 | Mod DLL 内 `Block` 子类 + 公开静态 `Index` | 已确认（反编译）；v0.3 Android 待验证 |
| 方块数据合并 | `.netmod` 内任意 `.csv` 由 `BlocksManager.LoadBlocksData` 读取 | 已确认（反编译）；v0.3 Android 待验证 |
| 实体数据库合并 | `.netmod` 内任意 `.xdb` 由 `ModsManager.CombineDataBase` 按 GUID 合并 | 已确认（反编译）；v0.3 Android 待验证 |
| Mod 资源加载 | `Assets/` 下资源加入 `ContentManager`；路径必须使用 `/` | 已确认（反编译与包检查）；Android 待验证 |

## v0.2.0 快艇原型兼容性

| 能力 | Windows 编译 | Android/7723 | 联机 |
| --- | --- | --- | --- |
| 识别原生船和骑乘状态 | 已确认 | 待验证 | 待验证 |
| 复用原生触屏移动输入 | 已确认接口 | 待验证 | 不启用 |
| 有上限的额外推进 | 已编译 | 待验证 | 主动禁用 |
| 独立快艇物品/模型 | 未实现 | 未实现 | 未实现 |

当前原型只在 `WorkType.Local` 下修改船体速度。远程客户端会显示禁用提示，避免未经确认的客户端物理写入造成回弹、不同步或反作弊问题。

## v0.3.0 独立快艇兼容性

| 能力 | Windows 构建/静态检查 | Android/7723 | 联机 |
| --- | --- | --- | --- |
| 方块编号 900 与创造栏条目 | 已确认编号未被原版 0–263 使用；已打包 CSV | 待验证 | 待验证冲突 |
| `Speedboat` 实体模板与生成子系统 | XDB 结构、GUID 和类签名已检查 | 待验证 | 主动禁用生成 |
| 自定义 DAE 模型与 PNG 贴图 | XML/三角形索引/PNG 头已静态检查 | 待渲染验证 | 不适用 |
| 原生触屏乘坐、转向和快艇推进 | 已编译；仅按实体模板名启用 | 待验证 | 主动禁用推进 |

编号 `900` 在目标原版程序集内空闲，但 SCNET 没有全局第三方 Mod 编号注册表，因此与其他 Mod 的冲突仍属于待验证项。

## v0.4.0 快艇速度与联机测试

| 能力 | Windows 构建/静态检查 | Android/7723 | 联机 |
| --- | --- | --- | --- |
| 用户贴图（1254×1254 PNG） | 已打包且包内哈希一致 | 待渲染验证 | 不适用 |
| 额外推进 `9`、水平上限 `18` | Release 编译已确认 | 待验证 | 待验证 |
| 快艇物品联机生成 | 已移除 `WorkType.Local` 限制 | 待验证 | 待主机/客户端同步验证 |
| 联机客户端持续推进 | 已移除软件禁用并保留硬上限 | 待验证 | 服务器回弹与权威边界待验证 |

“已允许联机执行”不代表“已确认联机同步”。若服务器不运行相同 Mod 或服务器保持权威物理状态，客户端速度仍可能被纠正。

## 双座快艇可行性结论（2026-10-17）

| 能力 | Windows x25.01.17 ABI | Android/7723 | 联机 |
| --- | --- | --- | --- |
| 同一船体声明两个原生 Mount | 不支持：原生协议和 Rider 查找无座位号 | 不支持 | 不支持 |
| 首位 Rider 作为主驾驶 | 字段与行为已确认；额外推进已有首位 Rider 保护 | 待验证 | 待主机/客户端验证 |
| 副驾驶座锚实体 | 所需 Body/Mount/父子引用签名存在 | 待实现与验证 | 实体顺序、同步和断线清理待验证 |
| 自定义输入/座位网络包 | `IPackage` 扩展线索存在 | 待验证 | 包 ID、双方 Mod 一致性和服务端校验待验证 |

完整接口对照、许可证边界、模型 UV 结论和分阶段方案见 `docs/SPEEDBOAT-RESEARCH.md`。

已检查文件 SHA-256：

- `Survivalcraft.exe`: `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`
- `Engine.dll`: `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`
- `EntitySystem.dll`: `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`

这些哈希只描述当前已检查的 Windows 文件，不能代替 7723 Android 包的验证。

## SCIE-Port v0.7.0 静态工业家具（独立工程）

此结论只适用于 `SCIE-Port/`，不得替代 Builder Assistant 的测试证据。固定 SCIE commit
`4864d956793b6e670413019f1f6b1cc5c8bb0970` 中的钢齿轮、钢轮、齿轮模具、车轮模具、工业活塞、
钢瓶六个模型已作为独立 Block ID 917–922 接入。真实 x25.01.17 Windows 程序集环境检查、net472
Release 0 warning/0 error、模型 Y_UP/节点/顶点和 `.netmod` 包内容为已确认；Windows 游戏运行、
Android/7723 渲染与保存重载、多人地形同步均为待验证。机器、配方、库存、气体和化学功能不支持。

### SCIE-Port v0.7.1 Kelly 图集修复

用户 Android 日志已确认 v0.7.0 在创造栏绘制 Kelly 果实图标时，`ContentManager` 无法把包内
`KellyExpansion.webp` 解析为 `Texture2D`，从而退出世界。v0.7.1 改为只在运行包中携带同像素
转换的 512×512 RGBA PNG，并由打包守卫拒绝 WebP 回归。该缺陷原因与文件/打包修复为已确认；
Android 翻到植物第三页及后续页面是否不再退出仍为待验证。

### SCIE-Port v0.7.2 载具世界渲染修复

用户反馈飞艇和火车在物品栏颜色正常、生成进三维世界后变黑。已确认飞艇存在确定的加载顺序问题：
自定义驱动组件写入运行时白贴图后，`ComponentSimpleModel.Load` 仍可按空的 XDB 值覆盖贴图。
v0.7.2 将飞艇改为 XDB 明确引用包内白色 PNG，并为蒸汽船、飞艇、火车和车厢设定低强度保底自发光，
避免世界地形光照为零时成为纯黑。目标程序集编译、XDB 与包资源引用检查均已确认；Android 实机视觉结果待验证。

### SCIE-Port v0.8.1 陆地 ATV 预览

此项仅在 `SCIE-Port/` 独立工作树 `codex/atv-land-vehicle` 中开发，不改变 Builder Assistant。
新增独立 `LandVehicles` 模块、项目创建的低多边形四轮 `ATV.dae`、部署物品 ID `923`、实体
`SCIEPortATV` 及单座原生 Mount/Body/Spawn 模板。固定 x25.01.17 Windows 程序集的反射、反编译和
net472 Release 编译确认：`ComponentBody` 提供地形/body 碰撞、重力、保存的 Velocity/ParentBody、
接地状态及 `MaxSmoothRiseHeight`；ATV 仅在单人 `WorkType.Local`、接地且未浸水时读取触屏 `Move`，
限制上台阶高度为 0.35 格，支持前进、后退、转向和反向刹车。它不套用船的浮力物理、不写地形，也不套用
火车的轨道吸附。

已通过 13 项纯规则测试、XDB 合并验证、固定程序集环境检查、net472 Release 0 warning/0 error 和
`.netmod` 包资源守卫。**未启动 Windows 游戏、未在 Android 7723 实测、未进行多人联机测试**；因此
实体生成、原生上/下车与第三人称、碰撞/台阶手感、模型渲染、保存退出重进及所有联机行为均为待验证。
本阶段明确不支持燃料、多人与服务器权威输入同步、复杂悬挂。

### SCIE-Port v0.8.2 / ATV v0.2 稳定性修复

保持 Block ID `923` 和实体名 `SCIEPortATV`。已确认 v0.1 同时受到自定义滑行/抓地和原生
`ComponentBody` 接地 `6*dt` 阻力，导致速度建立受抑；v0.2 在驾驶时关闭原生接地阻力、无人时恢复，
最高前进速度仍为 11，没有通过提高限速掩盖问题。短暂离地不再强制清零水平速度。

原项目自有模型已重建为九个独立 Y_UP 网格，使用 identity 节点矩阵、完整法线/UV 和不透明四色 PNG；
物品渲染删除重复反向面。`ComponentBody` 的真实 `HandleSmoothRise` 以 `4.5*dt` 连续抬升并继续检查
地形、实体和顶部净空，故设置 `MaxSmoothRiseHeight=1.05` 支持完整一格而无需瞬移。Release 构建与
静态资源结果为已确认；Windows 游戏、Android 7723 渲染/驾驶/一格越障/保存重载仍为待验证，
多人仍不支持。

## 尚未确认的能力（Builder Assistant）

- Android 客户端是否使用与 Windows 完全相同的 `Survivalcraft` ABI。
- 7723 包内程序集是否与公开 APK 一致（最小加载成功不等于二进制完全一致）。
- 触屏 Widget 的安全挂载点与生命周期。
- 地形修改在主机、客户端和专用服务端之间的权威边界。
- `.netmod` 元数据中的版本约束是否严格比较，以及失败时的用户提示。

## v0.5.0 Builder Assistant 自动建造基础

本轮只改 Builder Assistant，不动 SCIE-Port。建造相关的游戏侧接口在 x25.01.17 真实程序集上通过 Release 编译绑定，等价于对这些成员的 ABI 取证：任何签名不符都会在编译期报 CS0117/CS1061/CS1501/CS7036，而 0.5.0 构建结果为 0 错误、0 警告。

| 能力 / 接口 | 结论 | 状态 |
| --- | --- | --- |
| 地形写入入口 `Game.SubsystemTerrain.ChangeCell(x,y,z,value[,bool][,ComponentMiner])` | 主线程、引擎自带（触发保护/网络/重网格）；已编译绑定 | 已确认（编译期 ABI） |
| 方块读回 `Game.Terrain.GetCellValue`、有效域 `IsCellValid`、按名取块 `BlocksManager.FindBlockByTypeName`、打包值 `Terrain.MakeBlockValue/ExtractContents` | 全部经编译绑定 | 已确认（编译期 ABI） |
| 区块成员 ABI `Terrain.GetChunkAtCell(x,z)`、`TerrainChunk.State`/`IsLoaded`/`Cells`/`Coords`、`Terrain.IsCellValid` | 均经编译绑定；`GetChunkAtCell` 对未生成列返回空。注意：早期把「可编辑」判据写成 `State==Valid && IsLoaded` 是**错误语义**（`IsLoaded` 是磁盘/流送态，非可写性），已于 **v0.6.2** 更正为 `State==Valid && Cells!=null`，详见下文 v0.6.2 小节 | 已确认（编译期 ABI） |
| 工具方块基类 `Game.PaintedCubeBlock(int textureSlot)` | 普通着色立方，无需朝向数据位；纹理槽 69/51 取自真实原版方块 | 已确认（编译期 ABI + 反编译） |
| 行为分发 `Game.SubsystemBlockBehavior.OnUse(Engine.Ray3, ComponentMiner)` + `HandledBlocks` | 主线程点击回调；已编译绑定 | 已确认（编译期 ABI） |
| 交互射线 `ComponentMiner.Raycast<TerrainRaycastResult>(Ray3, RaycastMode.Interaction)` -> `CellFace.X/Y/Z` | 已编译绑定 | 已确认（编译期 ABI） |
| 单人门控 `Game.NetWork.CommonLib.WorkType == WorkType.Local` | 建造/撤销/进度仅在此条件下执行 | 已确认（编译期 ABI） |
| 反馈 `ComponentGui.DisplaySmallMessage(string, Engine.Color, bool, bool)` | 主线程消息提示 | 已确认（编译期 ABI） |
| 方块编号 901/902（建造/撤销工具） | 原版 0–263 空闲；与快艇 900、SCIE-Port 编号不冲突（打包脚本校验类名唯一） | 已确认（静态/打包） |
| 数据库合并安全 `Building.xdb` 复用原版 `BlockBehaviors`(00c97f0f…) 与 `Project`(85023bf8…) GUID | 避免 SCIE-Port 曾遇到的“同名不同 GUID 生成 rogue 重复文件夹”合并异常；打包脚本强制校验 | 已确认（静态/打包） |

安全逻辑（默认不覆盖非空气、单事务 ≤1024、单批 ≤32、坐标与高度上限、区块未加载即失败、写入前抓快照、失败/取消回滚、撤销不覆盖玩家后续改动、蓝图重建确定性、旋转/镜像）由 `tests/BuilderAssistant.Tests` 以纯 C# 离线验证，共 38 项断言全通过。这些断言不依赖游戏程序集。

尚未确认（Android 实机）：

- `.netmod` 在 7723 Android 上能否进世界、方块是否以正确材质与朝向显示。
- 分批写入在真机帧率下的可见性与耗时（32/帧）。
- 建造落点选取、二次确认与撤销在触屏上的实际操作体验。
- 与 SCIE-Port 同时安装时，两者的 CSV/XDB 合并顺序与最终世界加载。

## v0.6.0 区块未就绪诊断与生存（M1）

本轮只改 Builder Assistant，不动 SCIE-Port，也不改动快艇。本机已由用户提供官方 Windows x25.01.17 客户端目录 `D:\SC`，其 `Survivalcraft.exe` / `Engine.dll` / `EntitySystem.dll` 三个程序集 SHA-256 与既有登记的公开 Windows 发布哈希完全一致，故编译期 ABI 结论可直接引用。证据严格分级如下：

1. **Core 纯逻辑（本机已离线证明）**

   新增 `Core/Transactions/CellEditability.cs`（每格不可编辑原因枚举+结构）、`ITerrainGateway.InspectCell`、`BuildPlan` 的不可编辑明细与 `AllEditabilityFailuresRecoverable`、`BuildTask` 的延迟回滚/撤销条目与 `RetryRollback`/`RetryUndo`、`TransactionHistory.UndoLast(gateway, out pending)` 的部分撤销保留。以上均由 `tests/BuilderAssistant.Tests`（net8，仅编译 Core+Generators 源码，不引用游戏程序集）离线验证，`72/72` 断言全通过；覆盖跨区块未就绪的分类与可恢复判定、越界/高度为硬性不可恢复、部分回滚被追踪并重试、部分撤销保留待续做、以及延迟撤销遇到玩家后续改动时让位不覆盖。

2. **Features 适配层（本机已用真实程序集编译 — 已确认（编译期 ABI））**

   以 `-p:SCNetGameDir='D:\SC'` 对真实 x25.01.17 程序集 Release 编译 `net472` Mod 工程，结果 **0 错误、0 警告**。`GameTerrainGateway.InspectCell` 与 `WorldEditRuntime` 的预检/延迟回滚只用 v0.5.0 已编译绑定过的 ABI 成员（`Terrain.GetChunkAtCell`、`TerrainChunk.State==Valid`、`TerrainChunk.IsLoaded`、`Terrain.IsCellValid`、`SubsystemTerrain.ChangeCell`），未引入新签名；这次编译进一步对 M1 新增调用的游戏侧类型（`chunk.State` 枚举、`IsLoaded`）做了编译期绑定确认。随后 `./scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release` 完成打包，通过全部包内容守卫（必需条目、CSV 类名唯一 `BuilderToolBlock/SpeedboatBlock/UndoToolBlock`、原版 GUID 断言、不含专有程序集）。

   - `.netmod`（v0.6.0，重新打包）SHA-256：`E454EC15254845DD816358DA3C3BF287CC0F443E7C3C6634D157185F408CCA67`
   - 本机程序集哈希（与公开 Windows 发布一致）：`Survivalcraft.exe 3D14B8D6…CC74A7AD`、`Engine.dll 98F2035B…E770694`、`EntitySystem.dll 7A87E643…B67ED19C`。
   - 注意：**Windows 编译+打包 ≠ 运行验证**。本结论仅证明这些接口在目标程序集上签名正确、可编译；Windows 游戏进程内加载、以及下面的 Android 实机仍各自待验证。

3. **Android 实机（待验证）**

   尚不能宣称该修复在手机上生效。有限等待是否真能在触屏上等到跨区块小屋的相邻区块加载、延迟回滚/撤销的重试提示是否按预期出现，均需 7723 Android 单人创造世界实测。

## v0.6.1 XDB 数据库 GUID / 合并冲突复核（发现并修复：Class 参数 GUID 重复）

**更正此前结论。** 上一节曾判定“Builder Assistant 无数据库缺陷、v0.6.0 不可能引入合并问题”——该结论**不完整且误导**：我据此复核时把 `<Parameter>` 的 GUID 排除在唯一性检查之外，误以为参数 GUID 可跨节点复用。用户回传的真机日志推翻了这一点，暴露出一个**随包发布即存在的真实缺陷**（v0.5.0 起就有，非 v0.6.0 引入）。

1. **真机首异常（用户日志，决定性）** — 加载 `[Builder Assistant] 加载.xdb数据库文件` 之后立刻：
   `WARNING: An item with the same key has already been added. Key: ea25b78e-7d8c-4998-85d3-2b1baad3ba34`
   随后连锁：`加载方块Game.EggBlock错误:Database not loaded.`、`加载方块Game.ClothingBlock错误:EntityTemplate "MalePlayer" not found.`，进世界时 `System.InvalidOperationException: Database not loaded.`（栈 `DatabaseManager.get_GameDatabase ← GameManager.LoadProject ← GameLoadingScreen`）。`ea25b78e` 正是 `Class` 参数的 GUID；数据库因此整体为 null，故连原版 EggBlock/MalePlayer 都解析不到。（日志里大量 `Unable to resolve host "m.schub.top"` 只是断网取 MOTD 失败，与本缺陷无关。）

2. **真实库交叉验证（本机 Windows 程序集，不采信记录值）** — 用游戏 `GetDecipherStream` 解 `Content.scpak` 得真实 `Assets/Database.xml`：原版里 `<Parameter Name="Class">` **每个实例都是各自唯一的 GUID**（全库 191 个各不相同），且 `ea25b78e` 在原版中**出现 0 次**——它不是共享的类型 GUID，而是 Builder Assistant 作者的复制粘贴。即：**文件夹/Project 容器可复用原版 GUID（合并时并入同一容器），但叶子 `<Parameter>` 的 GUID 必须全局唯一**；x25.01.17 加载器把所有 `Guid=` 值塞进一张字典，重复即抛“same key already added”。

3. **冲突节点与来源** — Building.xdb 把 `ea25b78e` 同时用于 `BuilderToolBlockBehavior` 与 `UndoToolBlockBehavior` 两个 Class 参数（Speedboat.xdb 还再用了第三次）。这也解释了此前“快艇阳性对照能加载”的矛盾：当初只单独加载 Speedboat.xdb（`ea25b78e` 仅 1 个实例，无冲突）；一旦装上含 Building.xdb 的完整包，同名键冲突即触发。根因属 Builder Assistant 自身数据，SCIE-Port 无关；历史 `already nested`（文件夹级）是另一类问题、与本次（参数级）不同。

4. **版本澄清** — 真机日志首行加载横幅写死 `v0.5.0`，说明用户手机上装的其实是**旧版 v0.5.0**（非 v0.6.0）；该 XDB 缺陷在 v0.5.0/v0.6.0 数据里**一字未改地共存**，所以“v0.6.0 没动 .xdb”属实、但“因此无缺陷”的推论错误。此前 `D:\SC\Bugs\Game.log` 那份“干净无异常”的日志不具备代表性——那次 `D:\SC\NetMods` 为空，根本没安装本 Mod。

**v0.6.1 最小修复**：只给 Building.xdb 的两个 `Class` 参数各换一个**新的、经确认不在原版库、彼此唯一**的 GUID（Builder=`ecf3ac44…`、Undo=`82d5d8fd…`），使 `ea25b78e` 在整个包里只剩 Speedboat.xdb 一处；**Speedboat.xdb 一字未改**（保持已验证可加载的快艇数据字节一致，不破坏快艇/建造/撤销）。同时把加载横幅改为按程序集版本动态输出（不再写死），并把版本号升到 0.6.1（`modinfo.json` / csproj / `AssemblyVersion`）。参数 GUID 不被存档引用，改动安全。

**守卫修正（关键：堵住上轮的盲区）**：`scripts/XdbMergeSafety.ps1` 的 `Test-XdbMergeSafety` 改为对**整包每一个 `Guid=` 值做全局唯一性校验**，仅豁免三个原版文件夹 GUID + Project GUID（合法跨文件复用）；从而能抓到 `<Parameter>` 重复 GUID。`scripts/Test-XdbGuards.ps1` 扩到 **7 项**并新增“跨文件重复 `<Parameter>` GUID 被抓”用例，全部通过；重新对真实库交叉验证 12 个原版依赖 GUID 存在且名匹配、BA 自定义 GUID 纯增量不覆盖。

**边界**：以上为“桌面结构 + 真实库交叉验证 + 修复随包产物内容核对”层面确认（重打包 `.netmod` v0.6.1 SHA-256 `C4B022D3566E4066AFB2EA15ED541D8AC68B4829903D593F207C3B3E3FF60C30`，随包 Building.xdb 已无重复 GUID、`ea25b78e` 全包仅 1 次、modinfo Version=0.6.1）。**仍不等于手机实机验证**——需把新包装上 7723 真机、确认进世界不再报 `Database not loaded`、建造/撤销/快艇均正常；若仍有报错，请回传读条阶段第一条 `Warning(...)` 原文 + 完整日志。

## v0.6.2 区块就绪误判修复（真因：把 `IsLoaded` 当成可写性把关）

装上 v0.6.1 后数据库崩溃消失、建造逻辑已跑到“区块就绪检查”，但真机回传：`区块在等待后仍未全部就绪，已停止(未改动世界) … 470 格中有 470 格不可编辑 … 示例:ChunkNotLoaded @(833,66,-28) chunk(-2147483648,-2147483648) state=9 loaded=False`。

**逐项排除红鲱鱼（均经 x25.01.17 程序集反射取证）**：
1. `chunk(-2147483648,-2147483648)` 看着像坐标算崩，实为 `CellEditability.UnknownCoord = int.MinValue` 占位符——判定“未就绪”时故意没回填真实区块坐标，是日志显示缺陷，非换算出错。
2. `state=9` = `TerrainChunkState.Valid`（枚举 0..9，Valid 是末位）——即该区块内容/光照/几何其实**已经生成完毕**，本就可编辑。
3. `GetChunkAtCell(x,z)` 与 `ChangeCell(x,y,z)`/`IsCellValid(x,y,z)` 用**同一套 cell 坐标**，不存在块↔原子尺度错配。
4. 落点 `(833,66,-28)` 来自 `ComponentMiner.Raycast(..., RaycastMode.Interaction)` 命中方块面后 `ComputeOrigin` 居中所得——就是玩家准星指着、正在渲染的那格地；且 `_terrain.IsCellValid(...)` 已返回 true（否则归为越界而非“未加载”）。

**真因**：`GameTerrainGateway.InspectCell` 把可编辑性**硬性要求 `chunk.IsLoaded == true`**。但 `TerrainChunk.IsLoaded` 是与 `State` 并列的独立字段，语义是“该区块是否已从存档/流送载入工作集”的磁盘态标志；对玩家正站立、`State` 已 `Valid`、`IsCellValid` 已通过的新生成区块它仍会是 `false`。于是眼前完全可建的落点被判成“未加载”，运行时等待 150 帧后放弃（“未改动世界”这一保守行为本身没错，但中止是误判）。该 `IsLoaded` 判据自 v0.5.0 起存在，从未在真机验证过。

**v0.6.2 最小修复**：
- 可编辑判据改为引擎自身的就绪信号：`chunk.State == TerrainChunkState.Valid && chunk.Cells != null`（`Valid` 已在内容/光照/几何全部生成后才达成，`Cells` 非空即方块数据已分配）。`IsLoaded` 仅继续写入日志诊断，**不再参与把关**。
- 把误导性的状态 `ChunkNotLoaded` 更名为 `ChunkContentsNotReady`，中文标签由“区块未加载”改为“区块内容未就绪”；`CellEditability`/`BuildPlanner`/离线测试双件同步更新。
- 诊断示例改为回填真实区块坐标（`chunk.Coords.X/.Y`，`Engine.Point2` public 字段），不再打印 `int.MinValue` 哨兵；“结论”文案改为如实告知“区块仍在生成中、请靠近后重试、并非越界”，去掉“改坐标/稍后重试即可”这类在此场景下误导的话。

**验证与边界**：`net472` 对真实 x25.01.17 程序集 Release 编译 **0 错误 0 警告**（编译期即确认 `chunk.Cells`、`chunk.Coords`、`TerrainChunkState.Valid` 签名可用）；离线逻辑 **72/72**、XDB 守卫 **7/7** 全通过。重打包 `.netmod` v0.6.2 SHA-256 `404D1798666B28ED85B611AD635760F7EAE497B3D07CFEA071F6B6E60EA11D61`（DLL 程序集版本 0.6.2.0、modinfo Version=0.6.2，Building.xdb `ea25b78e` 仍 0 次、Speedboat 仍 1 次，GUID 修复未被回退）。以上仍是“桌面取证 + 编译期 ABI + 离线逻辑”层面——**Android 7723 上能否真正落建筑材仍为待验证**，需装 v0.6.2 到单人创造测试世界实测：对准脚下地面建造应能成功；若仍失败，请回传新的诊断行（尤其 `state=` 与 `区块内容未就绪/区块状态未就绪` 计数）与完整日志。

## v0.6.3 植被可替换 + 建造中途短暂未就绪改为“有界停顿”

v0.6.2 修好“误判未加载”后，真机推进到建造落点判定，暴露两类新问题：其一，脚边草地上落建筑，旧逻辑对任何“非空气”格一律中止（默认不覆盖），可一株草/一层雪本就该让位；其二，建造自身写方块会触发引擎对区块的重新网格化（re-mesh），使该区块 `State` 短暂跌回 `Valid` 以下，旧执行器把这种**可恢复的瞬时态**当作硬失败，整栋小屋回滚。v0.6.3 针对这两点各做一处最小、可回退的放宽。

**（1）clear-soft：只替换“可穿过”的植被/积雪，绝不碰实心与建筑。**
真机取证（`D:\SC` 反射）确认：`Game.Block.IsCollidable` 是 public 实例 `bool` 字段、`Block.BlockIndex` 是 public 实例 `int` 字段、`BlocksManager.Blocks` 是 public static **属性**（返回 `Block[]`，故 `GetField('Blocks')` 取不到而 `get_Blocks` 存在）、`Game.FluidBlock : Game.CubeBlock : Game.Block`。据此在 `GameTerrainGateway` 新增 `IsCellSoftObstruction`：先 `Terrain.ExtractContents(value)` 取块号，`0`（空气）判否；经懒建静态缓存 `_idToBlock`（按 `Block.BlockIndex` 索引，非数组下标）解析出 `Block`；解析不到→判否（fail-safe，绝不清理无法归类的方块）；`is FluidBlock`→判否（水/岩浆永不被悄悄排掉）；否则返回 `!block.IsCollidable`，即只有草/花/高草/枯枝/雪层等引擎标记“不可碰撞”的可穿过方块才算 soft。`BuildPlanner` 相应把“目标非空气”细分为 **solid（实心地形/玩家建筑，硬冲突，任何模式下都不覆盖）** 与 **soft（植被）**：仅当开启 clear-soft 时才把 soft 格纳入写入清单并计入 `BuildPlan.SoftClearedCount`；纯 soft 受阻返回新失败类别 `BuildPlan.FailSoftBlocked`（`FailedBySoftObstruction==true`）。UI 侧 `WorldEditRuntime` 遵守“默认不覆盖、改动前显式确认”：默认 clear-soft 关；一旦规划因纯植被受阻，提示再点一次建造工具“确认清理植被后重建”，第二次确认才以 `clearSoft=true` 落建，且开工提示会写明“将清理 N 处植被/积雪（不影响实心方块与建筑）”。每次新落点、成功开工、硬失败、联机拦截或会话重置都会把 `_clearSoftArmed` 归零，杜绝意图泄漏。undo 沿用既有 `BlockUndoRecord` 机制：写在植被上的方块记录了原植被 packed 值，撤销时仅当现值仍等于本建造写入值才回灌原值，绝不留下空洞、也不覆盖玩家事后改动。

**（2）有界停顿：可恢复的瞬时未就绪不再回滚整栋，只有真拿不到才放弃。**
新增安全常量 `BuildSafetyLimits.MaxIdleAdvanceCallsDuringBuild = 240`（≈数秒 @30–60fps）。`BuildTask.Advance` 重写：遇到 `eco.IsRecoverableByWaiting` 的未就绪目标（ChunkMissing / ChunkStateNotReady / ChunkContentsNotReady），**不再 Fail**，而是就地暂停、保留本帧已放置的方块、留待下一 `GuiUpdate` 帧再 `Advance`；仅当“连续零前进”的调用数超过 240 才转为 `Fail` 并安全回滚（应对确实永不流入的区块）。任何一次真正写入都使 `_idleAdvanceCalls` 归零。硬错误（越界 InvalidCell、超高 InvalidY、TerrainNull）与“非空气且不允许覆盖”仍即时中止回滚，保守性不变。新增可观测属性 `IdleAdvanceCalls` / `IsStalled` / `StallReason` 供日志与后续 UI 使用。`TestRollbackOnMidBuildFailure`、`TestPartialRollbackIsTrackedAndRetried` 两处原“中途未加载即失败”用例，改为用**收缩有效范围造硬越界**来触发失败（以符合新语义：recoverable=停顿、hard=中止），语义更清晰。

**AGENTS 铁律核对**：边界（尺寸/数量/单轴上限不变）✔；最大数量（≤1024/事务、≤32/帧）✔；取消（`Cancel` 路径未改，停顿态 `Running` 仍可取消）✔；撤销（植被回灌原值、玩家事后改动不被覆盖、partial 保留重试）✔；异常中断（硬错即时回滚、瞬时未就绪有界等待后回滚，绝不谎报完成）✔；仅主线程（gateway/task 仍旧只在 OnUse 与 GuiUpdate 触碰地形）✔；单人门禁不变 ✔。

**验证与边界**：`net472` 对真实 x25.01.17 程序集 Release **0 错误 0 警告**（编译期即锁定 `Block.IsCollidable`/`Block.BlockIndex`/`BlocksManager.Blocks` 属性/`FluidBlock` 继承关系可用）；离线逻辑 **95/95**（原 72 项 + 4 个 v0.6.3 用例：`TestTransientChunkStallPausesInsteadOfAborting`、`TestStallBudgetExhaustionRollsBackSafely`、`TestClearSoftReplacesVegetationWhenOptedIn`、`TestClearSoftNeverTouchesSolidBlocks`，含植被被替换后 undo 精确复原、实心块任何模式都拒写、瞬时停顿不误回滚、超预算安全回滚）；XDB 守卫 **7/7** 全通过。重打包 `.netmod` v0.6.3 SHA-256 `D88B1E1A625490C7A91D2A3E0111B2B24BCAE4F554A252974C936755337FD7E0`（DLL 程序集版本 0.6.3.0、modinfo Version=0.6.3；Building.xdb `ea25b78e` 仍 0 次、Speedboat 仍 1 次，非豁免 GUID 重复 0，v0.6.1 数据库修复未回退）。以上仍是“桌面反射取证 + 编译期 ABI + 离线逻辑”层面——**Android 7723 上 clear-soft 实际替换植被、以及自建 re-mesh 是否确为可恢复瞬时态从而被有界停顿兜住，仍为待验证**。装 v0.6.3 到单人创造测试世界实测：①对准有草的地面建造，第一次应提示“可清理植被”，再点一次应成功落成且日志显示清理了 N 处；②建造跨区块大屋若中途出现 `state≠9`，应看到短暂停顿而非整栋回滚；若仍失败请回传新的诊断行（`IdleAdvanceCalls`、`StallReason`、soft/hard 计数）与完整 `Game.log`。

## v0.7.0 通用 JSON 蓝图导入/导出 + 复制粘贴（AI 建筑接口的桥）

本轮目标（用户明确指令）：在接入 AI 模型之前，**先确保建筑蓝图能被可靠地保存、重新导入并复现相同建筑**。交付一条从“纯数据蓝图 ↔ JSON 文本 ↔ 磁盘 ↔ 真实世界”的双向通路，且**复用既有 `Blueprint`/`BlueprintBuilder`/`BuildPlanner`/`BuildTask`/`TransactionHistory`**，不复制粘贴一份平行引擎。

**新增模块与职责**：
1. `Core/Json/Json.cs`：**零依赖**严格 JSON 解析器 + 确定性写器（net472 无 `System.Text.Json`、不引 NuGet）。递归下降解析，数值语法用 `IsValidJsonNumber` 严格校验（拒绝 `1e`、`.`、孤立符号、尾随垃圾、尾随逗号），`JsonParseException` 带字符偏移；写器按插入顺序输出成员，保证同输入同字节。纯 Core，100% 离线可测。
2. `Core/Blueprints/BlueprintJson.cs`：交换格式（`schemaVersion=1`，`kind=builderassistant.blueprint`）的 `Serialize(Blueprint)` 与 `Decode(string)→BlueprintJsonDecodeResult`（**永不抛异常**，返回结构化成功/错误）。校验覆盖：根须对象、`schemaVersion` 必填且 `≤1`（拒绝高版本静默降级）、`kind` 若存在须匹配、材料表 id→key 唯一、cells 的 x/y/z 须为**整数**且**无重复**、材料 id 不得悬空、数量 `≤1024`、归一化后单轴 `≤64`、声明 `size` 须与重算包围盒一致、cells 非空。**前向兼容但绝不静默造假**：cells 里的 `o`/`d`/`props`（朝向/数据/复杂方块属性）或顶层 `buildOps` 一律**响亮拒绝**，避免 v1 无法复现却硬凑出劣质建筑（AGENTS：不写垃圾）。
3. `Features/WorldEdit/BlueprintCopier.cs`：**两角选区读实时方块→蓝图**。只读不写，绝不损伤源结构。逐格 `TryGetCellValue`（未就绪计 `SkippedNotReady`）→ `Terrain.ExtractContents`（空气计 `SkippedAir`）→ `GameTerrainGateway.TypeNameForContents`（未知计 `SkippedUnknown`）→ 仅当类型名 ∈ `SupportedPlainCubes` 才 `builder.Set`，否则按类型计入 `SkippedUnsupported` 并在 `Describe()` 里逐类报告；`>1024` 判失败。产物是纯 `Blueprint`（用具体原生方块 CLR 类型名作 material key）。
4. `Features/WorldEdit/VanillaBlockPalette.cs`（改写）：新增保守白名单 `SupportedPlainCubes = {CobblestoneBlock, StoneBrickBlock, PlanksBlock, BricksBlock, GlassBlock}`——正是小屋已编译验证过的纯立方方块。`TryResolve` 先查逻辑键，否则当 key 命中白名单时按类型名直接解析，二者皆不中即返回 false → 规划器中止并给出清晰原因。新增 `IsResolvableKey` 供导入/粘贴前的预检。台阶/半砖/带朝向的原木/栅栏/植物/雪/水岩浆/门/箱子熔炉等**一律不复制、导入即拒**，仅在真机逐块验证后才扩列。
5. `Infrastructure/BlueprintStorage.cs`：蓝图 JSON 落盘。**基目录取 `ModsManager.UserDataPath`**（反射确认为 public static string）再拼 Mod 私有子目录 `BuilderAssistant/blueprints`，**绝不硬编码 `/sdcard/...`**——这正是第 10 步“Android 本地文件访问”的答案：优先用游戏自带的内容/路径 API。`SanitizeName` 只保留 `[A-Za-z0-9_-]`（≤48，中文名留在 JSON `name` 字段内以保跨存储可移植）；所有 IO 包 try/catch 返回清晰错误串，不崩。内容正确性由 Core 的往返测试独立证明，本类只搬不透明文本。
6. `Features/WorldEdit/WorldEditRuntime.cs`（升级）：引入 `RuntimeMode { Cottage, Copy, Paste }` 状态机。`OnBuildTool` 按模式分派；复制模式两次点选对角→`BlueprintCopier.Copy`→填剪贴板→自动 `BlueprintStorage.Save`（存盘失败也不丢内存剪贴板，本次粘贴仍可用）→切到 Paste；粘贴用 `EffectiveClipboard()`（按 `_clipboardRotation` 在纯蓝图空间 `Reorient`）定位后走 `BeginBuildBlueprint`：先 `CollectUnresolvableKeys` 预检材料，再复用与小屋**同一条** `PlanAndStart`（写前全量校验、有界区块等待、clear-soft、回滚、undo 全部一致）。新增公开入口 `ImportBlueprint`/`ExportClipboard`/`SetPasteRotation`/`CycleMode`/`SetMode`。手机无独立模式按钮，故**空闲态点撤销工具即循环 Cottage→Copy→Paste**（仅当无在途任务/无预检等待/无待确认/无可撤销时，busy 分支优先，互不干扰）。

**旋转**：`Blueprint.Reorient(0..3, mirrorX)` 早在 v0.5 已有且离线充分验证；本轮把它接进粘贴路径（`SetPasteRotation` 归一模 4），旋转只作用于蓝图空间、落地不碰地形，故无需为转向改动区块逻辑。

**AGENTS 铁律核对（第 11 步）**：改动全部落在 `src/BuilderAssistant/` 与其离线测试工程，**SCIE-Port 一字未动**；单测/守卫/编译/打包均无一处触及联机（`OnBuildTool`/`OnUndoTool` 仍先 `IsSinglePlayer` 门禁 `WorkType.Local`，复制/粘贴/导入皆经此门）；**未绕过任何区块校验**——复制只读且经 `TryGetCellValue→InspectCell`（`State==Valid && Cells!=null`）判就绪，粘贴写前仍跑 `BuildPlanner.Plan` 的全足迹预检与有界等待，默认不覆盖、显式确认、仅主线程一律保留。✔✔✔

**验证与边界**：`net472` 对真实 x25.01.17 程序集 Release **0 错误 0 警告**（编译期即锁定 `ModsManager.UserDataPath`、`Terrain.ExtractContents`、`BlocksManager.FindBlockByTypeName`、`Block.GetType().Name` 等签名可用；过程中修掉一处 CS1570（doc 注释里未转义的 `<`）与一处 CS0051（`SetMode` 与私有 `RuntimeMode` 可访问性不一致，已把 enum 提为 internal、方法降为 internal））；离线逻辑测试由 95 项扩到 **133/133 全通过**（新增 14 个 JSON/蓝图用例：往返复现同方块集、字节稳定再序列化、负坐标归一化、重复坐标拒、小数坐标拒、`schemaVersion` 999/缺失拒、`>1024` 拒、单轴 65 拒、悬空材料 id 拒、空 cells 拒、per-cell `d` 拒、非空 `buildOps` 拒、声明 size≠包围盒拒、四分之一转向一致性）；XDB 守卫 **7/7**。重打包 `.netmod` v0.7.0 SHA-256 `4B227BB59C731D02BC1724473A69ABFD1B5A5D6E440C8C761C1487C1F1F51064`（DLL 程序集版本 0.7.0.0、modinfo Version=0.7.0；随包核对 Building.xdb `ea25b78e` 仍 0 次、Speedboat 仍 1 次，v0.6.1 数据库修复未回退；不含任何专有游戏程序集）。以上仍是“桌面 + 编译期 ABI + 离线逻辑”层面——**Android 7723 上的复制读取真实方块、剪贴板粘贴、JSON 导入/导出、以及 `UserDataPath` 落盘 IO 全部为待验证**；桌面测试从未在设备上跑过一次文件读写。装 v0.7.0 到单人创造测试世界实测：①切到复制模式点两角复制一段由已知立方方块构成的建筑，应报“已捕获 N 个…/已存入蓝图库：copy_….json”，并能切 Paste 后两次点击成功原样重建；②用撤销工具一次整体回退粘贴体、且不得覆盖回退前玩家的手改；③复制选区若含台阶/植物/水，应在报告中如实列为“暂不支持”并跳过、绝不硬凑；④导入一个先前保存的 `.json` 应能再次粘贴；若失败请回传首条 `Warning/Error` 原文与完整 `Game.log`。

## v0.8.0 蓝图工具：外部 AI → 导入 → 落成 闭环

v0.7.0 有数据桥但缺游戏内触屏导入入口；v0.8.0 补上第三个手持工具——蓝图工具（item 903），闭环“外部 AI 生成蓝图 JSON → 用系统文件管理器丢进手机蓝图库 → 手持蓝图工具逐次点击浏览/载入/校验 → 建造工具两次点击落成”。全程仍走 v0.5 起既有的 `PlanAndStart`→`BuildPlanner`→`BuildTask`→`TransactionHistory` 主链，安全规则一字不改。

| 项 | 结论 | 状态 |
| --- | --- | --- |
| 触屏面仅 `SubsystemBlockBehavior.OnUse(Ray3, ComponentMiner)` + `ComponentGui.DisplaySmallMessage(...)`，**无文本框/文件选择器/对话框** | 全仓检索确认该 ABI 无文本输入接口；导入必须做成触屏逐次点击 | 已确认（编译期 ABI） |
| `BlueprintToolBlock : PaintedCubeBlock(15)`（玻璃立方），行为类 `SubsystemBlueprintToolBlockBehavior`（`HandledBlocks={903}`，`OnUse` 里 `ExtractContents(ActiveBlockValue)==903` 守卫） | 与建造/撤销工具同一注册套路，复用既有行为分发；编译期绑定 | 已确认（编译期 ABI） |
| 物品编号 903 | 高于原版 0–263，且与快艇 900、建造 901、撤销 902 不冲突（打包脚本校验类名唯一） | 已确认（静态/打包） |
| 903 与设备上**其它第三方 mod** 的编号冲突 | SCNET 无全局编号注册表 | **待验证** |
| 空库时用 `DisplaySmallMessage` 打印真实基目录 `BlueprintStorage.BaseDirectory`（取 `ModsManager.UserDataPath`，绝不硬编码），玩家据此用安卓文件管理器投递 `.json` | 路径来源与拼接逻辑离线/编译均已确认 | 已确认（编译期 ABI） |
| 安卓 scoped storage 下**系统文件管理器能否写入 `UserDataPath`** | 桌面无从判定；若不可写，逃生绳为 ADB push 到同一路径 | **待验证** |
| 载入即 `BlueprintJson.Decode` + 材料预检，非法/AI 劣质文件响亮拒绝、绝不改动剪贴板、绝不部分导入 | 拒绝语义与 v0.7.0 同一 Core 逻辑，已离线证明 | 已确认（离线）+ 待验证（真机 IO） |

**验证与边界**：本轮所有改动均落在 `src/BuilderAssistant/` 与其离线测试工程，**SCIE-Port 一字未动**；蓝图工具只读文件+校验、从不写地形；载入后落成经与小屋完全相同的写前全量校验、有界区块等待、clear-soft、默认不覆盖+显式确认、仅主线程、`IsSinglePlayer` 单人门禁，铁律核对不变 ✔。

- 离线逻辑测试由 133 项扩到 **147/147 全通过**（新增 14 项针对纯 `BlueprintLibraryBrowser` 游标：空库/负数→-1、游标 -1 首点选 0、单调前进 0→1→2、到末尾回绕 2→0、单条目恒为 0、两条目连点六次严格交替 0,1,0,1,0,1；以及 `[i/N]` 位置标签：首/末/单条目、空库/预选游标 -1/越界游标的空串防御）。这些断言不依赖游戏程序集或文件 IO。
- XDB 合并安全守卫 **7/7** 全通过，含“真实随包 `.xdb` 文件（已含新 `BlueprintToolBlockBehavior` 模板与 Class 参数）必须干净”一项——新增的三个 GUID（模板 `c1f4a9d6…`、Class 参数 `9d6b253e…`、Project 成员 `6a7b1b7f…`）经文件内唯一性 + 跨文件全局唯一性双重校验，未回退 v0.6.1 数据库修复。
- `net472` 对真实 x25.01.17 程序集（`D:\SC`）Release **0 错误 0 警告**（编译期即确认 `BlueprintToolBlock` 基类、`SubsystemBlockBehavior.OnUse`/`HandledBlocks`、`Terrain.ExtractContents`、`ComponentMiner.ActiveBlockValue`、`ComponentGui` 等签名可用）。
- 重打包 `.netmod` v0.8.0 SHA-256 `D1FD40CF4EE61DC74E3C9F96106DE101900C829F596134466BB40220581AC700`。随包核对：CSV 方块类名唯一（`BlueprintToolBlock, BuilderToolBlock, SpeedboatBlock, UndoToolBlock`）、`Building.xdb` 中 `ea25b78e` 仍 0 次 / Speedboat 仍 1 次、不含任何专有游戏程序集。

**重要**：Windows 编译 + 打包 ≠ 运行验证。以上证明接口签名正确、可编译、离线逻辑成立；蓝图工具在手机上的浏览、导入、落成，以及文件投递路径**全部为待验证**——桌面从未在设备上跑过一次文件读写。

**Android 7723 单人创造世界实测清单**：
1. 装 v0.8.0，进单人创造世界，加载横幅应显示 v0.8.0（不再写死）。
2. 手持蓝图工具点击 → 记下消息里打印的真实蓝图库目录；用安卓自带文件管理器在该目录放入一个合法蓝图 `.json`（文件名纯 ASCII，仅限字母/数字/下划线/连字符，≤48）。
3. 再点蓝图工具 → 应报“已载入蓝图 [1/1]「name」 尺寸 …”；切建造工具对准地面两次点击 → 应落成，撤销工具可整体回退。
4. 放入一个非法 JSON（含不支持方块 / 超界 / 坏语法）→ 应报“导入失败：<原因>”，且不污染剪贴板（随后建造工具应保持原有蓝图或提示空）。
5. 放入多份 `.json` → 连点蓝图工具应循环浏览、到末尾回绕到第一条，`[i/N]` 计数正确。
6. 建造或预检等待进行中点蓝图工具 → 应被“施工或等待区块就绪中…”挡下，不误触发导入。
若失败，请回传首条 `Warning/Error` 原文 + 完整 `Game.log`（尤其含 `DisplaySmallMessage` 里打印的真实路径，以判定 scoped storage 是否可写）。

## v0.8.1 剪贴板粘贴导入（免建目录 / 免手写文件）

v0.8.0 已闭环，但玩家仍须“建目录 / 起 ASCII 名 / 用文件管理器投递 `.json`”，触屏成本高。v0.8.1 增一条**零文件操作**入口：把蓝图 JSON 复制到手机剪贴板 → 手持蓝图工具(903)点一下即粘贴导入；剪贴板不像 JSON 或服务不可用时，自动**原样回退** v0.8.0 的 toast+文件浏览，不降级、不报错。落成仍走建造工具(901)两次点击 → 同一 `PlanAndStart`→`BuildPlanner`→`BuildTask`→撤销链，安全规则一字不改。

重要更正：v0.8.0 曾记“该 ABI 无文本框/文件选择器/对话框”——那只对**文档化的 SCNET Mod API**成立。反射真实 x25.01.17 Windows 程序集发现，游戏还暴露了若干 **public `Game.*` 内部类型**（非文档 Mod API）：`Game.ClipboardManager.ClipboardString`{get;set;}、`Game.DialogsManager.ShowDialog/Alert/Confirm/Promit`、`Game.TextBoxDialog`、`Game.ListSelectionDialog`、`Game.MessageDialog` 等。它们在 Windows 参考程序集上**编译通过**，但**安卓/7723 运行时行为未经证实**。故本版只用其中最简单、可优雅回退的剪贴板读取，且**一律包在 `GameInterop` 的 try/catch 里，绝不假设未验证接口一定可用**；对话框/文本框面板留作第二阶段（避免一次堆太多未验证 UI）。

| 项 | 结论 | 状态 |
| --- | --- | --- |
| `Game.ClipboardManager.ClipboardString`（读 OS 剪贴板文本） | 对真实 Windows x25.01.17 程序集编译通过；但非 SCNET 文档 API，7723 上是否可用、拿到的是否系统剪贴板文本无从桌面判定 | **待验证**（编译期 ABI + 真机未测） |
| 剪贴板不可用/抛异常时的行为 | `GameInterop.TryReadClipboardText` try/catch 兜底返回 null → 上层回退 v0.8.0 toast+文件浏览 | 已确认（离线逻辑）|
| `BlueprintImportService.FromString` 复用同一 `BlueprintJson.Decode` + 材料预检，空/非 JSON 形/超长/坏语法/未知材料/重复坐标/超限一律精确拒绝且 `blueprint=null` | 与 v0.8.0 导入同一 Core 校验语义，不重写解析器；离线证明失败绝不返回半成品蓝图（故绝不污染剪贴板） | 已确认（离线）|
| `BlueprintNamePolicy`：纯中文名→`bp_<FNV 哈希>`（不再统统塌成 `blueprint` 互相覆盖），撞名自动 `_2/_3`，词干≤48，`ResolveUnique` 永不返回已存在词干（不覆盖）；可读名仍存 JSON `name` | 纯逻辑，离线单测覆盖去重、不覆盖、CJK 不塌名、哈希确定性 | 已确认（离线）|
| `BlueprintStorage.Save` 自动建目录 + 唯一命名 + 不覆盖同名 | 目录/IO 依赖 `ModsManager.UserDataPath` 与设备文件系统 | 已确认（离线命名）+ **待验证**（真机 IO）|
| 安卓 SAF 系统文件选择器（`GameInterop.TryPickBlueprintFileText`） | 仅预留独立入口桩，恒返回“未接入”，不触碰剪贴板路径；待原生调用真机验证 | **不支持**（未接入，第二阶段）|

**验证与边界**：本轮改动全落在 `src/BuilderAssistant/` 及其离线测试工程，**SCIE-Port 一字未动**、快艇原型未动、既有事务安全策略未动；蓝图工具仍**从不写地形**；导入失败**绝不改内存剪贴板、绝不部分导入、绝不落坏盘**。

- 离线逻辑测试由 147 项扩到 **194/194 全通过**（新增 47 项：合法粘贴导入复现正确尺寸/名称/方块数；空文本(null/""/纯空白)拒；非 JSON 形（不以 `{` 开头）拒；超尺寸 blob（过 shape 门但未进 tokenizer）拒并留 NO 蓝图；坏 JSON 拒；重复坐标拒；`>1024` 方块拒（计数门先于坐标门）；材料预检拒未知块且同 JSON 在“全部支持”下成功；七种坏输入逐一验证“失败⇒blueprint=null”即剪贴板完整性；`BlueprintNamePolicy` 的 sanitize/纯中文不塌名/`ResolveUnique` 撞名 _2/_3 且结果必不在已存在集合/`StableHash` 确定性与 8 位十六进制）。这些断言不依赖游戏程序集或文件 IO。
- XDB 合并安全守卫 **7/7** 全通过（本轮未改任何 `.xdb`，`ea25b78e` 仍 0 次、Speedboat 仍 1，v0.6.1 数据库修复未回退）。
- `net472` 对真实 x25.01.17 程序集（`D:\SC`）Release **0 错误 0 警告**（编译期即确认 `Game.ClipboardManager.ClipboardString` 与 `BlueprintToolBlock/BuilderToolBlock/UndoToolBlock` 等签名可用；`GameInterop` 通过编译证明“能编”，不等于“真机能跑”）。
- 重打包 `.netmod` v0.8.1 SHA-256 `E71E73866B0DA2B3C603F875E6DB6989C6A1287A221FE20E45139891B1F1310C`（2,338,624 字节）。随包核对：CSV 方块类名唯一（`BlueprintToolBlock, BuilderToolBlock, SpeedboatBlock, UndoToolBlock`）、`Building.xdb` 中 `ea25b78e` 仍 0 次 / Speedboat 仍 1 次、不含任何专有游戏程序集。

**重要**：Windows 编译 + 打包 ≠ 运行验证。剪贴板读取在 7723 上是否可用、拿到的是否系统剪贴板文本，以及真机文件落盘，**全部为待验证**；桌面从未在设备上跑过一次剪贴板读或文件写。因剪贴板路径已优雅回退文件浏览，即便真机不支持剪贴板，可用版本也不被阻塞。

**Android 7723 单人创造世界实测清单**：
1. 装 v0.8.1，进单人创造世界，加载横幅应显示 v0.8.1。
2. 用任意 App 复制一段**合法**蓝图 JSON（或以文本编辑器全选复制 v0.8.0 清单里那类合法 `.json` 内容）到系统剪贴板 → 手持蓝图工具(903)点一下 → 应报“已导入并载入蓝图「name」 尺寸 …，已切换到粘贴模式。（已存入蓝图库：….json）”。若报的是“蓝图库是空的/仍为空…”说明剪贴板不可用、已回退浏览——据此判定 7723 是否支持 `ClipboardManager`。
3. 切建造工具(901)对准地面两次点击 → 应落成；撤销工具一次整体回退。
4. 复制一段**非法**内容（坏 JSON / 含不支持方块 / 超 1024 / 重复坐标）→ 应报“导入失败：<精确原因>”，且**不得**改动当前已载入蓝图（随后建造工具应保持上一个有效蓝图或提示空）。
5. 复制**两个同名**合法蓝图先后各导入一次 → 落盘文件名应为 `name.json` 与 `name_2.json`（或纯中文名对应的 `bp_xxxx.json` 与 `bp_xxxx_2.json`），**不得互相覆盖**；两份都可再浏览到。
6. 用**纯中文 name** 的蓝图导入 → 存档文件名应带可读的 `name` 字段（JSON 内）且磁盘名不再统统是 `blueprint`，两个不同中文名应得两个不同文件。
7. 剪贴板内容不变连点蓝图工具 → 应只重复播报“剪贴板内容未变，已载入…”，不重复生成新编号副本。
若失败，请回传首条 `Warning/Error` 原文 + 完整 `Game.log`，尤其注明第 2 步是走了“粘贴导入”还是回退成“文件浏览”，以判定 `Game.ClipboardManager` 在 7723 的可用性。

## v0.8.2 扩充可复制/可建造的纯立方石/砖方块（Phase A，只扩白名单）

本轮目标（用户明确指令「支持更多方块」，AskUserQuestion 定为“先只扩充纯立方方块”“优先石/砖类”）：把 `VanillaBlockPalette.SupportedPlainCubes` 从小屋已验证的 **5** 块纯立方扩到 **12** 块，让实时区域复制与导入/手写/AI 蓝图都能复现更多石/砖块。这是**白名单最小改动**：`schemaVersion` 仍 1、地形写值仍 `data=0/light=0`、**不含按格颜色**、`GameTerrainGateway`/`BlueprintJson`/`BlueprintCopier`/`Building.xdb` 均未改，联机/SCIE-Port/快艇/事务安全一字未动。

依据来源：反射 `D:\SC\Survivalcraft.exe` 得到立方族全谱（非猜测），据此只挑 `CubeBlock`/`PaintedCubeBlock` 中 **data=0 即可完整复现、无朝向/实体/相态数据位**的纯立方。**明确排除**含实体(Chest/Dispenser/Furnace)、相态(Fluid/Ice/Snow)、重力(Sand/Gravel)、蔓延(Grass/Soil/FallenLeaves)、不可破坏(Bedrock)、带朝向原木(Wood)、导线(WireThrough)、以及 AlphaTest 族(Leaves/WickerLamp/Window/FramedGlass，本轮不加)。

新增 7 块与状态：

| 方块（material key = CLR 类型名） | 族 | 类存在 / 编译 ABI | 7723 实机渲染 |
| --- | --- | --- | --- |
| BasaltBlock（玄武岩） | PaintedCube | 已确认（反编译+net472 编译） | **待验证** |
| GraniteBlock（花岗岩） | PaintedCube | 已确认 | **待验证** |
| MarbleBlock（大理石） | PaintedCube | 已确认 | **待验证** |
| SandstoneBlock（砂岩） | PaintedCube | 已确认 | **待验证** |
| ClayBlock（黏土块） | PaintedCube | 已确认 | **待验证** |
| LimestoneBlock（石灰岩） | Cube 固定贴图 | 已确认 | **待验证** |
| MalachiteBlock（孔雀石块） | Cube 固定贴图 | 已确认 | **待验证** |

安全保证（可确认）：把某名字加入白名单是**失败即止、绝不塞垃圾**的——`TryResolve` 经 `BlocksManager.FindBlockByTypeName` 解析，若该名字在真机不是已注册方块则返回 false，建造/导入会以精确原因**干净中止**，绝不会放错块或半块。故“加入白名单”本身不引入写坏地形风险，唯一未决问题是上表逐块的 7723 渲染表现。

“复制丢色”取舍（务必知悉）：`PaintedCube` 族的按格颜色不存于地形 value，Phase A 只写 `data=0/light=0`，因此**已上色**的源方块被复制/落成后会呈现**引擎默认色**。此现象对现有 Cobble/StoneBrick/Planks/Bricks 早已存在，新块只是扩大范围；忠实保留颜色需 Phase B 升 `schemaVersion=2` 加按材料 `color` + `IPaintableBlock`/地形上色 API，且那些非文档 API 必须先在真机验证——本轮明确不做。

已跑验证（本机，无设备）：
- 离线 `tests/BuilderAssistant.Tests` 净增至 **203/203 全通过**（在 v0.8.1 的 194 项基础上新增 9 项，专测“新具体方块类型名作 material key 经 `BlueprintJson` 序列化/解码逐字复现、`BlueprintImportService` 以 resolver 为唯一门控可放行或精确拒绝新块”）。这些断言不依赖游戏程序集。
- `net472` 对真实 x25.01.17 程序集（`D:\SC`）Release **0 错误 0 警告**（`Test-Environment` 哈希/2.4.0.0/net472/API1.42/SC2.4.10.6/快艇 ABI 均通过）。
- XDB 合并安全守卫回归 **7/7**（`Building.xdb` 本轮未改，仍 0 冲突；`ea25b78e` 复用缺陷未回退）。
- `.netmod` v0.8.2 SHA-256 `7FE91876B0C9A9AF93E750EC8DE0985E891207C2C925448C593C6C0ABDCD04AA`（2,339,153 字节，取代 v0.8.1 的 `E71E7386…`）。随包核对：`modinfo.json Version=0.8.2`、`BuilderAssistant.dll` 程序集版本 `0.8.2.0`、`Building.xdb` 中 `ea25b78e` 仍 0 次、CSV 方块类名仍唯一（`BlueprintToolBlock, BuilderToolBlock, SpeedboatBlock, UndoToolBlock`）、不含任何专有游戏程序集。

**重要**：Windows 编译 + 打包 ≠ 运行验证。以上只证明“这些 CLR 类存在于已验证的程序集、可编译、离线复现成立”；这 7 块在手机里是否按预期整块渲染，**全部待验证**——桌面从未在设备上落过任何一个新块。任何一块未实测前不得标“兼容/已确认”。

**Android 7723 单人创造世界实测清单（逐块）**：
1. 装 v0.8.2，进单人创造世界，加载横幅应显示 v0.8.2。
2. 对玄武岩/花岗岩/大理石/砂岩/黏土/石灰岩/孔雀石**逐一**准备一份只含该块的小型蓝图（`materials[].key` 用具体类型名，或从现含该块的真实结构复制），蓝图工具载入 → 建造工具(901)对准地面两次点击 → 应落成，且该块渲染为**完整整块**、无缺失/半块/紫黑/透明异常。
3. 复制一段含该块的结构再落回 → 类型保留、位置正确；若源块已上色，观察落成是否为默认色（预期“复制丢色”，非缺陷）。
4. 放非法/超界/含未支持块的蓝图 → 应“导入失败：<原因>”，且不污染剪贴板、不部分导入。
5. 撤销工具一次应整体干净回退。
若某块异常，回传首条 `Warning/Error` 原文 + 完整 `Game.log`，并注明是“落成”还是“复制/导入”阶段、具体哪个块；该块在本文保持**待验证**，不得标兼容。


## v0.9.0 支持带朝向方块：楼梯 / 半砖 / 栅栏 / 门（Phase B，蓝图 schema v1→v2）

本轮目标（用户明确指令「添加楼梯、栅栏、门、半砖等方块」，AskUserQuestion 定为“四类全做·所有变体”“改造生成器使用新方块”）：把 Builder Assistant 从只支持**纯立方**推进到支持**带数据位的朝向方块**——楼梯（stairs）、半砖（slab）、栅栏（fence）、门（door），覆盖其全部材料变体，并把中世纪小屋生成器改为**真正使用**这些新块。为此蓝图交换格式由 `schemaVersion=1` 升级到 **`schemaVersion=2`**。

这是一次**真正的 schema 迁移**，不是白名单扩充：每个格子现在可携带两个**可选**字段——`f` 族标签（`cube|stairs|slab|fence|door`，缺省=cube）与 `d` 该族**自有**的数据位。凡不属该格自有的、由引擎派生的状态（楼梯拐角接合、所有方块颜色）一律**不入库**（`BlockOrientation.Normalize` 剥离），延续“绝不塞垃圾/降级生成”的既有铁律。

### ABI 取证（依据来源：反编译 `D:\SCNET\Survivalcraft.exe` x25.01.17 Game 程序集，非猜测）

地形值打包 `Game.Terrain.MakeBlockValue(contents, light, data)`：contents 占 bit 0–9（`0x3FF`），light 占 bit 10–13，data 占 bit 14+；`ExtractData=(value & -16384)>>14`、`ExtractContents=value & 0x3FF`。各族 `data` 位语义（从 `StairsBlock`/`SlabBlock`/`FenceBlock`/`DoorBlock` 数据访问器与 `SubsystemFenceBlockBehavior`/`SubsystemStairsBlockBehavior`/`SubsystemDoorBlockBehavior` 邻接逻辑反推）：

| 族 | data 自有位 | Normalize 掩码 | 引擎派生（不入库）| 朝向变换 |
| --- | --- | --- | --- | --- |
| Stairs | rot[0–1] + upsideDown[2] | `0x7` | 拐角接合[3–4]、颜色[5+] | 随朝向变 |
| Slab | isTop[0] | `0x1` | 颜色[1+] | 不变（仅 isTop）|
| Fence | 四连接臂 [0]=+X [1]=−X [2]=+Z [3]=−Z | `0xF` | 引擎按邻块重算，仍保留授权掩码以便即时渲染 | 臂随旋转置换 |
| Door | rot[0–1] + open[2] + rightHanded[3] | `0xF` | — | 随朝向变；镜像翻铰链 |

朝向约定（与 `Block.GetPlacementValue` 一致）：`rot0=−Z, rot1=−X, rot2=+Z, rot3=+X`。一次顺时针 90°：`newRot=(rot+3)&3`；栅栏臂 `+X→+Z→−X→−Z→+X`。X 镜像：`newRot=(−rot)&3`，门的 rightHanded 位翻转，栅栏 `+X/−X` 互换而 `+Z/−Z` 不动。这些公式全部落在**纯 Core** `Core/Blueprints/BlockOrientation.cs`（无游戏依赖、离线可单测），`VanillaBlockPalette` 只负责“类型名→族→`MakeBlockValue` 打包”。

门是**双格方块**：蓝图/复制**只存下半格**，写入下半格后 `SubsystemDoorBlockBehavior.OnBlockAdded` 依下半格自动生成上半格，且要求下方为实心支撑。`BlueprintCopier` 会识别门上半格并计入 `SkippedDerived`（**只采下半**），否则粘贴时写入下半→引擎自动造上半→紧接着的计划上半写入会命中“目标已非空气”而中止——这是本轮修掉的真实设计缺陷。`BuildPlanner` 的格子按 Y 升序执行，保证“先下后上”。

新增支持变体（材料 key = CLR 类型名）：楼梯 9（Wooden/Cobblestone/Stone/StoneBrick/Brick/Basalt/Marble/Sandstone/Dirt StairsBlock）、半砖 9（同集 SlabBlock）、栅栏 5（Wooden/Stone/Brick/Basalt/Iron FenceBlock）、门 2（WoodenDoorBlock/IronDoorBlock），连同既有立方 12。`SupportedPlainCubes` 作为 `SupportedBlocks` 的 Cube 子集保留（向后兼容）。

安全模型**一字未改**：默认不覆盖非空气 + 显式二次确认、主线程分批写入、区块就绪预检、有界停等（transient re-mesh 暂停重试而非整体回滚）、撤销仅恢复玩家未再改动的格。旧 `schemaVersion=1` 文件（无 f/d）仍可解码、按纯立方载入，历史蓝图不受影响。

已跑验证（本机，无设备）：
- 离线 `tests/BuilderAssistant.Tests` 由 203 项净增至 **267/267 全通过**（新增 64 项：`BlockOrientation` 的 Normalize 掩码、旋转四次=恒等/−1≡+3 群律、楼梯朝向置换、栅栏臂置换、镜像对合+门铰链翻转；`BlueprintJson` schema v2 朝向格逐字往返、纯立方不发 `cube` 标签以保 v1 字节形、引擎派生位被剥离、非法族标签与 `props` 与负 `d` 精确拒绝；小屋**确实**产出楼梯 35/半砖 2/栅栏 2/门 1 且各朝向正确、门上半格确未被授权写入；整幢小屋旋转四次回到恒等、旋转后 JSON 往返朝向完好）。断言不依赖游戏程序集或文件 IO。
- `net472` 对真实 x25.01.17 程序集（`D:\SCNET`）Release **0 错误 0 警告**（`Directory.Build.props` 设 `TreatWarningsAsErrors=true`，0 警告即编译期绑定成功——`Terrain.MakeBlockValue`/`ExtractData` 与各 `*Block` 族名可用）。
- XDB 合并安全守卫 **7/7**（本轮**未改任何 `.xdb`/CSV**：新块都是已存在的原版方块，经 `BlocksManager.FindBlockByTypeName` 按类型名解析，无需新增子系统/方块注册，故不引入新 GUID；`ea25b78e` 复用缺陷未回退）。
- 重打包 `.netmod` v0.9.0 SHA-256 `E9281514FB57884BE5EF63E73AC3C7C4B8A0334F7D53A97A108F208F35EDA04E`（2,342,108 字节，取代 v0.8.2 的 `7FE91876…`）。随包核对：`modinfo.json Version=0.9.0`、`BuilderAssistant.dll` 程序集版本 `0.9.0.0`（FileVersion `0.9.0.0`，ProductVersion `0.9.0+…`，无 `0.8.2` 残留）、CSV 方块类名仍唯一、不含任何专有游戏程序集。

**重要**：Windows 编译 + 打包 ≠ 运行验证。以上只证明“这些族名存在于已验证程序集、可编译、朝向数学离线成立、蓝图能无损往返”；楼梯/半砖/栅栏/门在 7723 手机上**是否按朝向正确渲染、门是否自动补上半格、栅栏是否正确接连**，**全部为待验证**——桌面从未在设备上落过任何一个朝向方块。任一变体未实测前不得标“兼容/已确认”。

**Android 7723 单人创造世界实测清单（逐族）**：
1. 装 v0.9.0，进单人创造世界，加载横幅应显示 v0.9.0。
2. 内置小屋：建造工具(901)对准平整地面两次点击 → 应整幢落成，檐口呈**楼梯斜坡**、正面有**木门**、窗下有**半砖窗台**、屋脊两端有**栅栏**；撤销一次应整体干净回退。
3. 门：落成后正面门洞应显示**完整两格门**（下半由蓝图写、上半由引擎自动生成），若只见下半而上半为空/异常，即 `SubsystemDoorBlockBehavior.OnBlockAdded` 在该机未触发——回传日志，标注**待验证**。
4. 复制往返：对一段含楼梯/半砖/栅栏/门的真实结构做两角复制 → 蓝图 JSON 应含 `f`/`d`；再落回 → 朝向、门的两格、栅栏接连应与源**目视一致**。
5. 旋转/镜像：同一蓝图切不同朝向落成 → 楼梯高边、门开向应随整体转动（不是逐格乱转）。
6. 非法/超界/含未支持块 → 应“导入失败：<原因>”，不污染剪贴板、不部分导入。
若某族/某变体异常，回传首条 `Warning/Error` 原文 + 完整 `Game.log`，注明阶段（落成/复制/导入）与具体方块；该变体在本文保持**待验证**，不得标兼容。
