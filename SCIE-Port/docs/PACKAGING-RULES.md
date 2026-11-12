# `.netmod` 品牌与打包契约

2026-11-12 / 0.83.0：所有入口统一构建SCIEPort，书典源码/资源在本工程内；Fast旧名称仅作转发，发布仍执行环境/回归/包校验。取消绘卷237包/注册，客户端直用原生ID7，不改变238/239及240–254协议。用户本次已授权合并并正常push main；已有Release核对提交和资产哈希后复用，不覆盖不同版本/资产。

> **2026-11-08 最新发布覆盖规则：**用户要求自动 Release 不运行测试或包校验，改用 `scripts/Build-WildernessExplorerFast.ps1` 和 `scripts/Publish-WildernessExplorerRelease.ps1`。下文的完整打包验证只用于历史/手动的 `Build-NetMod.ps1`，不阻塞快速发布。快建可能跳过历史资源转换，且不证明 Android/联机兼容。详见根 `docs/NETMOD-RELEASE.md`。

> **最新统一发布目标（2026-11-08）**：「荒野探险家」与「云天书典」未来合为**一个 .netmod**，保持原 SCIEPort 稳定内部身份、兼容性与物品映射。以下历史约束仍适用于当前荒野探险家现有包；**目前尚未实际整合书典源码、资源及注册**，旧单包规则不等于已经满足新统一产品要求。正式整合时，须验证 956/DATA0、CSV/XDB/GUID、模组加载和 Android/SCNET；统一发布规则见仓库 `docs/UNIFIED-MOD-POLICY.md`。

2026-11-06 / 0.78.0 PixelFX：包内沿用`Assets/Textures/PixelFxStormhammer.png`（现为1024×2048、186帧）与同名JSON及`PixelFX-LICENSE.txt`，支持多神器共用，条目路径集合保持。`Test-PixelFx.ps1`在构建前/编译后/打包后验证native RGBA、JSON/C#表与包字节、原帧保留/来源许可；禁止离线JS/工具/源码进入运行包。外部中文包名、内部SCIEPort身份和原3D资源保持。

本文件定义 SCIE-Port 产物（`.netmod`）的**强制**打包规则，自 v0.36.0 起生效，v0.40.3 起由
`scripts/Build-NetMod.ps1` 在打包前后各校验一次。任何一条不满足都会让构建直接失败，不允许"先出包再补文档"。

## 1 品牌（游戏内可见）

| 字段 | 固定值 | 说明 |
|---|---|---|
| `modinfo.Name` | `荒野探险家` | 游戏内 Mod 管理、创造栏与加载提示显示的名字 |
| `modinfo.Author` | `zyan` | 作者 |
| `modinfo.Description` | 必须同时含 `荒野探险家` 与 `Wilderness Explorer` | 简介首句即品牌名中英对照，随后才是能力概述与版本要点 |

英文名 **Wilderness Explorer** 是品牌的官方英文写法，出现在简介与 README，不改写为其它拼写。

## 2 内部标识（一律不得随品牌改名）

| 标识 | 固定值 | 原因 |
|---|---|---|
| `modinfo.PackageName` | `SCIEPort` | 决定模组目录名与存档归属，改动等于换模组，旧存档全部失效 |
| `AssemblyName` / `RootNamespace` | `SCIEPort` | 程序集名与命名空间，改动会破坏 ModLoader 反射加载 |
| 方块索引 | 现有 903–952、995–1000（有预留空号） | 保留所有既有值；main 0.47.0 合入 950 吞噬之眼与 952 阿比斯，不按连续区间自动注册 |
| 衣物 `ClothingData.Index` | 200–203 | 同上 |
| 网络包 ID | 238（座位）、239（家居）、240–254（既有）；绘卷使用原生7 | 绘卷237已停用，旧包布局保持；不挪用家居239 |
| XDB 容器 / 模板 GUID | 现状 | 数据合并按 GUID 匹配 |
| `THIRD-PARTY-NOTICES.md` | 必须随包 | 上游署名与授权范围 |

`SCIE-Port` 继续作为**仓库工作区目录名**与**内部标识**存在；它不再作为玩家可见名称，也不再追加任何"移植版""预览版""Audit Build"之类后缀。

## 3 名称规范（物品 / 方块）

- 创造栏与物品提示默认保留上游原名；上游原名本身带括号时照抄，不额外包裹。
- 禁止在 CSV 的 `DefaultDisplayName` 里写发布状态后缀：`（移植）`、`（授权移植预览）`、`(Port)`、`Audit Build`。
  移植状态写进 `docs/`，不污染玩家可见名称。
