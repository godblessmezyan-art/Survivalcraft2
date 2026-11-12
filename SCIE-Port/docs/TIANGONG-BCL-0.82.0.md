# 天工绘卷运行库修复与本地主线整合 · 0.82.0

2026-11-12。用户要求参照已经可用的建筑助手修复荒野探险家自动建筑神器。用户手机日志的Mod MVID为 `9f6344be874541d79241efa765e8ef75`，mscorlib MVID为 `9d0f885b92554f54bb3ffdcb8916ab03`；明确缺少 `System.Net.Sockets.TcpClient.Close()`，故障在 `BlueprintCloudConnection.ConnectionObserver.Observe` 初始化异步状态机时发生，尚未发送HTTP请求。即使取消分支没有执行，JIT解析方法体也会遇到该成员。

## 修复与来源

任务开始时本地main为 `b94c31c`，SCIEPort为0.81.0且没有绘卷模块。远端main为 `bdc1072`，包含绘卷 `c3879c6` 及依次改用HttpWebRequest、APM、TCP/TLS的尝试。此前本地建筑助手0.25.2修复 `a708c90` 尚未纳入该远端版本线，绘卷仍保留旧0.25.1的HTTP/文件调用。

本轮只将绘卷及其原有图标、分类、图谱、加载器/测试接入独立适配到当前SCIEPort；保留本地家居、书典、PixelFX与多人修复。解决文档、主题、加载器及版本记录冲突，没有用远端较旧SCIEPort整体覆盖本地，也没有修改建筑助手源码或产物。原专项授权同步回根和移植AGENTS。原始适配来自 `c3879c6`，运行库适配来自本地建筑助手 `a708c90`，独立文件及SHA256记录在 `docs/assets/tiangong/blueprint-source.json`；无跨工程Compile/ProjectReference或BuilderAssistant.dll。

默认HTTP路径使用经0.25.2核验的 `HttpClient + BlueprintHttpPolicy`。公开setter存在时反射设置；已裁剪时仅接受已验证的Android `_delegatingHandler → _settings` 后端字段结构，关闭跳转/Cookie/自动解压并回读三个getter。未知布局或回读失败在发送前拒绝，TLS和证书验证不改。ContentType.ToString严格检查JSON媒体类型边界，保留UTF-8、identity、2MiB、12秒覆盖响应体的预算与取消。

不再包含绘卷专有TCP/TLS、WebRequest或TaskFactory.FromAsync通道。独立编译远端 `bee8757` 的原始 `BlueprintCloudConnection.cs` 作负对照（HTTP读取器仅用测试桩满足链接，不执行）：留存Android快照检出三项缺失成员：`TcpClient.Close()`、`SslStream.AuthenticateAsClient(string)`、`TaskCompletionSource<bool>.TrySetCanceled()`。该DLL是源码负对照，MVID `00232c15-31ab-4f28-8bd7-c47f9ba466b6`，不是用户手机原DLL；不能把两者MVID等同。因此只替换Close不足以解决后续缺失接口。

同步独立适配0.25.2的缓存和蓝图库文件操作：用StreamReader/StreamWriter代替裁剪的File编码重载，使用已保留的流写入/Directory.GetFiles/File.Delete；八位文件名哈希逐位输出，保持名称。私有缓存采用完整临时文件→旧完成文件`.previous`→新完成文件发布；中断后恢复前一份，存在新文件时以新文件为准，权威删除移除两份。不是单次原子File.Replace，不覆盖玩家改过的同版本蓝图。日志细分http/deadline、handler、send、headers、body与decode/cache，首行有界类型/消息；BCL错误不会伪装为离线成功。

## 本地主线冲突与升级

远端测试绘卷957、施工239与本地已发布的冰箱957、家居239冲突。本轮保留原家居身份，为绘卷分配 **971/DATA0** 与独立 **237** 通道，原图谱bit8及三条绘卷XDB GUID保持。实际固定目标原生与Mod类型扫描验证971/237无冲突；不证明其它模组也未占用。237沿用绘卷字段/分片协议布局，238座位、239家居和240–254旧包保持。

旧测试绘卷957与冰箱957没有可可靠区分的数值身份，**不自动迁移或重解释所有957存档**。从远端0.80.0绘卷测试包升级时，先在旧包移除背包/容器中的绘卷957，备份世界后换包，再在创造栏领取971；旧957不能直接当作新版绘卷使用。从本地0.81.0升级则原有冰箱/家居保持。各端统一0.82.0，不能与旧绘卷239协议或旧版本混房。

绘卷默认属性保留：WalkSpeed自有增量+20%、m_lastHitTime口径近战频率×1.1并与四圣兽相乘、独立Tiangong主题；原神器图谱八位保持，绘卷使用第九位。绘卷回执按本地已修复的登记主机连接识别，并在队列消费时重查连接与会话Token，避免重新引入主机对象身份差异问题。云天书典956/三GUID/缓存、原家居957–970、net472/API1.42/SC2.4.10.6及SCIEPort内部身份保持。新增GuiDraw、绘卷子系统/图标/数据库为绘卷原有接入，不新增网络或地形权限豁免。

