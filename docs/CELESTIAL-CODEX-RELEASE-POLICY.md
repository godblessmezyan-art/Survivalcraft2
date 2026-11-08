# 云天书典 Release 历史记录

云天书典已永久并入荒野探险家。当前统一版本、包名、main开发和已校验 netmod 的 GitHub Release 规则见 [统一打包契约](../SCIE-Port/docs/PACKAGING-RULES.md) 与 [开发规范](../AGENTS.md)。以下旧独立发布流程仅为历史记录，已退休，不再执行；旧标签保留不动。

---

# 云天书典 · GitHub Release 发布规范

本规范适用于独立模组 `src/CelestialCodex/`，不自动发布 Builder Assistant、SCIE-Port 或 Ruins 的安装包。各模组使用专属 tag，避免现有仓库历史 `v0.x.y` 标签冲突。

## 版本与 tag

- 以 `src/CelestialCodex/modinfo.json` 中的 `Version` 为发布版本，采用严格 `X.Y.Z` 语义版本格式。
- Release tag 固定为 `celestial-codex-vX.Y.Z`；例如 `celestial-codex-v0.2.0`。
- 每次准备发布新版本必须递增版本号，并同时更新 `CelestialCodex.csproj` 的 Version、AssemblyVersion、FileVersion、`scripts/Build-CelestialCodex.ps1` 中的版本校验、README 及专项测试记录，避免 manifest 与 DLL 或构建脚本不一致。
- 补丁、功能和不兼容变更分别使用 PATCH、MINOR、MAJOR；已经发布的 tag/Release 不覆盖、不移动。对发布后的修复创建新版本。
- 原有其他模组的 `v0.x.y` 标签保持原样，不迁移或强制更新。

## 自动发布流程

`.github/workflows/release-celestial-codex.yml` 在 `main` 上推送 `src/CelestialCodex/modinfo.json` 的版本变化时自动运行；也可以从 GitHub Actions 手动触发。读取 manifest 后检查同名 Release 是否已存在：

1. 未发布：在当前 `main` 提交创建 `celestial-codex-vX.Y.Z` tag，并生成**源码预发布（prerelease）**；GitHub 自动提供 Source code (zip/tar.gz)。
2. 已发布：保持既有 tag 和 Release 不变，禁止静默重发同版本。
3. tag 被用于另一提交但没有 Release：失败并报错，避免覆盖历史。
4. 只有代码或文档变化但 **未更新 modinfo.json** 时不自动发布；想给用户一个新版本，必须递增版本。

> GitHub Actions 需要仓库允许 Actions 且赋予工作流 `contents: write` 权限。若权限被管理员策略限制，工作流可能失败，此时必须先解决权限后才称为“已发布”。

## 二进制发布与验证

源码预发布**不能冒充已编译、可安装的模组**。目标程序集是用户本地的 SCNET `Net_x25.01.17` / Android7723，不得把原版 APK、DLL、PDB 或专有内容提交到仓库或 Release。

本机 Windows 使用 `scripts/Build-CelestialCodex.ps1` 完成 ABI 校验、Release 构建、测试与 `.netmod` 打包后，记录版本、包 SHA-256 和测试结果。

如果对应版本的可安装 `.netmod` 已完成构建、验证且与 tag 代码一致，由发布人通过 GitHub Release 页面上传 `云天书典-X.Y.Z.netmod` 等产物；只有 Android 真机及相关单机/房主/普通客户端检查确实通过，才能标为正式 Release。未通过时维持 prerelease，并明确说明未验证项。

建议二进制交付说明包含：
- 版本、Git tag、精确 SCNET/Android 目标版本；
- 本次变化、兼容性与已知限制；
- .netmod 文件名、SHA-256；
- Windows/Android 实际测试状态；
- 安装、升级及回退方法。

## 职责边界

- 继续遵守 `AGENTS.md`：代码由开发者提交本地 `main`，`git push` 由用户本人执行，除非用户另行明确授权。
- Release 自动化只在用户将包含版本变更的提交推送到 GitHub `main` 后触发；不自动推送本地改动、不修改用户的游戏世界或个人网站。
- 发布自动化是专为云天书典配置的；其他模组可以参照本规范建立独立工作流，但当前不能称为已开启。
