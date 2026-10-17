# SCIE-Port Agent 开发交接手册

> 更新时间：2026-10-17
> 当前移植版本：`0.5.1`
> 目标游戏：Survivalcraft NET 2.4 `Net_x25.01.17`
> 最终平台：Android（7723 游戏盒版本）
> 产物格式：`.netmod`

## 1. 开工前必须知道的事情

1. 先阅读仓库根目录 `AGENTS.md`、本目录上级的 `SCIE-Port/AGENTS.md`，以及
   `SCIE-Port/docs/COMPATIBILITY.md`。距离目标文件最近的规则优先适用于 SCIE-Port。
2. `SCIE-Port/` 是独立移植工程，不属于 Builder Assistant。禁止引用、修改或打包
   `src/BuilderAssistant/` 的源码和资源。
3. 当前 `SCIE-Port/` 在主仓库中仍显示为 **未跟踪目录**（`?? SCIE-Port/`）。在它被
   正式提交并推送之前，其他 Agent 只克隆 GitHub 仓库将看不到本项目。
4. 只允许按 `E:\SCNET\SCNETx25.01.17` 的真实程序集 ABI 开发。不得按官方原版、
   SCAPI、旧 SCIE DLL 或猜测的接口实现。
5. “编译通过”不等于“Windows 可运行”，更不等于“Android 兼容”或“多人兼容”。

## 2. GitHub 与 Git 信息

### 主开发仓库

- 地址：<https://github.com/godblessmezyan-art/Survivalcraft2.git>
- Remote：`origin`
- 当前分支：`main`
- 本文生成时主仓库 HEAD：`77e9b8ede3c3381c914ef0b756c6121e94ca6b88`
- Remote 所有者/公开账号：`godblessmezyan-art`
- 本机 `git user.name`：`851964454@qq.com`
- 本机 `git user.email`：`851964454@qq.com`

### SCIE 上游仓库

- 地址：<https://github.com/Lixue9jiu/SCIE>
- 移植基线 Commit：`4864d956793b6e670413019f1f6b1cc5c8bb0970`
- 本地只读快照：`SCIE-Port/upstream/SCIE/`
- 获取脚本：`SCIE-Port/scripts/Fetch-Upstream.ps1`
- 每次研究必须确认 `git -C upstream/SCIE rev-parse HEAD` 与固定 SHA 一致。

### 账号、密码和 Token

本仓库 **不保存、也不得保存明文密码、Personal Access Token、SSH 私钥或设备验证码**。
本文档中没有密码值；其他 Agent 也不得向日志或聊天输出凭据。

推荐使用 Windows Git Credential Manager 或已配置的 SSH Key：

```powershell
git credential-manager github login
git remote -v
```

如自动化环境必须使用 Token，只能由运行环境注入，例如 `GITHUB_TOKEN`，不得写入：

- Git Remote URL；
- `.md`、`.json`、`.ps1`、源码或配置文件；
- 提交信息、构建日志和测试报告；
- `.netmod` 包。

需要推送但没有有效凭据时，应停止并向项目所有者申请访问，不得猜测、生成或复用他人密码。

## 3. 固定运行环境

| 项目 | 固定值/状态 |
|---|---|
| 游戏 | Survivalcraft NET 2.4 |
| 精确版本 | `Net_x25.01.17` |
| SCNET API | `1.42` |
| SCNET 内部版本 | `2.4.10.6` |
| Target Framework | `.NETFramework,Version=v4.7.2` |
| Windows 游戏目录 | `E:\SCNET\SCNETx25.01.17` |
| Android 渠道 | 7723 游戏盒 |
| Mod 目录族 | `NetMods` |
| 产物 | `SCIE-Port/artifacts/SCIEPort.netmod` |

已记录的 Windows 程序集 SHA-256：

