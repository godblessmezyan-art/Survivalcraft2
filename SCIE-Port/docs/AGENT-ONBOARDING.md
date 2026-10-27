# SCIE-Port 快速上手交接文档（给接手的 Agent）

> 生成时间：2026-10-27 ｜ 当前 Mod 版本：`0.41.0` ｜ 主仓库 HEAD：`b74b80f1e295df0ccbbfd758b5245e678583faab`
> 目标游戏：Survivalcraft NET 2.4 `Net_x25.01.17` ｜ 最终平台：Android（7723 游戏盒）｜ 产物：`.netmod`
> 阅读本文前必须先读：仓库根 `AGENTS.md`、`SCIE-Port/AGENTS.md`、`SCIE-Port/docs/COMPATIBILITY.md`、`SCIE-Port/docs/AGENT-HANDOFF.md`。规则冲突时，离目标文件最近的规则优先。

---

## 0. 一句话项目定位

把获许可的第三方 Mod（SCIE Industrial Era、Kelly Expansion 等）移植到 SCNET x25.01.17，并开发原创内容（四圣兽神装、雷神之锤/苍穹之怒、昆古尼尔、各类载具）。游戏内品牌名固定为「荒野探险家 / Wilderness Explorer」（作者 zyan），但内部标识恒为 `SCIEPort`。`SCIE-Port/` 与 `src/BuilderAssistant/` 是两个完全独立的工程，禁止互相引用源码或资源。

---

## 1. GitHub 与 Git 信息（非机密部分）

| 项目 | 值 |
|---|---|
| 主开发仓库 | <https://github.com/godblessmezyan-art/Survivalcraft2.git> |
| Remote 名 | `origin` |
| 仓库所有者 / 公开账号 | `godblessmezyan-art` |
| 默认分支 | `main` |
| 本机 `git user.name` | `WeiXi88888888` |
| 本机 `git user.email` | `851964454@qq.com` |
| 凭据方式 | Windows **Git Credential Manager**（`credential.helper=manager`） |
| SCIE 上游仓库 | <https://github.com/Lixue9jiu/SCIE>（只读研究快照，见 `UPSTREAM-BASELINE.md`） |
| 本地开发根目录 | `D:\Dev\Survivalcraft2` |
| 本机游戏目录 | `D:\SC`（由开发者提供，见第 3 节；文档里的 `E:\SCNET\SCNETx25.01.17` 是历史路径） |

`SCIE-Port/` 现已被 Git 跟踪（约 340 个文件），克隆主仓库即可看到本项目。

---

## 2. 账号、密码、Token（机密部分——请这样处理）

**本仓库不保存、也不得保存明文密码 / Personal Access Token / SSH 私钥 / 设备验证码。** 这是 `AGENTS.md` 与 `AGENT-HANDOFF.md §2` 的硬性规定，也符合基本安全实践。

因此本文档**不含任何真实密码值**。生成它的助手也无法提供密码：本机 git 通过 Windows 凭据管理器认证，密钥存放在操作系统凭据库里，不在任何可读取的项目文件中。

接手 Agent 的正确做法：

1. **优先用已配置的 Git Credential Manager**（本机已就绪），无需手填密码：
   ```powershell
   git remote -v
   git credential-manager github login   # 仅在需要重新登录时
   git pull --ff-only
   ```
2. **自动化环境**只能用运行环境注入的 Token（如环境变量 `GITHUB_TOKEN`），且**严禁**写入：Git Remote URL、任何 `.md/.json/.ps1`/源码、提交信息、构建日志、测试报告、`.netmod` 包。
3. 如果项目所有者确实要把凭据交给某个 Agent，请填进**本地专用、已被 git 忽略**的模板：
   `SCIE-Port/docs/CREDENTIALS.local.md`（`.gitignore` 已加入 `*.local.md`，不会被提交）。
   推荐用 **Personal Access Token**（勾选 `repo` 权限）而不是账号登录密码；GitHub 早已不支持用账号密码做 git 推送。
