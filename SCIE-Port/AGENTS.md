# SCIE-Port 项目规范

本目录是独立的 SCIE Industrial Era → SCNET x25.01.17 移植工程，不属于 `src/BuilderAssistant`，不得引用、修改或打包 Builder Assistant 源码与资源。

- 唯一目标运行时：Survivalcraft NET `Net_x25.01.17`，目标平台 Android，产物 `.netmod`。
- `upstream/SCIE/` 是被 Git 忽略的只读上游快照。上游未声明许可证，不得把其源码、模型、贴图、音频、数据表或二进制复制进可发布产物。
- `src/SCIEPort/` 只允许 clean-room 新代码与明确可分发的原创资源。
- 游戏程序集只从 `SCNetGameDir`/`SCNET_GAME_DIR` 引用，不得复制或提交。
- 不使用 SCAPI DLL，不复用旧版 DLL，不通过伪造类或空实现宣称完成。
- 先完成单人材料注册和加载验证，再逐模块移植；联机功能必须区分 Local、Server、Client 权限。
- 兼容性状态只使用：`已确认`、`待验证`、`不支持`。
- 每次构建先运行 `scripts/Test-Environment.ps1`，再运行 Release 构建和包内容校验，并把结果写入 `docs/TEST-RESULTS.md`。

