# 云天书典0.1.4：Android裁剪后的HTTP接口

后续反馈：用户2026-11-04确认本次独立0.1.4“测试通过”，并要求整合。当前实现/构建已迁入荒野探险家0.77.0，见[整合与安装](../SCIE-Port/docs/CELESTIAL-CODEX.md)。下文保留独立版当时的路径、命令与尚未复测状态，作为历史取证；整合包手机需单独验收。

日期2026-11-04，基于main `7b77b7c`。只修改独立CelestialCodex、检查及文档；本地提交，不推送。原工作区fetch配置仅跟踪port分支，本轮显式获取origin/main并从644aa47无冲突快进到7b77b7c；未修改远端配置。

## 证据与结论边界

用户提供：

```text
22:23:09.266 ERROR: [CelestialCodex] Network BCL stage=http
<=>:0
  at CelestialCodex.Networking.LibraryClient.Read (...) [0x00103] in <d223541dfc924976ac1f5c34dd433a83>:0
```

MVID匹配[0.1.3交付记录](CELESTIAL-CODEX-CANCELLATION-0.1.3.md)。摘录没有异常类型、消息或具体缺失方法，不能声称已由本次日志证明某个特定setter报错。World2枚举拒绝出现在HTTP之前，该路径不是书典缓存路径；没有修改世界权限或硬编码该Android路径。

本机开发者留存的2026-10-28 `assemblies.blob`按其manifest只读提取Android mscorlib/System/System.Net.Http，文件均放在忽略的artifacts中。其mscorlib MVID与用户先前0.1.2异常中的BCL MVID相同，但本次手机全部BCL哈希未提供，不扩大为所有7723渠道的兼容结论。

| 本机Android程序集 | MVID | SHA-256 |
|---|---|---|
| mscorlib | 9d0f885b-9255-4f54-bb3f-fdcb8916ab03 | C51F1D8E4F5701F3D3DBFF9A98658D5F74B387BF5911B0F061527BE16ACAA3A3 |
| System | 728dd446-58ad-4d7e-b228-dab9ec0a9ef5 | A109C9C23A34DA847B08771475469FF2FFD3BF6FF7CB5DBDE0B48FB82245F75E |
| System.Net.Http | 3b285e10-dc57-4c10-82bc-62d32bd874bb | 7C42DDD0D9DD231E343C08876F8D925150B0BA4D34A7B9CF602AE55D28713132 |

使用Mono.Cecil解析实际Mod全部属于这三个BCL的MemberRef，0.1.3存在四个缺失调用：

- `HttpClientHandler.set_AllowAutoRedirect(bool)`
- `HttpClientHandler.set_UseCookies(bool)`
- `HttpClientHandler.set_AutomaticDecompression(DecompressionMethods)`
- `MediaTypeHeaderValue.get_MediaType()`

前三者位于Get异步方法的直接调用体，会在Android JIT解析阶段失败，尚未到网络响应；第四个即使修复前三者也会阻断响应校验。不能只检查源码中有没有CreateLinkedTokenSource来判断所有BCL接口都可用。

## 修复

`HttpHandlerPolicy`创建全新handler，反射检查public setter；都存在时走public配置。裁剪后端仅接受已取证的`System.Net.Http.SocketsHttpHandler → System.Net.Http.HttpConnectionSettings`布局：读取HttpClientHandler的`_delegatingHandler`及后端`_settings`，先验证三个字段存在、可写、类型匹配，再设置`_allowAutoRedirect=false`、`_useCookies=false`、`_automaticDecompression=None`。最终用保留的三个public getter回读，任何失败都在发送之前抛错并释放handler。未知后端不尝试其它猜测字段。

上述字段在本机实际Android类中存在，其SetupHandlerChain/Clone实际使用这些值；MediaTypeHeaderValue.ToString重写也保留。媒体类型改用ToString后截取分号之前的精确`application/json`边界，拒绝jsonp/其它前缀；保留原CharSet检查。TLS验证及回调、源限制、identity、12秒预算/关闭取消、容量/解码/缓存规则均保持。

日志改为`http/deadline`、`http/handler`、`http/send`、`http/headers`、`http/body`细阶段。首行额外包含有界的异常类型和单行消息；完整堆栈继续保留。便于手机复制日志时即使漏掉后续行，仍能定位具体错误。