4. 没有有效凭据又需要推送时：**停下来向项目所有者申请访问**，不得猜测、生成或复用他人密码。

> 安全提醒：把明文密码写进会被提交或转发的 `.md`，等同于公开泄露。请始终走凭据管理器 / 环境变量 / `*.local.md` 三条路径之一。

---

## 3. 固定运行环境

| 项目 | 固定值 |
|---|---|
| 游戏 | Survivalcraft NET 2.4 |
| 精确版本 | `Net_x25.01.17` |
| SCNET API | `1.42` |
| SCNET 内部版本 | `2.4.10.6` |
| Target Framework | `.NETFramework,Version=v4.7.2`（net472） |
| 构建 SDK | .NET SDK `9.0.300`（`global.json` 固定，`rollForward=latestFeature`），装在 `%LOCALAPPDATA%\Microsoft\dotnet` |
| 游戏目录（本机） | `D:\SC`（通过 `-GameDir` 或环境变量 `SCNET_GAME_DIR` / `SCNetGameDir` 提供） |
| Android 渠道 | 7723 游戏盒 |
| Mod 安装目录族 | `NetMods` |
| 产物 | `SCIE-Port/artifacts/SCIEPort.netmod` |

游戏程序集（`Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll` 等）由开发者本机提供，**不得提交、不得复制进仓库或 `.netmod`**。7723 的 Android 安装包可能被重打包，即使版本号相同也必须重新取证，不能继承 Windows 结论。

---

## 4. 标准构建 / 测试流程（本机已验证可用）

在 **bash** 里注意：Windows 路径的反斜杠会被吃掉，`-GameDir D:\SC` 要写成 `-GameDir 'D:\\SC'`；在 PowerShell 里直接用 `'D:\SC'` 即可。

```powershell
cd D:\Dev\Survivalcraft2\SCIE-Port

# 1) 首次或上游快照缺失时，拉取固定 SCIE 提交
.\scripts\Fetch-Upstream.ps1

# 2) 每次开发前：环境 + ABI + 保留 ID 检查
.\scripts\Test-Environment.ps1 -GameDir 'D:\SC'

# 3) Release 构建 + 离线规则测试 + 打包 + 包内容校验（推荐走固定 SDK 包装器）
.\scripts\Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release
#   ↑ 内部会设置 DOTNET_ROOT / DOTNET_ROLL_FORWARD 后转调 Build-NetMod.ps1
#   若已手动配好 SDK 环境，也可直接：.\scripts\Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release

# 4) 查看产物哈希
Get-FileHash .\artifacts\SCIEPort.netmod -Algorithm SHA256
```

`Build-NetMod.ps1` 会依次跑：环境/ABI 检查 → 品牌契约校验 → 各资源生成与结构校验 → 全部离线规则测试（FourSaints / Gungnir / Stormhammer / Skywrath / Ecology 等）→ net472 Release 编译（警告视为错误）→ XDB 数据库校验（`DATABASE VALIDATION PASSED`）→ 打包并输出条目清单与 SHA-256。任何一步失败都会 `throw` 并中止。

### 最新一次成功构建（v0.41.0，2026-10-27）

- 命令：`scripts/Test-Environment.ps1 -GameDir 'D:\SC'` + `scripts/Build-WithPinnedSdk.ps1 -GameDir 'D:\SC' -Configuration Release`，均 exit 0，0 warning / 0 error。
- 产物：`artifacts/SCIEPort.netmod`，**108 条目、3,501,301 bytes**，SHA-256 `C75D3139FAC5AFA7A737F599A74D2ADB77777B328919A4D2E9DB1944F1461F5C`。
- 包内 `modinfo.json` Version=`0.41.0`，DLL AssemblyVersion/FileVersion=`0.41.0.0`（两者必须一致，构建守卫会校验）。

---

## 5. 目录结构（关键部分）

