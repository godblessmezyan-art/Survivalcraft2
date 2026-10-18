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

检查来源：本机 `E:\SCNET\SCNETx25.01.17`，来自公开 x25.01.17 Windows 发布包。

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

## 尚未确认的能力

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
| 区块就绪判定 `Terrain.GetChunkAtCell` -> `TerrainChunk.State==Valid && IsLoaded` | 经编译绑定；`GetChunkAtCell` 缺失时返回空，非分配 | 已确认（编译期 ABI） |
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
