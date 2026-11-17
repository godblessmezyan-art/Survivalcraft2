# 原创 Android 文档选择桥接

`CodexDocumentPicker.java` 通过平台 Fragment 启动系统文档选择并接收结果。所选 `content://` 文档由 ContentResolver 读取，显示名经校验、内容最多8 MiB，复制到 Context 提供的私有缓存，再交给原书典导入事务。当前客户端拥有自己的书库和续读记录。

运行资源为 `src/SCIEPort/Features/CelestialCodex/Android/DocumentPicker.dex`，嵌入现有 SCIEPort.dll。它仅包含本项目的5个类；不打包APK、SDK、AndroidX、游戏资源或额外托管DLL。源码与DEX、编译引用的精确SHA256见 [manifest.json](manifest.json)。Android API jar与R8只用于编译，下载到被忽略的本地工具目录，不随Mod分发。

Windows开发机使用JDK17。已有本地编译引用时，在仓库根执行：

```powershell
python SCIE-Port/scripts/Build-CodexPicker.py --tools artifacts/codex-picker-audit/tools --output artifacts/codex-picker-audit/rebuild
python SCIE-Port/scripts/Test-CodexPicker.py
```

第一条执行25项JVM文件名/输入流/上限/取消检查，生成DEX及清单；第二条不需要JDK，校验源码指纹、DEX SHA256/SHA1/Adler32、类边界和入口。普通Build-NetMod强制运行第二条；目标书典检查另核验DLL内嵌字节、原生API引用和用户原文件保护。

系统API依据：[Android文档选择与存储](https://developer.android.com/training/data-storage/shared/documents-files)、[Android14只读动态代码要求](https://developer.android.com/about/versions/14/behavior-changes-14)。Android6/7的宿主空闲检查依据固定AOSP的只读 `mActive` 字段，Android8起使用公共 `getFragments`，不在新系统回退访问私有字段：[Android6](https://android.googlesource.com/platform/frameworks/base/+/android-6.0.1_r81/core/java/android/app/FragmentManager.java)、[Android7](https://android.googlesource.com/platform/frameworks/base/+/android-7.1.2_r39/core/java/android/app/FragmentManager.java)。有其它原生Fragment时拒绝启动。保存Activity状态时仅在本桥接独占Fragment宿主的情况下排除自己的动态Fragment，避免下次进程启动早于Mod加载时恢复未知类。

这些编译/流/指纹/元数据检查不能替代设备验收。Android7723真实系统窗口、返回/取消、授权、文档供应方、旋转和进程恢复均待验证。完整证据见 [0.89.1专项](../../docs/CODEX-PICKER-APPLE-0.89.1.md)。
