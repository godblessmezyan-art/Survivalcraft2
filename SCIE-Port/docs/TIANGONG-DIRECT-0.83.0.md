# 0.83.0：天工绘卷直接建造与main全线整合

2026-11-12。用户反馈“主机响应超时”，明确要求去除客户端校验、直接使用，并将家居/优化全部合入main后推送GitHub。本次指令覆盖旧绘卷客户端必须先经237上传/主机票据确认及默认源码不推送的约定；不扩大为后续任务默认force push或绕过游戏原生保护。

## 行为

删除绘卷自定义237包及注册、上传/逐片ACK、主机预览票据、真名解放门槛、身份/角色往返校验、15秒等待与重试。单机、房主、普通客户端共享本机世界子系统的直接施工队列，落点确认后立即开始，每帧总计32格；取消、失败回滚、按玩家隔离的撤销仍在主线程分批进行。历史最多20笔、全世界100000格，未恢复撤销保留并阻止新施工；死亡/退出/换物品停止并恢复未完成任务。编号971/DATA0、图谱既有位序/存档、默认+20%移速/+10%近战频率和独立主题保持；绘卷现在默认可用，其它神器的解放规则保持。

删除的是绘卷附加的客户端/主机校验流程。JSON/材料/坐标/数量、区块就绪、写前原值快照及后续玩家修改保护继续由施工事务处理。2026-11-13 用户要求清障默认开启：确认后先清空蓝图包围盒内（含门上半格与家具占地）所有非空气方块，再放置蓝图；清空与放置共用32格/帧预算并逐格写前快照，撤销可恢复。清障扫描体积上限262144格、清空量上限32768格，超出时拒绝并提示缩小蓝图。该默认开启是用户明确要求的例外，未开启清障时仍拒绝覆盖非空气目标。

固定x25.01.17目标的原生 `SubsystemTerrain.ChangeCell` 遇到WorkType.Client直接返回，故只删除角色判断不能施工。客户端现在调用 `ChangeCellNet` 做本地预测，再发送原生 `SubsystemTerrainPackage(x,y,z,value,true)`，即ID7/RequestChangeCell。原生主机Handle调用ChangeCell并广播权威结果；不使用自定义绘卷主机ACK，不修改CommonLib.WorkType，不伪造管理员身份。客户端门的上半格做同值预测并捕获原值，主机由原生门行为派生；撤销仍通过相同原生请求同步。

平台原生领地/其它Mod的TerrainChangeCell Hook仍可能拒绝请求；取消绘卷握手不会取消这些原生规则。客户端“已提交建造”表示本地分批与请求发送完成，不宣称收到主机确认。实际网络延迟、服务器拒绝后的预测校正、门/围栏派生状态与其它Mod组合仍需真机验证。

## main整合

将本地 `4fe99be` 与远端 `bdc1072` 进行正常双亲合并，保留家居957–970、座位/壁炉、已修复登记主机识别、云端BCL、PixelFX、建筑助手0.25.2和云雅书屋蓝图。合入远端工业家具长按回收（917–922）、书典纸页/夜读/收起字号的新界面、TXT/EPUB游戏内导入及本机文件浏览器。书典新增功能和测试迁至SCIE-Port内的既有目录/命名空间；不恢复独立工程、旧DLL或第二份modinfo，不移动956/三GUID/阅读缓存。旧TCP通道及其测试不合回运行路径。

新书典导入界面的GetBaseException和Task<string>.ContinueWith在留存Android BCL缺失，分别换为有界InnerException遍历及await回调。原HTTP策略/禁跳转/系统TLS/缓存限制保持。根旧Fast构建入口转发经校验的统一构建，CI发布也使用统一入口；已有版本只在提交和远端资产SHA256核验一致后复用，不覆盖不同提交或他人资产。正式产物仍为荒野探险家.netmod及同字节版本副本。

## 实际验证与产物

仓库根成功命令，仍使用本机只读目标程序集：

```powershell
$env:FrameworkPathOverride='D:/Dev/Tools/net472/build/.NETFramework/v4.7.2'
$env:DOTNET_ROOT='C:/Users/85196/AppData/Local/Microsoft/dotnet'
$env:DOTNET_ROLL_FORWARD='Major'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release
pwsh -NoProfile -File SCIE-Port/scripts/Test-BlueprintAndroidBcl.ps1 -AndroidBclDir artifacts/celestial-audit/android-bcl -CecilPath D:/Dev/Tools/ilspy/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll
pwsh -NoProfile -File SCIE-Port/scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir artifacts/celestial-audit/android-bcl -CecilPath D:/Dev/Tools/ilspy/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll
& SCIE-Port/tests/SCIEPort.Blueprints.Tests/bin/Release/net472/SCIEPort.Blueprints.Tests.exe D:/Games/SCNET-x25.01.17 (Resolve-Path SCIE-Port/artifacts/build/SCIEPort.dll).Path --live
```

