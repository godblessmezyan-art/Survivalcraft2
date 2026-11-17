# 已导入EPUB打开失败修复 0.89.3

2026-11-17。用户确认0.89.2可以导入，但点击已导入的EPUB报“本地文件不可读: FileNotFoundException”。荒野探险家统一升级0.89.3，继续单SCIEPort.dll/统一netmod，在本地main维护。

## 原因与目标取证

固定官方x25.01.17 APK的28个托管程序集里没有 `System.IO.Compression.dll`，也没有ZipArchive/ZipArchiveEntry类型。旧DLL仍直接引用该程序集，并在本地EPUB读取时调用对应类型；这会触发依赖加载的FileNotFoundException。新范围审计同时确认目标System.Xml中的 `XmlNode.SelectSingleNode(string)` / `SelectNodes(string)` 被裁剪，原XPath查询也不能用于手机。

原0.89.2的Android BCL审计只载入mscorlib/System/System.Net.Http，并跳过未登记scope，因此未覆盖缺失程序集与System.Xml。本次改为读取所提供APK的全部程序集，先检查最终Mod的全部直接程序集引用，再检查书典范围全部外部成员；解析结果必须来自提供的快照目录，禁止Windows/GAC兜底。以原0.89.2包中的真实DLL为负对照，精确检出缺失压缩程序集/5个ZIP成员/2个XPath成员共8项。

固定APK、Engine、Survivalcraft和Mono.Android指纹沿用[0.89.1](CODEX-PICKER-APPLE-0.89.1.md)。本次涉及的Android运行库SHA256：System.dll为 `A109C9C23A34DA847B08771475469FF2FFD3BF6FF7CB5DBDE0B48FB82245F75E`；System.Xml.dll为 `0A87E193586A05EA0C34F8E9D11C1B880930B94F826F4066172DA311A1CAD3D7`。DeflateStream(Stream,CompressionMode)及所用DOM接口在目标程序集保留。所有专有程序集只读保存在忽略目录，不进入源码和netmod。

用户的“现在可导入”记录为设备反馈；没有设备型号/7723实际程序集指纹或本次错误完整堆栈，不将官方快照的缺陷证据等同于所有手机均已验证。

## 修改

移除正式工程对System.IO.Compression.dll的引用。新增本工程原创、只读且有界的EPUB ZIP读取器，使用目标System.dll保留的DeflateStream解压；按[PKWARE ZIP APPNOTE 6.3.10](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT)实现中央目录/本地头边界检查，支持Store/Deflate及data descriptor，不复制该规范或第三方实现源码。全部成员在内存读取，不解压至磁盘；核对名称/长度/CRC与实际输出，拒绝多卷、ZIP64、加密、其它压缩方式和越界。保留8 MiB源文件、256条目、16 MiB总声明/实际读取预算、每XML/章节限额及取消；保留原路径穿越、重复条目、DTD/外部实体和深度限制。

EPUB XML改用节点LocalName遍历，继续支持默认/前缀命名空间、OPF元数据、spine、中文URI、正文提取及排除脚本/样式，取消对裁剪XPath方法的调用。

本地读取异常现在通过ReaderSession输出有界完整异常（最多4096字符，含消息/堆栈）至游戏日志，并在 `CelestialCodex/picker-last.txt` 写入local-read-failed/异常类型；不记录正文，诊断自身失败不影响阅读。导入位置、提交文件名、slug算法、revision算法和续读保持，升级后可先直接打开原已导入图书。系统选择器/原JNI修复、苹果食物/透视修复、植物生长和家居保持；不新增加载器Hook、资产、ID/GUID、存档或网络协议。

## 验证

新增Store/Deflate、顺序写入data descriptor、中文/emoji、精确输出上限、取消、逐字节截断、损坏CRC/头/尺寸、伪造展开长度、加密/多卷/超过256条目等测试；验证命名空间EPUB元数据/章节/正文，并检验完整异常回报与诊断失败隔离。

目标集成夹具执行实际编译DLL的ReaderSession.ImportFile和Engine.Storage：导入中文EPUB、清除已登记系统暂存文件、逐字节核对Imports文件、打开目录/章节/正文、关闭再建会话后重复读取并核对slug/revision。只使用GUID临时目录，无玩家世界或设备；全部原系统选书JNI引用生命周期负对照/失败注入继续执行。

仓库根实际命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:/SCNET/SCNETx25.01.17
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-CelestialCodex.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -AssemblyPath SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -Configuration Release
pwsh -NoProfile -File SCIE-Port/scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir artifacts/codex-picker-audit -CecilPath C:/Users/Administrator/.dotnet/tools/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll -NegativeDll SCIE-Port/artifacts/codex-read-0.89.3/previous-0.89.2.dll
```

环境检查与完整Release入口均退出0，编译0警告/0错误，完整旧回归通过。书典617项Core/Network、24639项目标检查及540个Windows成员解析通过，植物/苹果999961项隔离检查保持通过。最终DLL全部直接程序集引用在固定APK提供的28程序集内可用，书典499个外部成员解析通过、0缺失；真实0.89.2负对照如上检出8项缺陷。系统选择器17个JNI/Activity签名及FindClass全局引用IL检查仍通过。

统一包与版本副本字节一致：12790575字节、213条目，SHA256 `9FC5539B60EC62982A7FF1F016C2EED826151ECFC7C7886E509FE8B2C3F2BB5A`。唯一SCIEPort.dll SHA256 `ADA44910C038502B0CE0F74D416A5072F0CC5325267F72C4809DFCC2D5B15FB8`，MVID `92970b25-7d52-4000-bbfa-eebb2988f14b`。逐项比较0.89.2，条目集合保持，仅SCIEPort.dll/modinfo.json变化，其它CSV/XDB/模型/纹理/音频/声明全部逐字节保留。

日志与包比较记录在 `SCIE-Port/artifacts/codex-read-0.89.3/full-build.log`、`android-audit-final.log`、`android-picker.log`、`package-verification.json`。本轮只读核验origin为 `https://github.com/godblessmezyan-art/Survivalcraft2.git`，远端main已由用户推至0.89.2提交 `70f3e4ca3cbd638189561d4c9054653c8671f4c2`；尚未包含本次0.89.3源码，因此不把新包发布为旧提交的Release。

## 实机与交付

Android/7723原EPUB、不同来源EPUB2/3、原已导入文件、重启续读、后台/取消、触屏/多分辨率及真实Local/Server/Client **待验证**。用户已确认系统选书可导入，本轮保留该路径；不以Windows/元数据测试替代手机阅读验证。联机仍为本机私有书库，不上传本地正文。

正式交付仍为 `荒野探险家.netmod` 与字节一致版本副本，保留956/DATA0/原三GUID/缓存；没有独立书典DLL。默认本地main提交，不代用户push；本次源码提交尚未到达GitHub时保留包，待推送后发布对应提交的预发布Release。