| 文件 | SHA-256 |
|---|---|
| `Survivalcraft.exe` | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` |
| `Engine.dll` | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` |
| `EntitySystem.dll` | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` |

这些哈希只证明 Windows 参考程序集。7723 Android 包仍必须单独取证。

## 4. 项目结构

```text
SCIE-Port/
├─ AGENTS.md                         项目级强制规则
├─ README.md                         项目入口和构建命令
├─ UPSTREAM-BASELINE.md              SCIE 固定提交记录
├─ THIRD-PARTY-NOTICES.md            上游资源与授权依据记录
├─ audit/
│  └─ SCIE.Legacy.TargetProbe.csproj 旧源码对 x25 的兼容错误探针
├─ docs/
│  ├─ AGENT-HANDOFF.md               本文档
│  ├─ API-DIFF.md                    旧 API 与 x25 实际接口差异
│  ├─ COMPATIBILITY.md               兼容性状态矩阵
│  ├─ DEPENDENCIES.md                第三方依赖和分发边界
│  ├─ PORTING-AUDIT.md               源码、资源、模块审计
│  ├─ PORTING-ROADMAP.md             分阶段路线图
│  └─ TEST-RESULTS.md                构建和实测证据
├─ scripts/
│  ├─ Build-NetMod.ps1               Release 构建、资源加入和包校验
│  ├─ Fetch-Upstream.ps1             获取固定 SCIE 提交
│  ├─ Test-Environment.ps1           程序集、ABI 和 ID 检查
│  └─ Test-LegacyTargetProbe.ps1     旧源码兼容审计
├─ src/SCIEPort/
│  ├─ Assets/
│  │  ├─ Blocks/SCIEPort.csv         测试材料注册
│  │  ├─ Blocks/Watercraft.csv       四种载具与铁轨物品注册
│  │  └─ Database/Watercraft.xdb     载具实体和组件模板
│  ├─ Features/
│  │  ├─ Materials/                  最小材料加载探针
│  │  └─ Watercraft/
│  │     ├─ SteamBoatBlock.cs
│  │     ├─ SubsystemSteamBoatBlockBehavior.cs
│  │     └─ ComponentSteamBoatDrive.cs
│  │  └─ Rail/
│  │     ├─ RailBlock.cs
│  │     ├─ RailVehicleBlocks.cs
│  │     ├─ SubsystemRailBlockBehavior.cs
│  │     ├─ SubsystemRailVehicleBlockBehavior.cs
│  │     └─ ComponentRailVehicle.cs
│  ├─ Mod/SCIEPortLoader.cs          SCNET ModLoader 入口
│  ├─ modinfo.json
│  └─ SCIEPort.csproj
└─ upstream/SCIE/                    Git 忽略、只读研究快照
```

`artifacts/`、`bin/`、`obj/` 都是生成物，不提交。游戏 DLL、APK、PDB、内容包和
用户存档不得进入仓库或 `.netmod`。

## 5. 当前实现：SCIE 四种交通载具

固定 SCIE 提交中交通实体为 `SteamBoat`、`Airship`、`Train` 和 `Carriage`。当前 0.5.1 包含：

- 独立物品 `SteamBoatBlock`，内容 ID `999`；
- 独立实体模板 `SCIEPortSteamBoat`；
- 原生 `ComponentBoat`、`ComponentMount`、`ComponentBody`、`ComponentDamage`；
- `SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)` 生成路径；
- 单人 `WorkType.Local` 下的有限额外推进；
- 水平速度上限 `15`，额外加速度 `8`；
- 8 格范围最多 4 艘的生成限制；
- SCIE 原 `SteamBoat.dae` 和 `SteamBoat.png`；
- 模型必须保留网格节点名 `Cylinder`；
- 原船体参数：`BoxSize=2,0.5,4.5`、`Mass=250`、`Density=0.2`；
- 驾驶座偏移：`0,0.85,-0.8`。

飞艇部分包含内容 ID `998`、实体 `SCIEPortAirship`、Y_UP 派生 `Airship.dae`、白色运行时纹理、
单人触屏推进/转向/视角升降、速度上限 12、浸水损坏和附近最多 2 艘限制；0.5.1 已缩小驾驶舱碰撞体以恢复原生骑乘搜索可达性。

轨道部分包含内容 ID `997` 的十形态铁轨、ID `996` 火车、ID `995` 车厢、邻接自动成形、
仅在轨道生成、单人沿轨导向和触屏驾驶。车厢目前是独立低速试乘实体；自动编组、货箱和燃料未实现。

构建脚本从固定上游快照加入以下资源，不在 `src/` 中维护重复副本：

```text
upstream/SCIE/IndustrialMod/Models/SteamBoat.dae
upstream/SCIE/IndustrialMod/Textures/SteamBoat.png
upstream/SCIE/IndustrialMod/Models/Airship.dae
upstream/SCIE/IndustrialMod/Models/Train.dae
upstream/SCIE/IndustrialMod/Models/Minecart.dae
upstream/SCIE/IndustrialMod/Textures/Train.png
```

资源哈希：

- DAE：`E3C2F54513D10486FA14927418E6ABB76CB7AC270B947A57C338A2423911C4F8`
- PNG：`BB0CC5F9886E30BA9DE12D79F1C7724CBF9C6341DCE7606C65906441ABE8F6A4`
- Airship DAE：`FAE480B6DFC30A3BB88BD6D9D1A7A4368CC2193E5E95C518A718310AB7A2BD47`
- Train DAE：`6E9AF8F38F939F2889FF00CA14AE8DD6093CC86A87CC9BDA3D2ECC4B74E2A0D4`
- Minecart DAE：`CDB55F28AA29BAB7067DF8D68217953BB70EB8D3544BC10A337C2BDD639D88F5`
- Train PNG：`413E44943BA96517FB0B1712EB0ACC157CE60E14BE7B96C7D0B3FB37C3A5CD57`

## 6. 已发现并修复的重要问题

### 0.3.0：`Database not loaded`

0.3.0 的 `Watercraft.xdb` 错误地使用了：

```xml
<DatabaseObjects>
```

目标 `ModsManager.CombineDataBase` 会把 Mod XDB 根节点的直接子节点加入原版
`DatabaseObjects`。上述写法会产生嵌套的 `DatabaseObjects`，使数据库反序列化失败，
进入世界后显示 `Database not loaded.`。

0.3.1 已改为：

```xml
<SCIEPortWatercraft>
```

构建脚本现在会拒绝错误根节点。任何 Agent 都不得把它改回 `DatabaseObjects`。

### 安装升级注意事项

`PackageName` 固定为 `SCIEPort`。测试新包前必须删除旧版本，确保 `NetMods` 中只存在一个
`SCIEPort`；否则 SCNET 会报告重复安装并可能禁用 Mod。

## 7. 尚未实现或尚未验证

| 能力 | 当前状态 | 说明 |
|---|---|---|
| Release 编译 | 已确认 | 0.5.1 为 0 warning / 0 error |
| XDB 根格式修复 | 已确认 | 由 x25 `CombineDataBase` 真实实现确认 |
| Windows 进入世界 | 待验证 | 0.3.1 尚缺完整运行日志 |
| Android 进入世界 | 已确认 | 用户确认 0.3.1 已修复数据库加载错误；完整日志仍待归档 |
| Android 四种载具和铁轨 | 待验证 | 0.5.1 飞艇/火车坐标轴修复及轨道功能待用户复测 |
| 模型与 UV 实际渲染 | 待验证 | 文件、网格和纹理哈希已确认，实机画面未确认 |
| 生成、乘坐和驾驶 | 待验证 | 需要新建可丢弃单人世界测试 |
| 保存退出和重新进入 | 待验证 | 必须确认船实体可恢复且无模板错误 |
| 19 格蒸汽机库存 | 不支持 | 尚未移植 |
| 燃料、热量和工业 UI | 不支持 | 尚未移植 `ComponentEngine` 依赖链 |
| 多人权威驾驶同步 | 不支持 | 自定义推进仅允许 `WorkType.Local` |
| 双座 | 不支持 | 当前 SCIE 蒸汽船本身为单座；原生 Mount 协议没有座位号 |
| 火车燃料、车厢编组/货箱 | 不支持 | 依赖后续工业机器、库存和父体同步链 |

不要用删除功能、空实现、伪造类或屏蔽异常的方式改变这些状态。

## 8. 标准开发和构建流程

```powershell
cd E:\Survivalcraft2\SCIE-Port

