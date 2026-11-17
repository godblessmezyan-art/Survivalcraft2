# 本地导入无日志闪退修复 0.89.2

2026-11-17。用户反馈点击本地图书导入即退出，游戏日志没有异常。统一荒野探险家版本升为0.89.2；书典继续编入唯一SCIEPort.dll，保留系统文件夹选书、TXT/EPUB、原Imports/缓存/续读与0.89.1苹果食物修复。

## 已查明的代码缺陷

只读检查固定官方x25.01.17 APK提取的Mono.Android.dll：`Android.Runtime.JNIEnv.FindClass(string)` 内部调用 `NewGlobalRef(reference.Handle)`，释放临时引用后返回的是 **全局引用**。它不同于原生JNI的同名FindClass函数；不能仅根据函数名推断所有权。

0.89.1在 `File.setReadOnly` 与 `DexClassLoader` 初始化的finally里，统一调用DeleteLocalRef清理FindClass结果。释放类型错误在系统窗口打开前即可发生。Android的[JNI规范与CheckJNI检查说明](https://developer.android.com/ndk/guides/jni-tips)明确区分两类释放，CheckJNI会检查引用类型误用。虚拟机层终止进程可以绕过托管catch和游戏日志。这是已确认的缺陷，符合用户现象；由于没有设备logcat/tombstone，不能断言这是该设备退出的唯一原因。

目标Mono.Android.dll SHA256仍为 `168EF7F2E6633F946112FE3D4F20FCDBF557E50B29EED9ABBC5036B6F73E8335`；Engine/Survivalcraft/APK/BCL指纹沿用[0.89.1专项](CODEX-PICKER-APPLE-0.89.1.md)，均为只读取证快照，不进入源码或安装包。

## 修复

- FindClass返回的类句柄使用DeleteGlobalRef；GetObjectClass、NewString、NewObject和对象调用返回的局部句柄继续用DeleteLocalRef。跨帧保留的选择器类仍为一份全局引用。
- 全部回调与start入口验证成功后才发布共享bridge。验证失败释放候选全局引用，允许下次重新初始化；字符串创建失败等中断也进入正确清理路径。
- JNI入口预检同时核对两类释放API；Activity、对象、类、方法和新建实例的必要句柄为空时，在托管层拒绝继续调用。
- 原生调用前将固定阶段写入运行时 `ModsManager.UserDataPath` 父目录下 `CelestialCodex/picker-last.txt` 并Flush/Dispose；仅保留最后一条版本/UTC时间/阶段/错误类型，不含图书路径和正文。文件是独立诊断记录，不依赖游戏日志；记录失败不会阻止选书。已完成时写selected/finished-without-file，启动失败写launch-failed，未完成记录只表示最后阶段，不能独自证明根因。

不修改Java/DEX、宿主APK/Manifest、加载器Hook、包布局、ID956/DATA0、三条GUID、缓存、网络包、植物/苹果/家居或蓝图材料。DEX继续11088字节/5自有类，SHA256 `DDF03BDC0FBD3CCD5EE3DFF98590E0C1B26D97215CB78A647DF1A43A1FDF621D`。

## 验证范围

新增严格JNI引用表夹具，注入实际编译后的反射桥接，执行完整新/已有DEX准备与类加载路径。错误种类释放和重复释放立即失败；旧DeleteLocalRef(FindClass)作为负对照必须失败。在每一个原生调用边界分别注入异常，验证无遗留局部/临时全局引用、无错误释放、失败bridge不发布；成功仅保留选择器类。另验证空对象/空方法不会进入原生对象调用。此夹具模拟目标引用契约，没有运行Android虚拟机。

Android取证脚本新增DeleteGlobalRef签名和FindClass方法体NewGlobalRef调用的IL检查，防止仅有方法名/签名通过却误解引用类型。

实际执行命令（仓库根）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:/SCNET/SCNETx25.01.17
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-CelestialCodex.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -AssemblyPath SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll
pwsh -NoProfile -File SCIE-Port/scripts/Test-CodexPickerAndroid.ps1 -AndroidDir artifacts/codex-picker-audit -CecilPath C:/Users/Administrator/.dotnet/tools/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -Configuration Release
pwsh -NoProfile -File SCIE-Port/scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir artifacts/codex-picker-audit -CecilPath C:/Users/Administrator/.dotnet/tools/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll
```

环境检查与完整Release入口均退出0，编译0警告/0错误，既有回归通过。书典461项Core/Network与24078项目标检查、534个目标成员解析通过；植物/苹果999961项隔离检查保持通过。17个Android JNI/Activity签名及FindClass全局引用IL检查通过；最终DLL书典范围353个Android BCL成员解析通过，0缺失。这些是静态/模拟/Windows检查，没有打开Android系统窗口。

统一包与版本副本字节一致：12787738字节、213条目，SHA256 `E3F5D043D2DDE4CDB8ABF8B737775AAE749C0AA1BBE2B68EEE615D6CEE92CCF8`。唯一SCIEPort.dll SHA256 `19CD181A1E46EDFB3C8F5D2D0CB476269CACC97D2E41106FCC3F2F2BDDE32D56`，MVID `0a47f80d-6499-46c6-9270-6cdc839c1eb3`。逐项比较0.89.1，条目集合相同且仅SCIEPort.dll/modinfo.json改变；其它模型/纹理/音频/CSV/XDB/声明全部逐字节保留。没有独立书典包或平台DLL。

日志与独立包比较记录在 `SCIE-Port/artifacts/codex-picker-0.89.2/full-build.log`、`android-abi.log`、`android-bcl.log`、`package-verification.json`。发布前只读核验origin仍为 `https://github.com/godblessmezyan-art/Survivalcraft2.git`，远端main为 `250695e9fec608b9ac787af2db9ed178b4727fa5`，尚不包含本次构建源码；因此本次不创建指向旧提交的Release。

## 设备复测与发布边界

Android7723原闪退设备、系统窗口/供应方、选择/取消/重复打开、TXT/EPUB实际导入、切回游戏、旋转/后台/进程重建、触屏/不同分辨率及真实Local/Server/Client均 **待验证**。不能把固定官方APK元数据或Windows引用夹具称为手机已兼容。

安装统一0.89.2包替换旧荒野探险家，不清除书库/缓存。先在可丢弃世界点击导入；若仍退出，查看 `CelestialCodex/picker-last.txt` 最后一阶段，并收集系统logcat/tombstone（游戏日志可能仍无托管异常）。正常选择文件后确认入库、阅读和重启续读；取消后再次打开。

源码默认本地main提交，不代用户push。构建提交尚未到GitHub时保留经校验产物，待用户推送后发布对应提交的预发布Release，不将包标成旧提交的构建。
