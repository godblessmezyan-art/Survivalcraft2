# 云天书典手绘纸质 UI 0.90.0

2026-11-17。用户要求基于最新main统一优化书库、章节、正文、设置与导入界面，限定本次只改UI。工作开始时在干净本地main执行fetch/ff-only，已与origin/main最新 `72ec090` 一致；保留0.89.3 EPUB、0.89.2 JNI、植物/苹果/家居及其他模块。

## 改动范围

统一手工纸纹、自然水彩角花、墨褐手绘双线边框和鼠尾草绿按钮，保留夜读并统一导入面板。正文缩窄阅读栏、增加行距/留白，隐藏阅读页装饰，保留原有四项操作；中文只在显示行缓存重排，避免标点孤立和原生短行，不改变原文。

沿用原生Canvas/ScrollPanel/Label/Button/Clickable、游戏字体与2D图元；控件至少48逻辑单位、操作栏8间隔。主界面与导入界面共用控件、纸页、颜色和主题状态。系统选书仍为原Android选择器，未修改Java/DEX/Manifest或回调。原生预览复查后修正了水彩预乘透明编码、原生控件默认副标题残留和紧凑横屏的状态栏间距。

运行PNG仅新增Paper/Botanical两张256×256，合计151210字节/0.5 MiB GPU；image_gen原图、提示词、处理脚本与SHA256见[美术记录](../design/codex-paper-ui/README.md)。原图和设计资料不进入包。纹理只在原生Draw中加载并由ContentManager缓存；不引入UI/字体/平台依赖、shader或离屏渲染资源。

默认神器被动/物品栏主题原样保留，没有技能或技能特效改动。版本统一0.90.0，仍为单SCIEPort.dll/单荒野探险家包，net472/API1.42/SC2.4.10.6、956/DATA0/三GUID保持。

## 业务与存档边界

`Test-CodexPaper.py --base 72ec090` 逐文件核对23个文件：Core中除ReaderLayout/ReaderTheme外全部阅读/解析/导入/缓存/进度代码、全部Networking/Mod/Android桥接、书典CSV/XDB/原图标均与最新main完全相同（文本按Git行尾规范比较）。未修改ReadingPosition编码、Imports目录、slug/revision、限额、数据缓存、世界存档、权限或网络协议。

UI中的请求、切换、章节跳转、续读/保存、字号/主题、系统选择、后台返回、取消和清理沿用既有流程；只把样式、布局和显示行排版统一。原EPUB ZIP/XML、选书JNI引用生命周期及导入→暂存清理→重开会话读取回归继续执行。

## 验证命令

仓库根执行：

```powershell
git fetch origin main
git merge --ff-only origin/main
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:/SCNET/SCNETx25.01.17
python SCIE-Port/design/codex-paper-ui/build_assets.py
python SCIE-Port/scripts/Test-CodexPaper.py --base 72ec090
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-CelestialCodex.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -AssemblyPath SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll
SCIE-Port/tests/SCIEPort.CelestialCodex.TargetTests/bin/Release/net472/SCIEPort.CelestialCodex.TargetTests.exe E:/SCNET/SCNETx25.01.17 E:/Survivalcraft2 E:/Survivalcraft2/SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll --render-ui
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -Configuration Release
pwsh -NoProfile -File SCIE-Port/scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir artifacts/codex-picker-audit -CecilPath C:/Users/Administrator/.dotnet/tools/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll
```

新增美术源/运行哈希、256尺寸/alpha/预乘编码/预算检查，完整构建强制执行。原生测量/命中覆盖320×240、375×812、812×375、1280×720、2340×1080、768×1024，双主题/五字号/四阅读视图，并增加导入按钮触达、中文/英文/代理对/标点排版、原文保持及重排后的真实滚动比例检查。36张真实Engine/OpenGL控件预览覆盖两主题与三分辨率的六个界面；只用示例数据，没有世界/网络/Android。GPU夹具记录本机无音频输出设备提示，渲染正常完成。

环境检查与最终完整Release均退出0，编译0警告/0错误，原有完整回归通过。书典623项Core/Network、35372项目标检查及567个Windows成员解析通过；植物/苹果999961项隔离检查保留。最终DLL全部直接程序集在提供的固定APK快照可用，书典528个外部成员解析通过、0缺失；没有新增托管依赖。36张GPU预览及业务边界/资源预算检查通过。

统一包与版本副本字节一致：12944680字节、215条目，SHA256 `F5BAAAAE96C3A3FB9D7D193C097B09F694DCC41C4D93D2F3DAB971370454F52D`。唯一SCIEPort.dll SHA256 `7B727095CBA28B9EADBFD9B55924AF5DC1D2A3A06025B6707B6F6A0E3B5114C8`，MVID `cedcb7f3-b354-4ae2-a118-83e10f1ff817`。逐项比较0.89.3包：只新增Paper.png/Botanical.png，只修改DLL/modinfo/原创美术声明；没有删除条目，全部既有模型、纹理、音频、CSV与XDB逐字节保留。

本次日志在 `SCIE-Port/artifacts/codex-ui-0.90.0/full-build-final.log`、`android-final.log`、`ui-boundary.log`、`native-render-final.log`、`package-verification.json`。并排预览在同目录 `preview.png`；逐页原生图位于根 `artifacts/CelestialCodex/ui-0.90.0/`。上述都是开发证据，不打包游戏程序集、字体、原生截图工具或原始生成图。当前origin/main仍是构建基线72ec090；新UI提交尚未推送，不创建指向旧源码的Release。

## 手机/联机验收与交付

固定Windows x25与官方固定Android快照的证据单列；不能把编译、模拟命中或GPU预览标成Android已兼容。7723重新打包客户端、实体手机横竖屏/挖孔与手势区域、低端机帧率、最大字号触摸滚动、系统选书切回及真实Local/Server/Client均 **待验证**。没有新增常驻HUD；继续原生模态对话框。阅读业务/存档未改不等同于所有设备行为已经验证。

正式交付为 `荒野探险家.netmod` 与字节一致的0.90.0副本；本次默认本地main提交，源码由用户push。若构建提交尚未在GitHub可访问，不创建指向旧提交的Release；待用户推送后按对应提交发布待设备验证的预发布。
