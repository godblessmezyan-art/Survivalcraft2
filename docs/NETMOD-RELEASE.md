# 快速自动发布 .netmod（无测试、无安装包校验）

最新约定：GitHub Release 必须在 Assets 提供可下载的 `.netmod`，而不是只有源码 ZIP。发布过程只要求成功编译并生成文件；**不执行测试套件、压缩包结构检查或 SHA-256 比对。**

## 自动化流程

1. 更新 `main` 分支中的 `SCIE-Port/src/SCIEPort/modinfo.json` 版本号（并同步 csproj），或者从 Actions 手动运行工作流。
2. GitHub Actions 通过 Windows 自托管 Runner 调用 `scripts/Publish-WildernessExplorerRelease.ps1`。
3. 它调用 `scripts/Build-WildernessExplorerFast.ps1`，使用 `dotnet build -c Release` 编译 `SCIEPort.dll`，随后将 DLL、`modinfo.json`、第三方署名与 Assets 打包成 `荒野探险家-X.Y.Z.netmod`。
4. 构建成功后使用 `gh release create` 直接上传 `.netmod`。不运行旧版 `Build-NetMod.ps1` 的完整测试和资源审计；也不再需要 `docs/UNIFIED-RELEASE-READY` 标志。
5. 任何编译失败或未能产出实际 `.netmod` 都不创建 Release。历史版本不覆盖，新版本使用 `wilderness-explorer-vX.Y.Z`。

## Windows 构建机要求

- GitHub 仓库 Settings → Actions → Runners 添加受信任 Windows 自托管 Runner，标签为 `self-hosted`、`windows`、`scnet-x25`。
- 在 Settings → Secrets and variables → Actions → Variables 添加 `SCNET_GAME_DIR`，填写构建机本地 SCNET x25.01.17 程序集目录。
- 构建机安装 Windows PowerShell 5.1、.NET SDK、GitHub CLI `gh`，并由 GitHub Actions 使用 `GH_TOKEN`。游戏 DLL 不得上传公开仓库或 Release。

## 交付状态与风险

- Release 默认是 **未经测试的 prerelease**。跳过测试和校验可能出现导入失败、贴图/模型异常或 Android 启动问题；不保证游戏内可运行。
- 当前云天书典源码与 SCIEPort 尚未真正合包，因此这个快速构建**只包含 SCIEPort 工程已有的内容，不包含独立的云天书典**；等功能真正移入同一工程才会随包发布。
- 此快建只打包现有项目 Assets，并排除旧版 Android 已知不支持的 `KellyExpansion.webp`；未执行历史构建流程的复杂模型/资源转换，可能与过去正式包存在差异。
- 不强制生成 `.sha256`，也不扫描归档内部。Release 页面中的 Source code (zip/tar.gz) 是 GitHub 自带源码链接，不是游戏安装文件。

下载：https://github.com/godblessmezyan-art/Survivalcraft2/releases

## 2026-11-09 更新

荒野探险家 0.79.0 开始，快速构建已经配置从 `src/CelestialCodex/` 编译全部书典 C# 并合并资产，而不再仅打包旧版 SCIEPort。真实 Windows 编译/Android 使用未验证，旧说明“不含书典”仅对应早期版本。无需测试或包校验的发布政策不变。