## 实际验证

Windows固定目标指纹：Survivalcraft.exe `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`；Engine.dll `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`；EntitySystem.dll `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`；LiteNetLib.dll `D9C49444639102021EC6141C66E61B35F57493D3393CF2A737F68195C362D25A`。只读引用，不分发。

实际成功命令，仓库根执行：

```powershell
$env:FrameworkPathOverride='D:/Dev/Tools/net472/build/.NETFramework/v4.7.2'
$env:DOTNET_ROOT='C:/Users/85196/AppData/Local/Microsoft/dotnet'
$env:DOTNET_ROLL_FORWARD='Major'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release
pwsh -NoProfile -File SCIE-Port/scripts/Test-BlueprintAndroidBcl.ps1 -AndroidBclDir artifacts/celestial-audit/android-bcl -CecilPath D:/Dev/Tools/ilspy/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll SCIE-Port/artifacts/build/SCIEPort.dll -NegativeDll artifacts/tiangong-tcp-negative/bin/Release/net472/TiangongTcpNegative.dll
& SCIE-Port/tests/SCIEPort.Blueprints.Tests/bin/Release/net472/SCIEPort.Blueprints.Tests.exe D:/Games/SCNET-x25.01.17 (Resolve-Path SCIE-Port/artifacts/build/SCIEPort.dll).Path --live
```

完整构建入口执行本工程Test-Environment、Release与全部原有回归；新增Test-Tiangong在编译后检查最终DLL，在打包后比对CSV/XDB/PNG/DLL字节。首次编译的加载器冲突合并括号问题已修正。第一次完整回归因物品主题冻结守卫检出新增Tiangong失败；只为新增枚举/971映射/主题色三条添加精确归一化，其余旧主题与PixelFX机制仍匹配原SHA，未更新冻结基线蒙混。上述失败不作为通过证据。

| 范围 | 状态 | 证据/限制 |
|---|---|---|
| 固定Windows环境/ABI、net472 Release、完整旧检查及包 | 已确认 | 完整入口退出0，Release0警告0错误；未启动游戏 |
| 绘卷原生API/蓝图/地形/模拟联机 | 已确认 | 770项，含两位普通玩家主机施工、权限、边界、预算、取消/撤销/失败回滚、编号/包占用、属性、布局、模型及下载文件/缓存恢复；额外真实loopback禁跳转/无Cookie检查通过 |
| Android留存BCL直接成员/HTTP布局 | 已确认 | 最终DLL全部Blueprints类型及状态机508个成员解析，0缺失，禁止桌面/GAC兜底；旧TCP源码负对照检出上述3项。仅元数据，不是手机执行 |
| 最终DLL生产HTTPS/下载/保存/离线 | 已确认 | 目录total=3且非缓存；下载第一项154162字节、revision=1，SHA/schema/身份验证、保存与离线重读通过；不是完整游戏联网 |
| 书典、家居、分类与原有功能回归 | 已确认 | 书典412离线/13907目标及412成员、24家居模型40232面/494104规则/325原生库存与存档、813分类主题；星门/神器/军势/PixelFX等完整入口回归通过 |
| Android7723刷新/下载/触屏、旧世界升级及真实Local/Server/Client | 待验证 | 无手机/完整游戏/房间执行；不能继承建筑助手、旧绘卷或Windows的实机结论 |

留存Android快照：mscorlib SHA256 `C51F1D8E4F5701F3D3DBFF9A98658D5F74B387BF5911B0F061527BE16ACAA3A3`（MVID与用户日志相符）；System `A109C9C23A34DA847B08771475469FF2FFD3BF6FF7CB5DBDE0B48FB82245F75E`；System.Net.Http `7C42DDD0D9DD231E343C08876F8D925150B0BA4D34A7B9CF602AE55D28713132`。本次手机APK/其它BCL哈希未提供，不能自动确认7723其它重打包渠道。

最终包 `荒野探险家.netmod` 与 `荒野探险家-0.82.0.netmod` 字节一致：210条目、12753220字节，SHA256 `DF38D46AD7DA6FB79B20B5807EE752917DB2B8F6985AC34662F4DE4CC700675B`；DLL SHA256 `55A769B6A2863182C412B09A7DDC682C15DBF447DBBC061ADA91BD210B156185`，MVID `7745af7c-bde3-4860-a6bb-8580404e31d4`，ProductVersion=0.82.0。相对0.81.0只新增绘卷CSV/XDB/PNG，原条目仅DLL/modinfo变化，其余所有资源保持字节一致；唯一DLL为SCIEPort.dll，书典资源在同包。没有嵌入旧提交号、游戏依赖或第二份Mod。

日志在忽略目录 `artifacts/tiangong-full-build-final.log`、`tiangong-android-audit-final.log`、`tiangong-live.log`；额外ZIP/前版资源比对记录为`tiangong-package-check.json`。本地main提交后由用户同步源码；未在GitHub可访问前保留产物，不创建指向其它提交的Release。源码未推送，远端其它无关变更也未整体合并；后续合并需保留双方内容，不能强推覆盖。