- 新增方块前必须比对原版与本工作区全部 `Block.Index`，并在 `docs/COMPATIBILITY.md` 记录占用区间。
- v0.44.0 起全部当前及新增物品使用独立的 `SCIEPort.*` 原生分类键，中文显示为神器/载具/圣兽套装等；全部神器、四圣兽分别有主题槽位背景，见 [INVENTORY-STANDARD.md](INVENTORY-STANDARD.md)。不得重新混入原版分类。

### 3.1 外部产物文件名（v0.44.0 起强制）

- 固定新交付名：`荒野探险家.netmod`；同字节版本副本：`荒野探险家-<Version>.netmod`。
- 临时压缩文件：`荒野探险家.zip`，不作为安装包交付；历史 `SCIEPort.netmod` 可保留为旧版记录/只读恢复来源，禁止作为新包名。
- 包内仍为 `SCIEPort.dll`；`PackageName`、程序集名、命名空间与模组存档归属不改。中文文件名不是改模组身份。
- 构建后比较主包/版本副本哈希，拒绝文件名漂移；中文 PowerShell 脚本需兼容 Windows PowerShell 5.1（UTF-8 BOM），modinfo JSON 保持 UTF-8 无 BOM。

## 4 版本一致性

2026-11-08用户将整合设为长期规则：荒野探险家与云天书典后续全部直接在本地main开发、验证和提交，共用SCIEPort工程、加载器、modinfo及一个版本号；不得再拆分开发/发布独立书典包。常规任务不新建功能分支/worktree，用户当次明确指定时除外。根书典脚本仅转发统一构建，交付始终只有本契约规定的荒野探险家主包及版本副本；此规则不合并Builder Assistant等独立项目。

2026-11-08用户要求开发完成的`.netmod`默认上传到对应GitHub Release：先通过构建和内容校验，核验版本、目标仓库、可访问的准确源码提交及已有资产，再上传并校验下载文件哈希。源码推送仍由用户执行；若GitHub尚无对应提交，保留包并报告等待用户推送，不将其错误关联至其他提交。

0.77.0内置云天书典：只打包一个SCIEPort.dll及现有modinfo；必须存在`Assets/Blocks/CelestialCodex.csv`、`Assets/Database/CelestialCodex.xdb`、`Assets/Textures/CelestialCodex/Book.png`，禁止另带CelestialCodex.dll或独立modinfo。956/DATA0与原书典三GUID保留，Class改为本程序集中的命名空间。独立CelestialCodex包与整合版同时安装不支持，存在ID/GUID重复；安装要求见[云天书典](CELESTIAL-CODEX.md)。

`SCIEPort.csproj` 的 `Version` / `AssemblyVersion` / `FileVersion` 与 `modinfo.json` 的 `Version` 必须完全一致，
由构建脚本比对；`.netmod` 内 `modinfo.json` 会在压缩后再读一次，确认没有被改写。

## 5 校验点

`scripts/Build-NetMod.ps1` 依次执行：

1. `Test-Environment.ps1`：目标程序集哈希、API 1.42、SC 2.4.10.6、net472。
2. **品牌契约（打包前）**：`Name`、`Author`、`PackageName`、`AssemblyName`/`RootNamespace`、
   `Version` 一致性、`Description` 中英名、`THIRD-PARTY-NOTICES.md` 存在、CSV 名称无状态后缀。
3. 全部离线规则套件、资源守卫、XDB 校验、`net472 Release` 构建。
4. **品牌契约（打包后）**：从 `.netmod` 内重新读取 `modinfo.json`，再校验 `Name`/`Author`/`PackageName`/`Version`。
5. 必需条目、禁用条目（如 `KellyExpansion.webp`）、纹理解析格式、方块 ID 与网络包 ID 唯一性。
6. 独立 Tab/神器专属背景、原版衣物 DATA 隔离、所有可创造变体与实际分类 getter、注册幂等与中文标签；主包/版本副本命名与哈希一致。

## 6 变更流程

需要改动品牌或内部标识时，按以下顺序，缺一不可：

1. 先改本文件与 `AGENTS.md` 的规则；
2. 再改 `scripts/Build-NetMod.ps1` 的校验值（两处：打包前常量 + 打包后比对）；
3. 同步 `modinfo.json`、`SCIEPort.csproj`、`README.md`；
4. 在 `docs/COMPATIBILITY.md` 与 `docs/TEST-RESULTS.md` 记录本次包大小、条目数、SHA-256 与 `modinfo` 版本；
5. 完整跑一次 `Build-NetMod.ps1`，把结果写进文档后才允许发布。

> 只改品牌显示名（`Name` / `Author` / `Description`）属于**展示层**变更，不动任何 ID 与存档逻辑；
> 改 `PackageName`、`AssemblyName`、方块索引、GUID 或网络包 ID 属于**破坏性**变更，必须单独评审并在兼容性矩阵里明确标注为不兼容。
