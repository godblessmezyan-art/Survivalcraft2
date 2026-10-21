# SurvivalcraftRuins → SCNET x25.01.17 移植记录

## 范围与证据基线

本轮仅新增独立目录 `SurvivalcraftRuins-Port/`，没有修改 Builder Assistant 或
`SCIE-Port/` 的源码、工程、资源、版本和产物。上游固定为
<https://github.com/godblessmezyan-art/SurvivalcraftRuins> 提交
`53425779dc499c6f08b94f4b973f329217f92765`，保留 Mulan PSL v2 `LICENSE`、
README、作者 `SCRuins`、23 种结构逻辑、13 个 JSON 模板、世界生成、战利品、守卫、
机关、设置读写和原测试。

目标程序集来自本机 `E:\SCNET\SCNETx25.01.17`。它们只作为外部引用，不进入 Git 或
`.netmod`：

| 程序集 | SHA-256 | 状态 |
| --- | --- | --- |
| `Survivalcraft.exe` | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` | 已确认 |
| `Engine.dll` | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` | 已确认 |
| `EntitySystem.dll` | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` | 已确认 |

`Survivalcraft.exe` 的程序集版本为 `2.4.0.0`，TargetFramework 为
`.NETFramework,Version=v4.7.2`，内部标记为 Mod API `1.42`、SC `2.4.10.6`、
发行标识 `x25.01.17`。这些是 Windows 公开包的证据，不能替代 7723 重打包 APK 的
程序集哈希比对。

## 兼容性迁移

- 主工程从 `net10.0` 改为 `net472`，真实引用 `Survivalcraft.exe`、`Engine.dll`、
  `EntitySystem.dll`，引用均为 `Private=false`。
- `System.Text.Json` 改为目标游戏内置的 `SimpleJson`，模板、战利品和持久化均不携带
  额外 JSON DLL。持久化采用临时文件完成后替换正式文件。
- `Math.Clamp`、`MathF`、`string.Contains(StringComparison)` 均替换为 net472 可用实现。
  `required`/`init`/record 仅使用编译器元数据 polyfill，不调用 .NET 10 运行库 API；
  接口默认实现已移除。
- `modinfo.json` 只保留目标 `Game.ModInfo` 的真实字段；`ApiVersion=1.42`、
  `ScVersion=2.4.10.6`、`Dependencies=[]`。删除 SCAPI schema 字段和目标加载器不存在的
  `LoadOrder`、`NonPersistentMod`、`GameplayImpactLevel`、`Settings`，没有通过改数字
  伪装二进制兼容。
- 上游 `tools/package.sh` 曾生成 `net10.0` 并写入未经本目标验证的 `1.44/2.4.40.6`；
  该入口现明确拒绝执行。Windows PowerShell 脚本是唯一 SCNET 打包入口。

## ABI 与调用时机

对固定目标程序集反编译/编译确认：

| 项目 | 结论 | 状态 |
| --- | --- | --- |
| 加载器/注册 | `Game.ModLoader.__ModInitialize()`；全局 `ModsManager.RegisterHook(string, ModLoader)` | 已确认 |
| 地形 Hook | `OnTerrainContentsGenerated(TerrainChunk)` | 已确认 |
| Hook 时机 | `GenerateChunkContentsPass3` 后、`GenerateChunkContentsPass4` 植被前，在 TerrainUpdater 地形线程 | 已确认 |
| 更新 Hook | `SubsystemUpdate(float)`，位于每次游戏更新循环末尾 | 已确认 |
| 地形生成器 | `ITerrainContentsGenerator` 含 OceanLevel/Height/Temperature/Humidity 与四个 pass | 已确认 |
| 方块实体 | `SubsystemBlockEntities.GetBlockEntity` 与 `CreateBlockEntityLogic` 存在；端口避免给 null miner 调用后者，按真实模板创建实体 | 已确认（ABI）；Android 行为待验证 |
| 生物 | `SubsystemCreatureSpawn` 与 `DatabaseManager.CreateEntity`/`ComponentSpawn` 路径存在 | 已确认（ABI）；Android 行为待验证 |
| 存储 | `Engine.Storage` 提供 CombinePaths/OpenFile/ReadAllText/WriteAllText/MoveFile | 已确认 |
| 网络角色 | `Game.NetWork.WorkType={Local,Server,Client}`；同时要求 `CommonLib.IsWorkTypeValid()` | 已确认 |

Local 与 Server 仅在角色有效时允许生成、开箱实体、守卫和持久化；Client 与角色不确定
状态均拒绝主动写入。是否由 Server 正确同步区块、箱子库存与生物到 7723 客户端仍为
待验证，不能由编译结果推断。

## 方块、方向值与世界生成安全

兼容性测试实际展开全部 14 个生成槽、23 个结构变体，并对每个结构 palette 和全部
战利品条目执行以下检查：目标程序集必须存在对应 `Game.<BlockName>`、类型必须派生自
`Game.Block`、公开静态 `Index` 必须在 0..1023；楼梯 data 限 0..7、半砖 0..1、
原木轴 0..2、梯子 0..3、火把 0..4、颜色 0..15；发射器、压力板、开关、起爆器、
导线/穿透导线和活塞还会逐位检查目标程序集定义的方向、模式、挂载面和初始状态。
其他值仍受 SC 18-bit terrain data 上限约束。未知方块或非法 data 会在世界绑定时使整个 Mod 的世界写路径拒绝启用，
不再默默映射为空气。

跨区块结构只写当前 16×16 新生成区块；每次 Hook 先计算结构格与地基格总写入量，超过
`32768` 时在第一格写入前拒绝整个区块。箱子每帧最多完成 8 个且最多检查 64 个，传入
标记每帧最多合并 128 个，埋伏点每帧轮转检查最多 64 个，避免无界主线程扫描。
方块实体与守卫只在 `SubsystemUpdate` 主更新 Hook 创建；地形线程只写当前 chunk 并向
线程安全队列投递标记。世界生成没有玩家触发的长事务，因此不提供中途取消/撤销；它
只作用于引擎正在生成的新区块，不修改已生成区块或主动打开玩家存档。

## 构建、测试与产物

执行命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File `
  .\SurvivalcraftRuins-Port\scripts\Build-NetMod.ps1 `
  -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release
```

