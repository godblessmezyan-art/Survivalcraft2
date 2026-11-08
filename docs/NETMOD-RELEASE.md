# 直接发布可安装 .netmod：环境配置与安全边界

本文件说明用户希望 GitHub Release 的 Assets 中直接提供 `荒野探险家-X.Y.Z.netmod`，而非仅源码 ZIP。GitHub 固有的 Source code (zip/tar.gz) 链接无法当作游戏安装包。

## 当前情况

- `celestial-codex-v0.2.0` 是历史源码预发布，没有可安装 `.netmod`。
- 当前 `SCIE-Port/` 与 `src/CelestialCodex/` 仍没有真正整合成单一 DLL/包，**不能发布一个声称包含阅读功能的荒野探险家包**。
- 本仓库 `.github/workflows/release-wilderness-explorer.yml` 现已配置为“先编译、后发布”；缺少集成就直接跳过，不再创建只有 ZIP 的新 Release。

## 自动生成 .netmod 的前提

1. 完成两个功能模块的单包集成，确认阅读功能与原荒野探险家在同一 `SCIEPort.dll` 中，资源/物品 ID/存档兼容与测试均有记录。
2. 真正通过 Windows/net472、SCNET x25.01.17 精确程序集验证及相关测试，然后创建 `docs/UNIFIED-RELEASE-READY`，文件唯一内容为 `ready=1`（末尾换行可选）。禁止为了触发 Release 而未经验证添加这个标志。
3. GitHub 仓库 Settings → Actions → Runners 配置一台**受信任的 Windows 自托管 Runner**，标签包括 `self-hosted`、`windows`、`scnet-x25`。游戏程序集保存在这台电脑的**私有本地路径**，不可上传 GitHub 仓库、Actions artifact、Release 或日志。
4. GitHub 仓库 Settings → Secrets and variables → Actions → Variables 中添加 `SCNET_GAME_DIR`，其值为 Runner 上实际的游戏目录，例如 `D:\Games\SCNET-x25.01.17`。这是该机器路径，不是云端游戏安装下载地址。
5. Runner 安装 Windows PowerShell 5.1、.NET 构建工具、Python 和 GitHub CLI `gh`，并满足 `SCIE-Port/scripts/Build-NetMod.ps1` 已有全部工具及资源依赖。不要给外部不可信 PR 或工作流在此 Runner 上执行的权限。

## 触发与交付

- 统一产品版本升级：更新 `SCIE-Port/src/SCIEPort/modinfo.json` 和 `SCIEPort.csproj` 中全部版本字段，直接提交到 `main`。只有集成标记存在才会执行 Windows 自托管构建。
- 真实构建执行 `scripts/Publish-WildernessExplorerRelease.ps1`：它先调用既有 SCIEPort 构建/测试脚本，然后核验版本号、唯一 modinfo、唯一正式 DLL 身份、核心书典类型、阅读资源与 SHA-256。检查失败就不发布。
- 成功后自动创建 `wilderness-explorer-vX.Y.Z`，Assets 包含 `荒野探险家-X.Y.Z.netmod` 和对应 `.sha256`。发布初始状态为 prerelease，直到 Android 和联机实际测试通过。
- 发布文件与源码 tag 均不可悄悄重写：旧版本 Release/asset 如果已存在，工具报错，应该递增新版本。

## 不启用自托管 Runner 的手动发布

拥有 Windows/SCNET 游戏目录的开发者可在干净的 `main` 分支执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Publish-WildernessExplorerRelease.ps1 -GameDir "D:\Games\SCNET-x25.01.17"
```

手动方式仍然需要安装并登录 `gh`（`gh auth login`），同样需要完整集成标志、构建、校验。只做编译验收、不创建 Release 时追加 `-ValidateOnly`。

## 下载

GitHub Releases 地址：https://github.com/godblessmezyan-art/Survivalcraft2/releases

下载 Assets 中 `.netmod` 文件；不要下载 Source code (zip) 当作安装包。