```text
Survivalcraft2/                      仓库根
├─ AGENTS.md                         全局强制开发规范（先读）
├─ global.json                       固定 .NET SDK 9.0.300
├─ src/BuilderAssistant/             建筑助手工程（与 SCIE-Port 完全隔离）
└─ SCIE-Port/                        第三方移植 + 原创内容工程（本交接对象）
   ├─ AGENTS.md                      SCIE-Port 专属规范
   ├─ README.md                      入口与构建命令
   ├─ UPSTREAM-BASELINE.md           SCIE 固定提交记录
   ├─ THIRD-PARTY-NOTICES.md         上游资源与授权依据
   ├─ .gitignore                    忽略 artifacts/ bin/ obj/ upstream/SCIE/ *.local.md
   ├─ docs/                          兼容性、审计、路线图、测试证据（见下）
   ├─ scripts/                       构建、打包、资源生成、环境/数据库校验
   ├─ src/SCIEPort/
   │  ├─ Mod/SCIEPortLoader.cs       SCNET ModLoader 入口
   │  ├─ Features/                   按功能拆分（FourSaints / Watercraft / Rail /
   │  │                              LandVehicles / KellyPlants / Stormhammer / Gungnir …）
   │  ├─ Assets/                     Blocks/*.csv、Database/*.xdb、FourSaints.clo、Models、Textures
   │  ├─ modinfo.json                Name/Version/Description/PackageName=SCIEPort
   │  └─ SCIEPort.csproj             net472；Version 必须与 modinfo 一致
   ├─ tests/                         不依赖游戏进程的离线规则测试
   ├─ upstream/SCIE/                 Git 忽略的只读上游快照
   └─ artifacts/SCIEPort.netmod      生成物，不提交
```

### 必读文档

- `docs/COMPATIBILITY.md`：兼容性状态矩阵（状态词只用 `已确认` / `待验证` / `不支持`）。任何加载器类型、Hook 名、方法签名、`modinfo.json` 字段、包布局变更都要更新它。
- `docs/TEST-RESULTS.md`：每轮构建命令、结果、包字节/条目/SHA-256 的证据流水。
- `docs/AGENT-HANDOFF.md`：更细的历史交接、载具实现、禁区清单。
- `docs/PACKAGING-RULES.md`：品牌与打包契约（v0.40.3 起构建前后各校验一次）。
- 各功能审计：`FOUR-SAINTS-V1-AUDIT.md`、`GUNGNIR-V*-AUDIT.md`、`SKYWRATH-V*.md`、`VEHICLE-QUALITY-STANDARD.md`、`VEHICLE-MODEL-GUIDE.md`、`LARGE-VEHICLE-WEAPONS.md` 等。

---

## 6. 最近改动上下文：四圣兽神装 v0.41.0

四件衣物（复用原生衣物方块 ID 203，不新增方块）：

| 数据号 | 名称 | 槽位 | Sturdiness | MovementSpeedFactor | Layer |
|---:|---|---|---:|---:|---:|
| 200 | 万里青龙 | Head | 500 | 1.0 | 10 |
| 201 | 北冥玄武 | Torso | 500 | 1.0 | 10 |
| 202 | 镇狱白虎 | Legs | 500 | 1.2 | 10 |
| 203 | 炽焰朱雀 | Feet | 500 | 1.5 | 10 |

- 衣物数据有两条来源：权威 `src/SCIEPort/Assets/FourSaints.clo`，以及 `.clo` 合并失效时的代码兜底 `Features/FourSaints/FourSaintsClothing.cs`。**两处必须同步改**，`Layer` 统一 10、`IsOuter=True`（构建守卫强制）。
- 体温钳制（修复“穿整套仍会低温感冒”）：`SubsystemFourSaints.ApplyTemperature`（`UpdateOrder.Locomotion`，仅 `Authority`）每帧调用纯函数 `FourSaintsRules.ClampTemperature`。北冥玄武把体温下限抬到 8（高于感冒发病门限 6），炽焰朱雀把上限压到 8（远低于过热伤害 24），同穿钉在 8。
- 体温取证结论（x25.01.17）：中性 12；感冒 `ComponentFlu` 在体温持续 <6 时累积发病；严寒伤害 idx26(<3)/idx25(≤0)；过热 idx35(>20/24)。只拦伤害索引 25/26 **挡不住感冒**，必须钳体温 ≥8。
- 构建守卫红线：`SubsystemFourSaints.cs` **必须含** idx25/idx26 严寒原因字符串，**必须不含** idx35 或 `ComponentFlu.fName`。所以钳制走公开属性 `ComponentVitalStats.Temperature`（`System.Single`，public get/set，已反射核实），不碰 `ComponentFlu`。

