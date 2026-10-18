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

本轮只改 Builder Assistant，不动 SCIE-Port，也不改动快艇。证据严格分级如下：

1. **Core 纯逻辑（本机已离线证明）**

   新增 `Core/Transactions/CellEditability.cs`（每格不可编辑原因枚举+结构）、`ITerrainGateway.InspectCell`、`BuildPlan` 的不可编辑明细与 `AllEditabilityFailuresRecoverable`、`BuildTask` 的延迟回滚/撤销条目与 `RetryRollback`/`RetryUndo`、`TransactionHistory.UndoLast(gateway, out pending)` 的部分撤销保留。以上均由 `tests/BuilderAssistant.Tests`（net8，仅编译 Core+Generators 源码，不引用游戏程序集）离线验证，`72/72` 断言全通过；覆盖跨区块未就绪的分类与可恢复判定、越界/高度为硬性不可恢复、部分回滚被追踪并重试、部分撤销保留待续做、以及延迟撤销遇到玩家后续改动时让位不覆盖。

2. **Features 适配层（源码就绪，本机未能编译 — 待验证）**

   `GameTerrainGateway.InspectCell` 与 `WorldEditRuntime` 的预检/延迟回滚只用 v0.5.0 已经编译绑定过的 ABI 成员（`Terrain.GetChunkAtCell`、`TerrainChunk.State==Valid`、`TerrainChunk.IsLoaded`、`Terrain.IsCellValid`、`SubsystemTerrain.ChangeCell`），未引入任何新签名。但**本会话机器上不存在游戏引用程序集**，`net472` Mod 工程无法在此重新编译，故这两个文件状态为“源码完成、本机未编译”：

   - 直接 `dotnet build -c Release` 在引用校验步骤即失败：`error : Missing SCNET x25.01.17 reference: E:\SCNET\SCNETx25.01.17\Survivalcraft.exe. Pass -p:SCNetGameDir=... or set SCNET_GAME_DIR.`
   - 复现（需在装有 x25.01.17 的机器上，指向真实游戏目录）：`./scripts/Build-NetMod.ps1 -GameDir '<游戏目录>' -Configuration Release`
   - 结论：Features 层与 net472 打包标记为**待验证**，仅当在有游戏程序集的机器上 0 错误 0 警告编译通过后，才可升级为“已确认（编译期 ABI）”。

3. **Android 实机（待验证）**

   尚不能宣称该修复在手机上生效。有限等待是否真能在触屏上等到跨区块小屋的相邻区块加载、延迟回滚/撤销的重试提示是否按预期出现，均需 7723 Android 单人创造世界实测。
