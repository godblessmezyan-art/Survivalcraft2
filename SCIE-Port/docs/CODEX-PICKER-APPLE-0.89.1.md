# 系统选书与苹果食物修复 0.89.1

后续修订：0.89.1系统选书桥接存在JNI全局引用被错误局部释放的问题，可导致游戏日志无异常的原生退出；已在[0.89.2](CODEX-PICKER-CRASH-0.89.2.md)修复，应使用新版复测。以下保留当时验证记录，元数据签名通过不代表引用生命周期正确或手机已兼容。

2026-11-17。用户报告本地书典“系统选书不可用，请浏览游戏可访问的文件”，要求点击后在系统文件夹选中图书导入；同时指出苹果940能放置且使下方方块透明。版本统一0.89.1，继续在main维护单SCIEPort工程/单荒野探险家包，保留0.89.0植物/果树、原家居、书典、其它功能及身份。

## 原因与修复

书典原选择器只反射猜测 `Storage.ChooseFile`，固定Windows和Android x25均没有这个入口。官方Android APK内 `Engine.Window.Activity` 可用，但 `Mono.Android` 与引擎已经裁掉普通 `StartActivityForResult` 托管入口和宿主结果事件，不能仅改一个方法名或静态引用完整Xamarin DLL。

新增 `AndroidBookPicker` 与本工程原创Java Fragment桥接。运行资源为SCIEPort.dll内嵌的 `SCIEPort.CelestialCodex.DocumentPicker.dex`，从DLL读取后按SHA256命名写入Context提供的内部code cache，打开时先置只读再写内容，已存在文件先校验字节。通过目标已核验JNI API加载同一份原创桥接；没有修改/重签游戏APK、注册额外Manifest活动、添加AndroidX或第二个托管DLL。

本地书库点击“导入图书”后，导入面板首帧自动启动系统 `OPEN_DOCUMENT`、`CATEGORY_OPENABLE`，支持TXT/EPUB。用户可在系统文档界面选择文件夹与文件；桥接通过ContentResolver查询显示名并打开输入流，不把opaque content URI当作文件路径。后台分块复制至私有UUID临时目录，再沿原 `BookImportService.Copy` 导入原Imports目录。当前客户端保存原书库、缓存及续读，保留8 MiB/64本限制、重名加序号及写入临时文件后提交。

原文件只读，取消、错误与导入结束清理本次私有临时文件；清理登记表只接受已确认属于选择器缓存的文件。普通系统路径可直接以FileStream读取，游戏浏览器路径仍走Storage；不会删除用户选择的原文件。Android备用浏览器根从app:/只读资产改为目标已验证的android:/公共存储。界面状态回到游戏更新线程后处理，系统窗口导致暂停时保留导入会话，退出世界/书典时停止回调与导入；取消后可重试。

Fragment宿主已有其它原生Fragment时拒绝启动。Android8+通过公共getFragments检查，Android6/7只读核对AOSP mActive。系统保存Activity时，在桥接独占宿主的情况下排除动态Fragment的恢复状态，避免新进程加载Mod前恢复未知类；这些生命周期情形仍需真实设备验证。

苹果原CSV将940设为 `IsPlaceable=true` 且 `IsTransparent=false`，而实际模型只占格内很小一部分。目标 `Block.ShouldGenerateFace` 用邻块的 `IsFaceTransparent` 决定是否生成面；放置苹果后会把下面完整方块的面剔除，形成透视洞。

现940设为不可放置食物、透明邻面，并在代码显式拒放、六面均允许邻块生成。旧940地形格保留并正常渲染，不批量改玩家世界；通过原生挖掘可回收1枚苹果。原营养0.13、ID940、树939/树苗/果树生长和采果保持，苹果树仍可正常种植。

## 目标证据

固定开发目标仍为Windows `Net_x25.01.17`，net472/API1.42/SC2.4.10.6，三个原Windows程序集哈希由Test-Environment核验。

