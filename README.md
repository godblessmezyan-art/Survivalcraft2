# Builder Assistant for Survivalcraft NET

面向 Android 版《生存战争 2.4》联机客户端（Survivalcraft NET / SCNET）的建筑辅助 Mod。首要适配版本是 **Net_x25.01.17**，目标产物为 `.netmod`。

> 当前状态：Phase 1 开发环境与最小加载包已建立。代码已在对应 Windows 程序集上编译通过；Android/7723 渠道实机加载尚未验证，因此不能标记为兼容完成。

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
| 7723 渠道 Android 安装包 | 待提供程序集指纹 | 不能仅凭版本号认定与公开 APK 一致 |
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
- [ ] Android/7723 实机加载测试。
- [ ] Android 触屏测试按钮。
- [ ] 地形编辑、撤销、蓝图与联机同步功能。

开发规范见 [AGENTS.md](AGENTS.md)，环境与测试流程见 [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)，计划见 [docs/ROADMAP.md](docs/ROADMAP.md)。