固定net472/API1.42/SC2.4.10.6，版本0.1.4/程序集0.1.4.0。956/DATA0、内部身份、入口、GUID、资源/分类、5条目包、API schema、缓存/进度与SCNET协议均保持；不改其它Mod、Digital-Study服务、玩家属性或地形。

## 实际验证

本机PowerShell运行时SDK由PATH提供；实际命令（仓库根目录）：

```powershell
$env:PATH = 'C:\Users\85196\AppData\Local\Microsoft\dotnet;' + $env:PATH
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir D:\Games\SCNET-x25.01.17
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir D:\Games\SCNET-x25.01.17
# 下条使用PowerShell 7；Cecil来自本机ilspy工具，不提交、不打包。
./scripts/Test-CelestialCodexAndroidBcl.ps1 -AndroidBclDir artifacts/celestial-audit/android-bcl -CecilPath D:\Dev\Tools\ilspy\.store\ilspycmd\9.1.0.7988\ilspycmd\9.1.0.7988\tools\net8.0\any\Mono.Cecil.dll -NegativeDll artifacts/celestial-audit/CelestialCodex-0.1.3-negative.dll
./tests/CelestialCodex.TargetTests/bin/Release/net472/CelestialCodex.TargetTests.exe D:\Games\SCNET-x25.01.17 (Get-Location).Path --https-probe
git diff --check
```

- 环境检查：固定Windows三程序集SHA匹配，API/Framework及原ABI检查通过。Release零警告、零错误。
- 纯客户端408项通过：原取消/超时/缓存/解码/迟到结果检查，加public路径、Android结构模拟、未知布局拒绝、回读不一致拒绝、媒体类型及首行诊断。用真实loopback TCP响应验证302原样返回、不跟随Location，连续两次请求不回传Set-Cookie。模拟结构是测试自有代码，不能冒充Android运行。
- 目标套件12360项，350条外部成员解析；含0.1.3重建负对照、原生CSV/XDB/PNG和UI异常隔离，当前本机已装包0个。无手机或完整游戏运行。
- Android快照：237个Mod直接BCL成员引用全部可解析，并核验私有布局与ToString重写；不允许桌面/GAC解析结果蒙混通过。0.1.3源码重建负对照恰好缺上述四个成员。此负对照MVID为`d4900f8f38a04cffbfb88b7c902c3fb8`，是本机未改动7b77b7c源码重建，**不是**用户安装的d223541原始DLL；原始手机包未在本工作区取得。
- 实际0.1.4 Mod DLL公网传输及Protocol.Decode：目录20本、clouds-and-stars详情/c1正文三次200，4279/362/6211字节，正文2097 UTF-16单位，revision `1ab420b0764a28b0916d0708089e48de3a171f28e96c8c031324f5ad5e61f55e`。只读，没有部署或写入生产服务。
- 包内5条目逐字节匹配编译/资源文件；两个包24788字节、SHA-256 `EFEBDDCA76A57C629A8405C77A95B7C0194110B40114CC769C8E7159E23A33E3`，DLL MVID `f2d9d0127cb244fb9b44d8de3eaf0d46`。无游戏DLL/PDB/APK/BCL或其它专有资源。

## 手机复测

替换为`artifacts/CelestialCodex/云天书典-0.1.4.netmod`并完全重启，在列表核对0.1.4，只保留一个书典包；各参与联机端使用相同文件，不必删除缓存或存档。先用可丢弃单人创造世界刷新，验证目录→选书→章节正文，再验证缓存离线、关闭加载中窗口/重新打开、退出世界。触屏多分辨率、真实Local/Server/Client仍需分别验收。

若仍失败，保留新日志首行`stage=... type=... message=...`及后续异常/MVID。私有ABI变更将安全拒绝，需要新的运行时证据，不会静默放开请求策略。

| 范围 | 状态 |
|---|---|
| 本机Android快照直接成员/策略结构、Windows构建/隔离/HTTP/包 | 已确认 |
| 用户所示0.1.3在线阅读 | 不支持 |
| 0.1.4 Android7723完整阅读/TLS/缓存/触屏及Local/Server/Client | 待验证 |