结果：环境/哈希/ABI 检查通过；原测试与新增兼容性/安全测试共 `87/87` 通过；
`net472 Release` 为 0 警告、0 错误。包内容含 `SurvivalcraftRuins.dll`、
`modinfo.json`、`LICENSE`、`Assets/Structures/*.json`（13 个）和
`Assets/Loot/pools.json`，路径大小写已校验，且不含游戏 DLL、APK、PDB、
`Content.scpak` 或存档。

- 产物：`SurvivalcraftRuins-Port/artifacts/SurvivalcraftRuins.netmod`
- 大小：`335569` 字节
- SHA-256：`10350CBF9127BC43D1CB2F56C2E690E4103F73CA95A06C8C9BC2E44CC36A1FD1`
- 完整日志：`SurvivalcraftRuins-Port/artifacts/SurvivalcraftRuins-build.log`
- 校验文件：`SurvivalcraftRuins-Port/artifacts/SurvivalcraftRuins.sha256`

`artifacts/` 为生成物且被忽略，不提交 Git；提交中包含可重复生成它们的源码和脚本。

## 兼容矩阵与遗留问题

| 环境/能力 | 状态 | 说明 |
| --- | --- | --- |
| x25.01.17 Windows 固定程序集 ABI、net472 编译、测试、打包 | 已确认 | 证据见上方哈希、命令和日志 |
| x25.01.17 Windows 游戏内实际加载/进世界 | 待验证 | 本轮没有自动安装或启动游戏 |
| Android 7723 安装与加载 | 待验证 | 必须先比对 7723 程序集，再用真机日志确认 |
| Android 新世界 23 种结构、机关、方向渲染 | 待验证 | 特别检查楼梯、梯子、火把、活塞与发射器 |
| Android 箱子实体、分池战利品、保存重载 | 待验证 | 检查重复实体、库存同步和 JSON 持久化 |
| Android 守卫生成、超自然替代、水生寻路 | 待验证 | 检查帧耗时、触发一次性与重进世界 |
| 联机 Local | 待验证 | 代码路径允许，尚无运行证据 |
| 联机 Server/房主 | 待验证 | 代码路径允许，区块/实体/库存同步尚无真机证据 |
| 联机 Client 主动世界写入 | 不支持 | 明确拒绝；角色不确定时同样拒绝 |
| SCAPI `.scmod` 产物 | 不支持 | 本分支为 SCNET 专项移植，不混用二进制 |

## Android 7723 下一阶段步骤

