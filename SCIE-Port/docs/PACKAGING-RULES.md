# `.netmod` 品牌与打包契约

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
| 方块索引 | 903–948、995–1000 | 存档按 `BlockIndex` 存方块，改号等于毁档 |
| 衣物 `ClothingData.Index` | 200–203 | 同上 |
| 网络包 ID | 240–245 | 联机协议，改动即与旧版本不兼容 |
| XDB 容器 / 模板 GUID | 现状 | 数据合并按 GUID 匹配 |
| `THIRD-PARTY-NOTICES.md` | 必须随包 | 上游署名与授权范围 |

`SCIE-Port` 继续作为**仓库工作区目录名**与**内部标识**存在；它不再作为玩家可见名称，也不再追加任何"移植版""预览版""Audit Build"之类后缀。

## 3 名称规范（物品 / 方块）

- 创造栏与物品提示默认保留上游原名；上游原名本身带括号时照抄，不额外包裹。
- 禁止在 CSV 的 `DefaultDisplayName` 里写发布状态后缀：`（移植）`、`（授权移植预览）`、`(Port)`、`Audit Build`。
  移植状态写进 `docs/`，不污染玩家可见名称。
- 新增方块前必须比对原版与本工作区全部 `Block.Index`，并在 `docs/COMPATIBILITY.md` 记录占用区间。

## 4 版本一致性

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

## 6 变更流程

需要改动品牌或内部标识时，按以下顺序，缺一不可：

1. 先改本文件与 `AGENTS.md` 的规则；
2. 再改 `scripts/Build-NetMod.ps1` 的校验值（两处：打包前常量 + 打包后比对）；
3. 同步 `modinfo.json`、`SCIEPort.csproj`、`README.md`；
4. 在 `docs/COMPATIBILITY.md` 与 `docs/TEST-RESULTS.md` 记录本次包大小、条目数、SHA-256 与 `modinfo` 版本；
5. 完整跑一次 `Build-NetMod.ps1`，把结果写进文档后才允许发布。

> 只改品牌显示名（`Name` / `Author` / `Description`）属于**展示层**变更，不动任何 ID 与存档逻辑；
> 改 `PackageName`、`AssemblyName`、方块索引、GUID 或网络包 ID 属于**破坏性**变更，必须单独评审并在兼容性矩阵里明确标注为不兼容。