---

## 7. 编码与架构禁区（违反=返工）

- 只按 x25.01.17 真实程序集 ABI 开发；不按官方原版 / SCAPI / 旧 SCIE DLL / 猜测接口实现。每个接口都要回目标程序集验证。
- 不替换 `ComponentPlayer`，不迁移 `ComponentNPlayer`；不复制/加载旧 SCIE DLL、SCPluginAPI DLL。
- 不重打包游戏本体、APK、游戏 DLL、PDB、`Content.scpak`；不提交专有资源。
- 不硬编码用户存档路径、设备 ID、账号或凭据。
- 不改内容 ID 999/1000、方块索引区间、衣物数据号 200–203、GUID、网络包 240–245，除非先完成冲突迁移与存档方案。
- `.scmod` 与 `.netmod` 严格分离，不得重命名冒充。
- XDB 根节点必须是 Mod 容器（如 `<SCIEPortWatercraft>`），不能是 `<DatabaseObjects>`；自定义根内部不得再嵌套 `<DatabaseObjects>`。原版文件夹（Entities/Gameplay/BlockBehaviors）必须用原版 GUID。
- 资源路径用 `/`，包内大小写必须与代码/XDB 完全一致。
- 未编译的代码不得标“完成”；未在 Android/7723 实测的构建不得标“兼容”。“能运行/能加载/编译成功”一律不等于完成。
- 所有功能优先适配触屏，不依赖键盘、鼠标悬停或桌面专用路径。
- 联机必须区分 Local / Server / Client 权限；没有证据不得假定客户端写入会同步。
- 地形/大规模操作必须有坐标边界、数量上限、每帧预算、可取消、可撤销；首次验证用新建可丢弃单人世界 + 备份，禁止直接动玩家主存档。
- 不改动 Builder Assistant 来解决 SCIE-Port 问题。

---

## 8. 安装与实机测试

1. `NetMods` 里只能有一个 `SCIEPort`；测新包前先删旧包，否则 SCNET 报重复安装并可能禁用 Mod。
2. 用新建、可丢弃、已备份的单人世界；启动后保存完整加载日志，确认无 XDB/CSV/模型/纹理错误。
3. 分别记录 Windows、Android 7723、多人主机、普通客户端、观察者的结果，**不得合并描述**。
4. 报错时保存 `Database not loaded` 等外层消息**之前的首个内部异常**。

---

## 9. Agent 完成任务后的交接清单（必须逐条报告）

1. 改了哪些文件、为什么；
2. 用的上游 Commit 与目标程序集哈希；
3. 实际运行的构建/测试命令；
4. Release 是否 0 warning / 0 error；
5. `.netmod` 字节数、条目数、SHA-256；
6. Windows 是否实际运行；
7. Android 7723 是否实际运行；
8. 多人主机 / 普通客户端是否分别测试；
9. 新增的存档 / 资源 / 许可 / 安全风险；
10. 尚未完成项与下一步最小可验证任务。

状态词只允许：`已确认` / `待验证` / `不支持`。每次代码变更都要同步更新 `docs/TEST-RESULTS.md`，涉及 ABI/加载器/签名/包布局还要更新 `docs/COMPATIBILITY.md`。

---

## 10. 许可

SCIE 公开仓库与固定提交中未发现 `LICENSE`/`COPYING`；项目所有者声明已获明确许可，依据记录在 `THIRD-PARTY-NOTICES.md`。不得自行把项目标为 MIT/GPL 或其他未明确授予的许可证。Kelly、SCIE 等外部 Mod 的代码/数据库/美术不得放入或打包进 Builder Assistant。
