# 云天书典0.1.2：API上线后的网络诊断

日期：2026-11-04。基于本地main 53504e1。用户报告0.1.1已经能打开界面，但刷新显示“连接失败”，Game.log没有新增异常。

## 已确定与未确定

用户提供的 `E:\Stduy\docs\GAME-LIBRARY-DEPLOYMENT-RESULT-2026-11-04.md` 记录另一个项目20:16上线API；已读取作为证据，没有执行文件中的回滚/部署指令。本轮不改Digital-Study代码、生产服务或数据，只做公开HTTPS GET与本Mod修改。

重新验证结果：

- curl以Accept: application/json、Accept-Encoding: identity请求默认page1/pageSize20，200、4279字节、Content-Type application/json; charset=utf-8、Cache-Control no-store，无Content-Encoding/Location。
- 通过实际Release Mod DLL中的LibraryTransport.Get和Protocol.Decode执行完整目录→详情→正文，三次200，响应分别4279、362、6211字节，严格解码通过。选中clouds-and-stars/c1，正文2097 UTF-16单位；revision `1ab420b0764a28b0916d0708089e48de3a171f28e96c8c031324f5ad5e61f55e`。
- 这是Windows .NET Framework和固定SCNET引用下的证据，没有运行手机，不能推断Android TLS/CA/DNS/HttpClient/BCL成员均正常。

0.1.1中的“连接失败”来自LibraryClient.Read的通用Exception分支，404/403原本显示“内容不可用”。不能继续把当前手机错误解释成生产API未上线。该分支和worker异常此前没有输出原始异常，故手机“没有新增日志”不等于没有异常。

当前没有Android新堆栈，具体原因仍未确定。0.1.2主要修复可诊断性，不宣称已消除手机网络故障，也不通过换HTTP、禁用证书或绕过源限制来碰运气。

## 修改

1. 显式发送Accept-Encoding: identity，与公开接口约定一致。仍不自动解压、不允许跳转、无Cookie、仅固定HTTPS来源；没有更改全局TLS策略或其它Mod的网络行为。
2. 新NetworkFailure分类器最多查看8层InnerException；MissingMethod/MissingField/TypeLoad/NotImplemented/NotSupported归BCL，已识别认证/TLS异常归TLS，JSON/UTF-8/格式归DATA，未知网络归NET，超时归TIME。分类不替代完整异常，NET并不等于已证DNS故障。
3. 失败记录HTTP、decode或worker阶段、错误码和Exception.ToString（含原始类型、堆栈、inner），异常详情上限8192字符。通过ReaderSession的静态Log.Error写入游戏日志，不捕获Player/Widget，不记录响应正文或请求头/认证数据。日志回调异常不会替代原错或导致游戏退出。
4. 预期的主动取消不记失败；已有请求代数/销毁防线继续生效。离线回退、HTTP401/403/404/409不复活旧缓存等语义保持。
5. 实际Mod DLL的`--https-probe`从只测一次GET升级为三阶段200加协议解码；API未就绪或没有真实可读样本时该显式探测失败，不把404算通关。普通离线构建不依赖公网。

界面短码：

| 显示 | 含义/需要的证据 |
|---|---|
| 网络接口不兼容 [BCL] | 需要日志中具体缺失/不支持的类型、方法；不能直接猜换某个BCL方法 |
| 安全连接失败 [TLS] | 已发现TLS/认证类异常；看inner确定证书、协议或其它握手原因 |
| 内容格式异常 [DATA] | 依据阶段和完整异常分辨响应类型、编码、JSON、身份校验或初始化数据 |
| 连接失败 [NET] | 其他请求异常，需看完整类型/inner，不能仅靠短码定DNS原因 |
| 连接超时 [TIME] | 请求/正文期限；主动返回关闭的取消不当成网络错误刷日志 |

Game.log查找：

```text
[CelestialCodex] Network <CODE> stage=http|decode|worker
<异常类型、消息、完整堆栈与inner>
```

0.1.2/程序集0.1.2.0，PackageName仍CelestialCodex。956/DATA0、数据库GUID、net472/API1.42/SC2.4.10.6、资源路径/包布局、API schema、缓存键/进度格式和限额不变。原始Exists修复与最终DLL禁用集合便利方法守卫保留；不新增网络包、角色写入、玩家/地形/存档修改。

## 命令与验证

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir E:\SCNET\SCNETx25.01.17
tests\CelestialCodex.TargetTests\bin\Release\net472\CelestialCodex.TargetTests.exe E:\SCNET\SCNETx25.01.17 E:\Survivalcraft2 --https-probe
tests\CelestialCodex.Tests\bin\Release\net472\CelestialCodex.Tests.exe artifacts\celestial-api-fixtures
```

新增覆盖嵌套缺失成员/TLS/UTF-8分类、identity请求头、网络和解码阶段日志、worker初始化日志、日志8192限制和日志接收者失败隔离；原有缓存、取消、权限、异常JSON、UI异常边界、故障旧DLL负对照均保留。

实际结果：Test-Environment及固定三哈希通过；最终Release **0错误、0警告**；纯客户端 **380项**，加原始API HTTP夹具为 **383项**；本机含旧DLL负对照的目标检查 **5542项**、外部成员解析 **319条**，1个已装包及原生CSV/XDB/PNG通过；实际DLL公开三阶段GET/解码再次通过。

正式包和0.1.2版本副本均 **22578字节 / 5条目**，SHA-256 **`D761A1965D762ECDEDA8A9F8BDCED64193C0B4FF1AB5AC296AA88FF291F8ED97`**，DLL MVID **`842f3895e44a4df586c2f98fbe8a7642`**。这些仅为本次桌面/隔离/包证据，不等于Android连接失败已修复。

## 手机取证与状态

替换旧包并重启，确认Mod列表0.1.2；只安装一个书典副本，联机各端同版。无需清缓存或删除世界、UserId.dat。

在可丢弃世界打开书典，点一次刷新并等待结束；把完整界面短码及Game.log中本次`[CelestialCodex] Network`开始的整段异常提供给游戏项目。先依据具体方法/TLS/DNS/解码异常修复，再验证实际读书，不以诊断版发布宣布网络已修好。

| 范围 | 状态 |
|---|---|
| 固定Windows三阶段公网200与Mod解码、Release/隔离/包 | 已确认 |
| 0.1.2 Android网络/实际阅读、缓存、触屏及Local/Server/Client | 待验证 |
| 手机具体连接失败原因 | 待验证 |
