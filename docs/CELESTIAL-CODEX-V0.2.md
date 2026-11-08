# 云天书典 V0.2.0：本地 TXT/EPUB 与 Digital-Study 云端双书源

当前为合入 main 的源码版本；没有在本环境编译出可安装的 .netmod，也没有 Android 实机验证。

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
