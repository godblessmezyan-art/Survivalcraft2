# 云天书典 V0.2.0：本地 TXT/EPUB 与 Digital-Study 云端双书源

2026-11-08：此文为远端独立源码版本的历史记录。本地main已将其功能合入荒野探险家0.81.0；当前构建、安装、测试与后续统一发布以[整合版说明](../SCIE-Port/docs/CELESTIAL-CODEX.md)和[测试记录](../SCIE-Port/docs/TEST-RESULTS.md)为准，不再发布独立书典包。

当前为合入 main 的源码版本；没有在本环境编译出可安装的 .netmod，也没有 Android 实机验证。

## 游戏内文件导入（2026-11-09 功能迭代，待实测）

- 在本地目录页将中央按钮改为「导入图书」，弹出自有文件选择窗口。优先运行时检查 `Engine.Storage.ChooseFile`（只接收已知的返回字符串或 `Action<string>` 回调类型），兼容时调用系统选择器；不直接引用 Android/新版 SCAPI 程序集。
- 如果 x25.01.17 的运行时不暴露兼容的 `ChooseFile` 方法，则窗口提供 `app:/` 起点的游戏虚拟存储浏览器。只列目录和 TXT/EPUB，尝试通过反射获取 `ListDirectoryNames`；**此后备方案只覆盖游戏可访问路径，未证实能直接访问公共 Downloads**。
- 选择后从 `Storage.OpenFile` 流式读取，限制每本 8MiB；存入 `app:/CelestialCodex/Imports/`，采用临时文件后移动，错误则清理；同名文件自动生成新文件名，不覆盖已有阅读资料；导入成功后返回并自动刷新书库。
- `content://` URI、Android 回调签名、Native 文件权限、前后台切换恢复等尚未在目标 SCNET 安卓客户端验证。**系统选书能否真正启动和读取手机任意文件仍为待验证，不得描述为已解决。**
- 统一项目仍然没有合入 `SCIEPort.dll`，因而本更新只提交云天书典模块源码，当前快速荒野探险家安装包不包含该 UI。

## 新增功能

- 书库列表中通过“本地 / 云端”切换书源，保留原书籍、章节与阅读界面。
- 云端仍使用 https://wxhappylife.top/digital-study/api/game-library/v1；切回云端时会重新允许联网。
- 本地导入是目录扫描：游戏创建虚拟 app:/CelestialCodex/Imports，玩家需将 TXT 或 EPUB 文件放入实际映射目录后进入本地书库。Android 文件管理器能否访问该目录需要实测；没有 Android 原生文件选择弹窗。
- 本地 TXT/EPUB 在后台读取，按章节切片（每片最多12000 UTF-16单元），提供章节列表与断点续读。
- 本地阅读进度使用 local-position.json，云端原进度继续用 position.json；不会将本地书上传到网站、房主或其他玩家，也不修改世界文件。

## 格式与安全限制

- TXT 严格 UTF-8（允许 BOM），识别章、节、回以及 Chapter 标题；GBK/GB18030 未验证。
- EPUB 使用标准 ZIP+META-INF/container.xml+OPF manifest/spine，按顺序提取 XHTML 纯文本；禁用 DTD/外部实体，忽略脚本与样式。不支持 DRM、图片或复杂排版。
- 源文件不超过8 MiB，解压总量不超过16 MiB，EPUB 条目最多256个，本地最多64本，分片最多512段；异常文件拒绝。
- Android 对 System.IO.Compression、System.Xml、Engine.Storage 的真实支持及文件夹映射仍需验证。

## 本机编译与真机验收

请在具有合法 SCNET x25.01.17 游戏程序集的 Windows 电脑上运行：

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir E:\SCNET\SCNETx25.01.17

编译通过后才可生成 artifacts/CelestialCodex/云天书典-0.2.0.netmod。测试：TXT/EPUB 导入、目录与章节、滚动进度恢复、断网读取、云端重连、恶意 EPUB 拒绝、Android 触屏、单机/房主/普通客户端。

## 交付状态

| 项目 | 状态 |
|---|---|
| 0.1.3 Windows 目标 ABI 和云端 API 历史验证 | 已确认（历史版本） |
| 0.2.0 源码与静态替换检查 | 已准备，需核对 GitHub 提交 |
| 0.2.0 Windows/net472 真实编译 | 待验证 |
| 0.2.0 Android 文件导入、联网、单机/联机 | 待验证 |
| 0.2.0 安装包 .netmod | 尚未生成 |