1. 从 7723 安装包提取只用于本地核验的程序集名称、版本、SHA-256 与上述 API 表面；
   不提交 APK、DLL 或内容包。任一差异都按新目标重新编译，不能沿用 Windows 结论。
2. 备份后新建可丢弃单人世界，只安装本 SHA-256 的 `.netmod`；确认加载器识别名称、
   版本和依赖，保存启动及进世界日志。
3. 用固定 seed 跨区块巡查 23 种结构；核对方块方向、Pass 4 植被、地基、陷阱、箱子、
   战利品、守卫、帧耗时以及退出重进后的持久化。不得用玩家主存档首测。
4. 分别记录 Local、Server/房主、普通 Client。普通 Client 必须无主动地形/实体写入；
   Server 需验证区块、箱子库存、生物及触发状态能否同步。
5. 真机全部通过后才可把对应项目从“待验证”改为“已确认”，并记录精确 APK/程序集
   哈希、设备、步骤、日志和结果。

本移植不改变 Builder Assistant 的蓝图支持方块、族标签或 DATA 范围，因此
`docs/blueprint-block-reference.md` 无内容变更；不得把本 Mod 的 palette 当作 Builder
Assistant 的可导入蓝图能力。

## Android 7723 首轮实测修复（1.0.1）

2026-10-21 真机日志确认 1.0.0 能被加载，但进入世界后每次绑定均失败：
`Method not found: System.Linq.Enumerable.Repeat<T>(T,int)`。因此 1.0.0 在该 7723
运行时不会创建 `WorldState`，也不会生成任何遗迹；这不是生成概率或旧区块导致。

1.0.1 将结构注册表中的五处 `Enumerable.Repeat(...).Concat(...).ToArray()` 全部改为
本地数组填充，不再引用该运行时缺失的方法；构建脚本新增源码守卫，发现
`Enumerable.Repeat` 即拒绝打包。此修复不改变结构数量、战利品池顺序、生成概率、
权限规则或存档内容。桌面构建与测试通过后仍须在同一 Android 7723 客户端复测；在
出现 `[Ruins] Bound to world ...` 且新区块实际生成遗迹前，Android 世界生成功能保持
“待验证”。

1.0.1 最终构建通过 `86/86` 测试、`net472 Release` 0 警告 0 错误；IL 复核确认
`SurvivalcraftRuins.dll` 含本地 `RepeatPools` 且不再引用 `Enumerable.Repeat`。产物大小
`335181` 字节，SHA-256 为
`C407456D67147DBBFF3F87749E3B781F82A7CC4D4E5242DC82EF76E926D52CA5`。

## Android 7723 第二轮实测修复（1.0.2）

2026-10-21 真机复测 1.0.1 后，`Enumerable.Repeat` 异常已经消失，但日志确认世界绑定
仍失败于 `Missing structure template 'sky_fortress'`。包内容复核证明模板文件存在且路径
大小写正确；问题位于运行时资源发现：1.0.1 使用 `ModEntity.GetFiles(".json", ...)`
按扩展名遍历 ZIP，而 7723 实际没有把该必需模板交给回调。

1.0.2 不再依赖扩展名遍历，改为通过 `ModEntity.GetFile` 精确读取 13 个固定模板路径和
`Assets/Loot/pools.json`。任一文件缺失、大小写不符、读取失败、JSON 名称与文件名不符
或 loot 为空都会在初始化阶段一次性拒绝，不再留下部分资源集合到进世界后才失败。
同时移除运行 DLL 中剩余两处 LINQ `ToArray()` 扩展调用；构建阶段反编译最终 DLL，若
仍存在任何 `System.Linq.Enumerable` 方法引用即拒绝打包。包校验也从“模板数量为 13”
提升为逐项核对 13 个精确 ZIP 路径。永久性的世界绑定异常现在每个世界只记录一次，
避免按区块重复刷日志。

最终构建通过 `87/87` 测试（新增禁止扩展名遍历、只允许精确资源路径的加载回归测试）、
`net472 Release` 0 警告 0 错误；IL 守卫确认无任何 `System.Linq.Enumerable` 方法引用。
1.0.2 产物大小 `335569` 字节，SHA-256 为
`10350CBF9127BC43D1CB2F56C2E690E4103F73CA95A06C8C9BC2E44CC36A1FD1`。Android 上看到
`[Ruins] Loaded 13 structure templates`、`[Ruins] Bound to world ...` 并在新区块发现遗迹前，
世界生成仍保持“待验证”。