# 首次或上游快照缺失时
.\scripts\Fetch-Upstream.ps1

# 每次开发前检查真实环境
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'

# Release 构建和 netmod 打包
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'

# 查看产物哈希
Get-FileHash .\artifacts\SCIEPort.netmod -Algorithm SHA256
```

当前 0.5.1 构建记录：

- 文件：`SCIE-Port/artifacts/SCIEPort.netmod`
- 大小：136,966 字节
- SHA-256：`68DD8F38C7962953C655BC84A3CECD542F7E2063E21E22CF70D06E036A1070A6`
- 包内条目：

```text
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Database/Watercraft.xdb
Assets/Models/Airship.dae
Assets/Models/Minecart.dae
Assets/Models/SteamBoat.dae
Assets/Models/Train.dae
Assets/Textures/SteamBoat.png
Assets/Textures/Train.png
modinfo.json
SCIEPort.dll
THIRD-PARTY-NOTICES.md
```

## 9. 实机测试流程

只使用新建、可丢弃并已备份的单人世界：

1. 删除旧 `SCIEPort` 包和重复安装记录。
2. 安装最新 `SCIEPort.netmod`。
3. 启动后保存完整加载日志，确认没有 XDB、CSV、模型和纹理错误。
4. 进入创造模式，搜索 `SCIE 蒸汽船（授权移植预览）`。
5. 对水面使用，确认物品减少且只生成一个实体。
6. 验证触屏前进、后退、转向、松手减速、下船和碰撞。
7. 在附近生成第 5 艘，确认 4 艘限制提示生效。
8. 保存退出并重新进入，确认世界、库存和船实体恢复。
9. 分别记录 Windows、Android、多人主机和普通客户端结果，不得合并描述。

发生错误时必须保存错误详情以及 `Database not loaded` 等外层消息之前的首个内部异常。

## 10. 许可和再分发

SCIE 公开仓库与固定提交中没有发现 `LICENSE`/`COPYING`。项目所有者已声明获得明确许可，
当前依据记录在 `THIRD-PARTY-NOTICES.md`。公开发布前仍应归档权利人许可原文或可核验链接，
并确认覆盖：

- 源码修改；
- 模型和纹理修改；
- `.netmod` 二进制打包；
- 公开再分发；
- 商业或非商业使用限制。

不得自行把项目标记为 MIT、GPL 或其他未明确授予的许可证。

## 11. 编码与架构禁区

- 不替换 `ComponentPlayer`，不迁移 `ComponentNPlayer`。
- 不复制或加载旧 SCIE DLL、SCPluginAPI DLL。
- 不重新打包游戏本体、APK、游戏 DLL、PDB 或 `Content.scpak`。
- 不硬编码用户存档路径、设备 ID、账号或凭据。
- 不在多人客户端直接假定物理写入具有服务器权威性。
- 不改动 Builder Assistant 来解决 SCIE-Port 问题。
- 不改变内容 ID 999/1000，除非先完成冲突迁移和存档方案。
- 不改动 `Cylinder` 网格节点名，除非同步修改并实测所有模型引用。
- XDB 根节点必须是 Mod 容器，不能是 `DatabaseObjects`。
- 资源路径使用 `/`，包内大小写必须与代码/XDB 完全一致。

## 12. Agent 完成交接清单

每个 Agent 完成任务后必须报告：

1. 修改了哪些文件及原因；
2. 使用了哪个上游 Commit 和目标程序集哈希；
3. 实际运行的构建/测试命令；
4. Release 是否 0 warning / 0 error；
5. `.netmod` 文件大小、条目和 SHA-256；
6. Windows 是否实际运行；
7. Android 是否实际运行；
8. 多人主机和普通客户端是否分别测试；
9. 新增的存档、资源、许可或安全风险；
10. 尚未完成和下一步最小可验证任务。

状态词只允许使用：`已确认`、`待验证`、`不支持`。
