# 云天书典0.1.3：Android链接取消令牌接口缺失

日期2026-11-04。基于本地main d691125，只改独立Mod、测试与文档；不更改Digital-Study、生产服务、其它Mod或游戏/BCL文件。

## 实机证据

用户刷新显示“网络接口不兼容 [BCL]”，日志：

```text
20:57:43.901 ERROR: [CelestialCodex] Network BCL stage=http
System.MissingMethodException: Method not found: System.Threading.CancellationTokenSource System.Threading.CancellationTokenSource.CreateLinkedTokenSource(System.Threading.CancellationToken[])
  at System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].Start[TStateMachine] ... in <9d0f885b92554f54bb3ffdcb8916ab03>:0
  at CelestialCodex.Networking.LibraryTransport.Get ... in <842f3895e44a4df586c2f98fbe8a7642>:0
  at CelestialCodex.Networking.LibraryClient.Read ... in <842f3895e44a4df586c2f98fbe8a7642>:0
```

Mod MVID与本地0.1.2实际DLL相同，包SHA-256为 `D761A1965D762ECDEDA8A9F8BDCED64193C0B4FF1AB5AC296AA88FF291F8ED97`。原调用传单一token，C#选中了params数组重载；该重载在用户BCL缺失，Get启动时即失败，不能归因于API未部署或泛称TLS问题。没有APK/BCL文件哈希，证据只对应所示运行时与已证缺失方法。

用户同段日志包含World2目录权限错误及m.schub.top DNS失败，二者不是书典固定HTTPS来源或缓存路径，未做无关权限、存档和DNS修改。

## 修改及接口核对

- 新RequestDeadline只创建自有CancellationTokenSource。后台每25ms检查调用方IsCancellationRequested及Stopwatch单调时钟预算，条件达到后Cancel自有源并传给HTTP/正文读取。
- 不调用任何CreateLinkedTokenSource重载；同时移除CancelAfter，不使用CancellationToken.Register或额外Timer接口，避免仅切换到另一未经手机取证的重载。
- 请求finally中异步Stop观察任务，等其结束后再Dispose源，防止观察任务在释放后调用Cancel。正常完成不触发超时；主动关闭继续取消后台任务，不向已销毁UI发布结果。
- 目标CancellableProgress实际采用普通CTS构造/Token/Cancel；目标游戏IL实际调用Task.Delay(int)，Engine.Time.RealTime实际使用Stopwatch.GetTimestamp/Frequency。以这些实际接口构成替代，未按其它SCAPI或新.NET版本猜API。
- GetAsync改为Uri重载，与固定目标WebManager实际调用一致；不声称旧string重载已被手机证实缺失。
- TLS默认校验、禁重定向、无Cookie、identity、12秒预算、正文限额、UTF-8/JSON检查及异常日志全部保留；没有采用会放行证书错误的原生WebManager.Get，也没有改全局TLS配置。
- 最终DLL守卫拒绝CreateLinkedTokenSource与CancelAfter；测试程序集故意编译的旧调用和本机保存的真实0.1.2故障DLL都须被拒绝。旧DLL仅留忽略的artifacts，不提交或打包。原List.Exists旧DLL负对照继续运行。

0.1.3/程序集0.1.3.0；PackageName仍CelestialCodex，956/DATA0、模板GUID、资源、分类、net472/API1.42/SC2.4.10.6、5条目包、API schema、缓存/进度格式及限额保持。不新增SCNET网络包，不修改玩家、服务器阅读状态、世界或地形。

## 实际验证

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir E:\SCNET\SCNETx25.01.17
tests\CelestialCodex.TargetTests\bin\Release\net472\CelestialCodex.TargetTests.exe E:\SCNET\SCNETx25.01.17 E:\Survivalcraft2 --https-probe
tests\CelestialCodex.Tests\bin\Release\net472\CelestialCodex.Tests.exe artifacts\celestial-api-fixtures
```

新增测试真实短超时取消等待、调用方取消传递、在途HTTP取消、预取消、完成后观察停止、不提前取消已完成操作、计时边界/计数溢出；验证不依赖被禁用的链接工厂。原有异常、缓存、日志、UI隔离、原生CSV/XDB/PNG和包检查保持。公开探测通过实际Mod DLL传输及Protocol.Decode读取目录/详情/正文，不写生产内容；Android仍需复测。

实际结果：环境与固定三程序集哈希通过；最终Release **0错误、0警告**；纯客户端 **390项**通过，加原始API夹具为 **393项**；本机含真实0.1.2/0.1.0负对照的目标检查 **9018项**、外部成员解析 **333条**。两份实际故障DLL均被新守卫按预期拒绝，原生CSV/XDB/PNG及1个已装包的身份检查通过。

实际Mod DLL的公网传输及协议解码再次通过：目录20本、clouds-and-stars详情/c1正文，三次200，4279/362/6211字节、正文2097 UTF-16单位，revision为 `1ab420b0764a28b0916d0708089e48de3a171f28e96c8c031324f5ad5e61f55e`。未执行手机或完整游戏。

两个0.1.3包均 **23646字节 / 5条目**，SHA-256 **`36976F343CE2B0E21906B56CE5C7A9AFFD8B7E5C38CA6C1FCF0B0597FB9858A1`**；DLL MVID **`d223541dfc924976ac1f5c34dd433a83`**。用于后续手机日志核对；不包含原版DLL/PDB/APK或任何专有资源。

## 替换与复测

安装 `artifacts/CelestialCodex/云天书典-0.1.3.netmod`（或同内容正式包），替换0.1.2并重启，确认Mod列表0.1.3；只保留一个书典包，各联机端同版。缓存和存档不必删除。

1. 可丢弃单人创造世界点刷新，检查是否出现目录，再选书/章节实际阅读。
2. 加载时关闭、重新打开、退出世界；确认没有迟到UI错误。断网时验证有缓存可读、未缓存短反馈。
3. 若有新错误，继续提供本次`[CelestialCodex] Network`完整异常和新MVID；不要用旧MVID日志断定新包失败。

| 范围 | 状态 |
|---|---|
| 0.1.2在用户日志环境在线阅读 | 不支持 |
| 0.1.3固定Windows编译、故障引用移除、取消/超时/释放、负对照与包 | 已确认 |
| 0.1.3 Android完整阅读、缓存及Local/Server/Client | 待验证 |

移除本次已证缺失接口不代表所有Android网络/BCL接口都已验证。
