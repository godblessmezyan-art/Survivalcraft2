# Builder Assistant for Survivalcraft NET

面向 Android 版《生存战争 2.4》联机客户端（Survivalcraft NET / SCNET）的建筑辅助 Mod。首要适配版本是 **Net_x25.01.17**，目标产物为 `.netmod`。

> 当前状态：Phase 1 最小加载已由用户在 Android/7723 客户端确认。v0.4.0 已接入正式快艇贴图，提高推进和速度上限，并开放联机生成与加速测试；联机同步效果仍待主机和客户端分别验证。

## 项目目标

- Android 触屏优先的功能入口。
- 小范围、有限额、可撤销的批量建造。
- 区域选择、复制、粘贴、旋转与建筑蓝图。
- 渐进式执行，避免一次修改大量方块导致卡顿或损坏存档。
- 明确区分单机、主机和联机客户端权限。

## 支持范围

| 运行环境 | 状态 | 说明 |
| --- | --- | --- |
| SCNET Net_x25.01.17 Windows 程序集 | 已确认/已编译 | 用于 ABI 校验与构建引用 |
| SCNET Net_x25.01.17 Android 官方发布 APK | 待实机测试 | 尚未完成安装与加载验证 |
| 7723 渠道 Android 安装包 | 最小加载已确认 | 用户实机确认安装/加载；程序集指纹和功能 API 仍待验证 |
| SCAPI / `.scmod` | 不支持 | 加载器 ABI 与包格式不同 |
| 官方原版 Survivalcraft | 不支持 | 不包含已确认的 SCNET ModLoader |

详见 [兼容性矩阵](docs/COMPATIBILITY.md)。

## 构建

要求：Windows、PowerShell 5.1+、.NET SDK，以及 .NET Framework 4.7.2 Targeting Pack。

```powershell
./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
```

成功后生成：`artifacts/BuilderAssistant.netmod`。脚本只引用本机游戏程序集，不会将其复制进仓库或 Mod 包。

## 安装

1. 备份测试存档。
2. 将 `BuilderAssistant.netmod` 复制到客户端的 `NetMods` 目录。
3. 在游戏设置中启用 Mod 加载（若该构建默认关闭）。
4. 首次仅使用新建的单人测试世界启动。
5. 检查游戏日志是否出现 `[BuilderAssistant] Loaded`。

Android 的实际 `NetMods` 路径会受客户端构建和 Android 存储权限影响；必须以设备上该客户端实际创建的目录为准。不要把文件放入 `Mods`，也不要改名为 `.scmod`。

## 当前功能

- [x] x25.01.17 Windows 程序集与加载入口核验。
- [x] 最小 Mod 加载日志。
- [x] `.netmod` 构建、依赖检查和打包脚本。
- [x] Android/7723 最小 Mod 安装与加载测试（用户实机确认）。
- [x] 独立快艇方块物品（编号 `900`）、`Speedboat` 实体模板和受限生成逻辑。
- [x] 原创低多边形占位模型与占位贴图；不打包游戏本体资源。
- [x] 仅对 `Speedboat` 实体启用有上限的推进，普通木船不受影响。
- [x] 接入用户提供的 1254×1254 正式 PNG 贴图。
- [x] v0.4.0 额外推进提高至 `9`，水平速度硬上限提高至 `18`。
- [x] 移除联机生成与推进的软件禁用条件。
- [ ] v0.4.0 独立快艇 Android/7723 单机、主机和普通客户端实机测试。
- [ ] Android 触屏测试按钮。
- [ ] 地形编辑、撤销、蓝图与联机同步功能。

### v0.4.0 独立快艇使用方法

1. 先备份存档，并新建一个可丢弃的创造模式单人世界。
2. 在创造栏 `Items` 分类寻找 `Speedboat`（快艇），其内容编号为 `900`。
3. 手持快艇物品，点击水面或岸边方块表面，应生成独立快艇并消耗一个物品。
4. 点击快艇乘坐，使用原有 Android 移动摇杆前进、后退和转向。
5. 上船后应出现 `Speedboat boost active`；普通原生木船不应出现该提示，也不应获得加速。
6. 保存退出并重新进入世界，确认快艇实体和存档仍能正常加载。

附近 8 格范围最多生成 4 艘快艇，水平速度始终限制在 `18`。v0.4.0 已允许联机生成和推进，但客户端速度可能仍被服务器权威状态修正，因此必须分别记录主机玩家与普通客户端的实际速度、回弹和同步表现。编号 `900` 未被原版 x25.01.17 使用，但安装其他新增方块 Mod 时仍需检查编号冲突。

开发规范见 [AGENTS.md](AGENTS.md)，环境与测试流程见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)，计划见 [docs/ROADMAP.md](docs/ROADMAP.md)。
