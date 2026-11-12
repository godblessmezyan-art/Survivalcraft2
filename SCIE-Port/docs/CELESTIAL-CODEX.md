# 云天书典 · 荒野探险家内置模块

自0.77.0起已整合，当前交付基线为荒野探险家0.82.0。2026-11-08用户进一步明确：今后两者永久统一开发、统一版本、统一打包，直接在本地main维护和提交；不再恢复独立书典Mod。要求已写入仓库根和SCIE-Port的AGENTS.md。

## 本地书库

0.81.0合入远端0.2.0的TXT/EPUB本地书库：书库页按“本地”/“云端”切换。将自己的UTF-8 TXT或无DRM EPUB放入游戏运行时 `ModsManager.UserDataPath` 父目录下 `CelestialCodex/Imports/`，打开本地书库扫描；没有系统文件选择器。Android设备上该目录的实际映射和文件管理器能否访问均待真机确认。

TXT按常见章回标题切分；EPUB按spine提取正文，跳过脚本和样式，不支持DRM、图片或复杂排版。本地与云端各保存独立续读进度，原云端缓存和进度不清除；本地内容不上传。每本文件≤8 MiB、解压≤16 MiB、最多256 ZIP条目及64本书；详细限制和历史来源见[0.2.0说明](../../docs/CELESTIAL-CODEX-V0.2.md)。

本机net472构建及离线TXT/EPUB测试不代表手机支持：Android的 `System.IO.Compression`、`System.Xml` 和存储目录可访问性，以及手机触屏和实际联机状态均待验证。

## 使用与替换

云天书典已内置荒野探险家，在创造栏“实用工具”取得956/DATA0，使用后打开Digital-Study公开书库。选择书籍/章节，滑动阅读，A−/A+调整字号；“继续”恢复本机阅读位置。正文、目录与详情按原限额缓存，没有预下载未读书籍；网络故障时回退已缓存内容，刷新重试联网。

只保留一份`荒野探险家.netmod`或版本副本，移除旧版荒野探险家及独立`云天书典.netmod`后完全重启。不要同时安装独立书典：二者占用相同956和书典GUID。参与联机各端统一安装同一版本的荒野探险家，原生Mod列表/版本/资源校验保留。

阅读缓存、position.json和可选endpoint.txt继续位于运行时ModsManager.UserDataPath父目录的`CelestialCodex`目录，不按新包名搬迁，不删除旧缓存。956与物品DATA保持，实际旧世界升级仍需用备份/可丢弃副本验收；不要用保留编号推定所有存档均已测试。

该书是普通阅读工具，延续独立版零武器伤害、不可放置、不修改玩家属性的定位，不加入神器真名图谱。新增独立天蓝/古金槽位主题，分类`SCIEPort.Tools`/“实用工具”可供今后同类工具使用；不是为神器默认属性创建新例外。

## 实现与身份

- 来源：本仓库7eb3ddf的独立CelestialCodex0.1.4；用户2026-11-04明确反馈“测试通过”并授权整合。源码、资源与测试迁入SCIE-Port，不通过项目外Compile/ProjectReference或独立DLL复用，不引用Builder Assistant。
- 实现：`src/SCIEPort/Features/CelestialCodex/{Core,Networking,Mod,UI}`，命名空间`SCIEPort.Features.CelestialCodex.*`。只有既有SCIEPortLoader，原书典独立ModLoader/csproj/modinfo退休；旧根构建命令现在转到荒野探险家Release，不再产出独立包。
- 956/DATA0保留；书典自有GUID `8e7fc4af-2b7a-4b68-a02e-5c5443b1ece2`、`58451814-3a27-4453-b96b-8d87e9241368`、`00999a64-5d27-4a0a-a4d8-7b42c7593782`保留。Subsystem/Member名仍CelestialCodexBehavior，XDB Class改为本程序集完整类型名。
- 固定net472/API1.42/SC2.4.10.6、PackageName/AssemblyName/RootNamespace=SCIEPort及原物品/协议/存档身份保持。新增CSV、XDB和Book.png三个包条目，无第二个Mod/DLL或自定义SCNET网络包。
- 原API来源`https://wxhappylife.top/digital-study/api/game-library/v1`，目录/详情/正文schema、revision约束、12秒预算、取消/迟到结果隔离、离线/缓存/续读格式保持。TLS验证、禁跳转/无Cookie/identity及已验证的Android裁剪后端策略保持。书典OnUse只为本机IsMainPlayer打开UI，后台不持有玩家/Project引用；Dispose关闭全部对话框。
- 缓存每文件256KiB、128个受管JSON、总量8MiB；不改变公共来源/endpoint白名单。HTTP细阶段、首行异常类型/消息及完整日志保持。原Android BCL取证及边界见[0.1.4记录](../../docs/CELESTIAL-CODEX-HTTP-0.1.4.md)。

## 构建和检查

两部分的常规开发直接在本地main进行，共用荒野探险家版本号和构建入口。任何一方的修改都要保留另一方已经纳入main的内容，并对相关模块回归；书典根构建脚本只转发至统一入口。用户当次明确指定分支时遵从当次指令，但交付仍需统一成包。Builder Assistant与其它独立项目不受此整合影响。

```powershell
# 指向本机实际x25.01.17；需要net472引用程序集和可用的Python/.NET SDK。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-WithPinnedSdk.ps1 -GameDir <SCNET目录> -Configuration Release
```

完整构建自动运行阅读Core/Network测试及目标DLL检查：单加载器、唯一956、XDB类名/GUID/原生反序列化、PNG、分类和已知缺失BCL守卫；包内新资源与最终DLL逐字节匹配，禁止CelestialCodex.dll。书典专用检查不修改其它神器的测试范围。

另外使用PowerShell7运行`SCIE-Port/scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir <本机Android BCL快照> -CecilPath <本机Mono.Cecil.dll>`，仅解析读书namespace及其嵌套状态机的直接引用，核验HTTP策略字段/媒体类型ToString，拒绝桌面/GAC蒙混；旧0.1.3重建DLL作四个缺失接口的负对照。取证文件不提交、不打包。

## 验收边界

独立0.1.4由用户本会话反馈测试通过；未提供本次APK哈希或细分缓存/多人测试步骤。此反馈不替代整合版实际游戏验证。整合版Windows/元数据/公网与包结果见[测试记录](TEST-RESULTS.md)；Android旧世界、触屏多分辨率、断网续读、退出/重开与Local/Server/Client均待验证。

手机首测使用可丢弃创造世界/旧存档副本，验证实用工具分类→使用→刷新→目录→正文，然后验证已有position.json续读与离线缓存、多次关闭/退出世界。若失败，提供新的`[CelestialCodex] Network stage/type/message`行及后续MVID。