本轮只读下载并提取 [官方固定x25.01.17发布APK](https://github.com/survivalcraft-net/survivalcraft-net/releases/tag/x25.01.17) 做裁剪元数据审计。APK与提取DLL仅保存在被忽略的artifacts，不纳入源代码/安装包：

| 对象 | SHA256 |
|---|---|
| 官方APK | `B654F5AE8E9A3A9844ED461139F44707E265A3EEE53D81A874E2F1F8AD928A47` |
| Android Engine.dll | `8811109FDA2EF14AAA97DE537A686A355F32784996181C5A9FE9416ECBF314A7` |
| Android Survivalcraft.dll | `3520162AFB8DCCC51DD32A2FE22114D0325DD55D270B4B922AABF16A4AA4429B` |
| Mono.Android.dll | `168EF7F2E6633F946112FE3D4F20FCDBF557E50B29EED9ABBC5036B6F73E8335` |
| Android mscorlib | `C51F1D8E4F5701F3D3DBFF9A98658D5F74B387BF5911B0F061527BE16ACAA3A3` |

16项JNI/Activity签名检查通过。新路径曾被Android BCL审计检出 `HashAlgorithm.ComputeHash(Stream)` 缺失，现改为有上限读取后ComputeHash(byte[])；最终DLL的348项书典BCL成员解析全部通过。官方快照与7723重新打包不能自动等同，不将静态证据标为手机兼容。

原创DEX为11088字节，5个自有类，SHA256 `DDF03BDC0FBD3CCD5EE3DFF98590E0C1B26D97215CB78A647DF1A43A1FDF621D`。全部编译引用、原Java源码和DEX指纹在 [工具清单](../tools/codex-picker/manifest.json)；普通构建强制校验。Android系统接口依据及再生成方式见 [工具说明](../tools/codex-picker/README.md)。

## 实际命令与结果

仓库根执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1
python SCIE-Port/scripts/Build-CodexPicker.py --tools artifacts/codex-picker-audit/tools --output artifacts/codex-picker-audit/rebuild
python SCIE-Port/scripts/Test-CodexPicker.py
pwsh -NoProfile -File SCIE-Port/scripts/Test-CodexPickerAndroid.ps1 -AndroidDir artifacts/codex-picker-audit -CecilPath C:/Users/Administrator/.dotnet/tools/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-CelestialCodex.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -AssemblyPath SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -Configuration Release
pwsh -NoProfile -File SCIE-Port/scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir artifacts/codex-picker-audit -CecilPath C:/Users/Administrator/.dotnet/tools/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll
```

环境与完整Release入口退出0，0警告/0错误；完整旧回归通过。新增25项JVM文件名/Unicode/流/8 MiB边界/取消检查，DEX源码与校验和/类边界检查通过。书典461项Core/Network及23513项目标检查通过，包含嵌入资源逐字节一致、单DLL/无静态平台DLL引用、选中文件读取和原文件清理保护。植物999961项目标隔离检查通过，新增苹果禁放、六面邻面生成、原旧不透明食物负对照和旧格回收。以上是编译/静态/JVM及内存夹具，未打开玩家世界或真实系统窗口。

日志在 `artifacts/codex-picker-0.89.1/full-build-final.log`、`android-abi.log`、`android-bcl.log`，包额外核对在 `package-verification.json`。统一包与版本副本字节一致：12786987字节、213条目，SHA256 `62792291F741EEA390833E15E2B96F21C3E738C732B6B5766078D0D9A47EA4FC`。唯一SCIEPort.dll SHA256 `12516CDC6D75DEA38A9EABC9846EEECA60BC7855B5B367C6B5DA9438BA3100B8`，MVID `19dc273c-b01f-428b-80a6-3c5366e40478`。逐项比较0.89.0，条目集合保持，仅SCIEPort.dll、modinfo.json、Apple.csv与第三方声明变化；全部其它模型/纹理/音频/CSV/XDB（含书典原资源）逐字节保留。DEX仅作为DLL嵌入资源，不另放APK或平台DLL。

## Android与联机验收

**待验证**：7723实际程序集、Android系统选择窗口、中文TXT/EPUB（含无扩展名content URI但有显示名）、下载/文件管理器供应方、授权与拒绝、选择取消、8 MiB边界、退回游戏、旋转/后台/进程重建、重复导入、重名、重启离线阅读及续读。先备份并在可丢弃世界测试，不删除旧书库。

**待验证**：真实Local/Server/Client的苹果不可放置、正常食用、旧落地苹果邻面及回收、树苗/成熟树采果；各端统一0.89.1。本轮没有引入网络包、改存档身份或全图清理，不能用Windows隔离检查代替手机与真实联机。

源码按规范本地main提交；对应提交尚未到GitHub时不发布指向旧提交的Release。保留统一荒野探险家包，待用户推送后发布同一提交的预发布版本。