| 检查 | 状态 | 实际证据/边界 |
|---|---|---|
| 环境/ABI、net472 Release和完整旧回归/包 | 已确认 | 最终完整入口退出0，0警告0错误；固定Windows三程序集及LiteNetLib登记指纹沿用0.82专项并由环境入口核对 |
| 直接施工与原生同步 | 已确认 | 842项：Local/Server/Client均无真名/237请求，取消/撤销/断线/异常回滚及32格预算；原生客户端预测→真实包流往返→原生主机Handle实际写入及广播，包括撤销。隔离内存场景，不是实际房间 |
| 书典整合 | 已确认 | 461离线与21315目标检查、494外部成员引用；真实原生控件尺寸/主题/字号/文字布局/点击区域及导入功能代码在单一SCIEPort.dll |
| Android快照成员 | 已确认 | 绘卷474、书典319成员解析，均0缺失；mscorlib MVID仍9d0f885b92554f54bb3ffdcb8916ab03，三份快照SHA见0.82专项。仅元数据，缺少本次APK完整指纹/其它BCL快照 |
| 最终DLL公网 | 已确认 | 目录3项、下载154162字节/revision1、严格校验/保存/离线重读通过 |
| Android7723、触屏/完整游戏、真实Local/Server/Client与服务器拒绝后的预测纠正 | 待验证 | 没有手机或真实网络房间执行，不以隔离测试替代 |

第一次书典全量BCL审计发现新导入UI上述两个裁剪成员；修改后重新完整构建/审计通过。合并时的缺失using/退休TCP测试引用及新原生fixture的命名空间编译错误已修复，不作为通过证据。最终日志 `artifacts/direct-full-build-final.log`、`direct-native-tests.log`、`direct-bcl-final.log`、`direct-book-bcl-final.log`、`direct-live-final.log` 和 `direct-package-check.json` 均在忽略目录。

包210条目、12757105字节，SHA256 `76BDA701ADFA15903F4F25F83379B9D8A13D457703D542D156C9245C43AB753C`；DLL SHA256 `31AB1968D3A5C043F7C14145102D858A197CB1A9B4B705D0EB44B5CA9051CBD1`，MVID `a97b9b49-feff-4cb4-bce9-91044281bde2`，版本0.83.0。对比0.82包条目集合一致，功能字节变化仅DLL/modinfo/工业家具CSV，绘卷CSV只有换行差异；家居模型/纹理、书典资源、PixelFX与图标保持，唯一DLL为SCIEPort.dll。

本次用户明确授权正常推送origin/main；合并提交包含双方历史，不force push、不改远端。提交在GitHub可访问后才发布0.83预发布并核验远端资产哈希，实际推送/Release结果以GitHub记录和最终交付为准。安装各端统一0.83.0并完全重启，使用可丢弃世界先验证绘卷直接建造/取消/撤销及书典导入。旧远端测试绘卷957的升级处理仍见0.82说明，971不再变号。

## 0.83.1 增补（2026-11-13）

清障默认开启并改为区域预清空：确认后先清空蓝图包围盒内（含门上半格、家具占地）所有非空气方块，再按32格/帧放置；清空与放置均逐格写前快照，取消/失败回滚/撤销可恢复。包围盒≤262144格、清空≤32768格，超限拒绝并提示缩小蓝图；未开启清障时仍拒绝覆盖非空气目标。旧门只允许清为空气，覆盖已有门仍不支持。853项蓝图检查通过；包210条目，`荒野探险家-0.83.1.netmod` 12757948字节/SHA256 `EB5B86588C6FD007B43EAC28D6A599BB34542CC8D0F5137ADCFE3BC332BB9052`，DLL MVID `51fb42b1-bcc6-47af-9eaa-4b9e03281251`/SHA256 `6A526A2A93712F5040AAD8117CA4360A4E874F6922BD16D7D706A7D99E640906`，版本0.83.1。各端统一0.83.1；Android/真实房间仍待验证。
