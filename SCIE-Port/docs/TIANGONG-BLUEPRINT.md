# 天工绘卷

## 2026-11-12 18:10 三次 BCL 修复：独立 TCP/TLS 通道

用户已替换成上次修复包：手机日志MVID `94bc260d-014b-4110-bcb1-8e230d6fcfac` 与产物一致。本次缺失的是 `HttpWebRequest.set_AllowAutoRedirect(bool)`，并非 `TaskFactory.FromAsync` 再次出现；异常在请求初始化即发生。此前Windows检查仅证明桌面行为，不能证明手机BCL。本次同时移除蓝图生产路径的 `WebRequest`/`HttpWebRequest`，避免在两个已报告缺成员的高层HTTP实现之间继续切换。未取得手机BCL，不能断言其具体成因为裁剪或推断新接口已被手机确认。

`BlueprintCloudConnection` 通过 `TcpClient.BeginConnect`/`EndConnect` 与自有完成源连接，后台 `SslStream(Stream, false).AuthenticateAsClient(host)` 使用系统证书链、主机名验证和TLS协议选择，没有证书放行回调或全局TLS修改。关于该接口的契约见[Microsoft文档](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream.authenticateasclient?view=netframework-4.7.2)。不复用游戏 `WebManager.Get`，因为固定Windows目标中的实现放行证书错误、吞异常且没有响应容量限制。APM仅适配连接，不使用 `TaskFactory.FromAsync`；同步TLS/HTTP读取在后台进行，观察器关闭socket以中止连接/握手/头部/正文。12秒统一预算与用户取消继续由原截止观察器提供，APM完成源在取消时一并结束等待。直连固定HTTPS来源，不经过HTTP代理或接受响应链接；HTTP仅供内部loopback检查，生产Transport仍先执行原精确HTTPS白名单和规范路径校验。

HTTP/1.1仅发送一次GET/Host/JSON/identity/Connection: close，不处理Location、Cookie、认证挑战、压缩或重试。`BlueprintHttpReader`在最终非200状态行即拒绝并保留原状态；支持固定长度、chunked及连接关闭定界，沿用JSON/UTF-8与2MiB正文限额。新增头部、临时响应、chunk元数据与尾部累计32KiB上限、最多4个临时响应/16384块；拒绝重复关键头、Content-Length与Transfer-Encoding并存、未知传输编码、非法长度/CRLF、受保护尾字段与不完整正文。原目录缓存、HTTP撤回/权限策略、下载哈希/长度及原导入器保持；957、材料/家具/schema、施工/撤销、网络协议和书典源码均未改。

Windows环境/编译与820项蓝图检查通过，真实生产HTTPS目录1529字节/total3和一份154162字节蓝图已读取，长度与SHA-256匹配。新增本地TLS检查确认自签名/主机名不匹配证书在发送GET前被拒绝，取消能中断停滞TLS握手；旧DLL负对照被新增WebRequest调用守卫拒绝。这些是桌面证据；Android7723完整TcpClient/SslStream/BCL/TLS与真实Local/Server/普通Client仍**待验证**。

交付继续为0.80.0本地修复开发包，MVID `9f6344be-8745-41d7-9241-efa765e8ef75`，`荒野探险家-0.80.0.netmod` 11637037字节；正式名副本一并提供。不改modinfo，不触发或覆盖GitHub Release/tag。替换旧0.80.0包，避免同时安装相同PackageName的两份包，再测试刷新/下载；若还有BCL，须以新MVID及完整缺失成员堆栈继续定位。命令和夹具问题见[测试记录](TEST-RESULTS.md)。下面两轮记录保留为历史，已被本节替代。

## 2026-11-12 云端刷新 BCL 修复

用户日志在14:14:05–14:14:06连续报告 `Cloud BCL stage=http`，缺少 `System.Net.Http.HttpClientHandler.set_AllowAutoRedirect(bool)`，Mod MVID 为 `938f9691-4277-43cb-8d7b-9b66f8f830af`。这证明该运行库不能执行旧请求初始化；尚未取得此Android/7723运行库的程序集/哈希，不能据Windows程序集推定其它成员可用。

天工绘卷默认请求改为 `HttpWebRequest` 的 Begin/End 异步接口，完全移除蓝图模块的 `HttpClientHandler` 引用。显式禁止重定向，新请求无Cookie容器/凭据/自动解压，不设置全局TLS或证书回调，沿用系统证书验证。12秒截止与用户取消均通过观察器调用 `Abort`，覆盖等待响应头和正文读取；响应仍经同一状态码、JSON/UTF-8、编码与2MiB校验。HTTP 4xx/5xx从WebException保留原状态，断网映射NET、证书/安全通道错误映射TLS；原缓存与下载哈希/导入校验保持。

本轮保留0.80.0版本号，只提供本地修复开发包并提交main，不触发基于modinfo变化的自动Release，也不覆盖历史tag/Release。修复DLL以构建MVID区分。957/DATA0、Hook、239/254协议、材料/家具/schema、施工/撤销和书典源码均未改。Windows编译和生产HTTPS只读请求已成功；Android新通道及真实单机/主机/普通客户端仍**待验证**。原始日志环境的旧云端请求为**不支持**；不能把新接口在Windows可用写成手机兼容已确认。实际命令与结果见[测试记录](TEST-RESULTS.md)。

### 15:33 二次 BCL 修复（同一 0.80.0 开发包）

14:14 修复包（MVID `e54e1e3c-33b2-4b33-880c-1f66b999db5f`）在 15:33 实机日志仍报 `Cloud BCL stage=http`，缺失 `System.Threading.Tasks.TaskFactory.FromAsync<!0>(...)`：该运行库没有 `TaskFactory.FromAsync<TResult>(Func<AsyncCallback,object,IAsyncResult>,Func<IAsyncResult,TResult>,object)` 泛型 APM 适配器，异常在 `BlueprintCloudWebRequest.Get` 的 `AsyncTaskMethodBuilder.Start` 抛出，请求未发出。本轮保留 `HttpWebRequest` 通道，改用原生 `BeginGetResponse`/`EndGetResponse` 配对 + `TaskCompletionSource<WebResponse>`，并让生产适配器直接读取 `HttpWebResponse`（不再包装 `HttpResponseMessage`/`StreamContent`，不再用 `ReadAsStreamAsync`/`Stream.ReadAsync`）；同步 `Stream.Read` 由观察器 `Abort` 中断，12 秒截止与用户取消语义不变。响应仍经同一状态码、JSON/UTF-8、Content-Encoding、2MiB 声明与分块上限校验；HTTP 4xx/5xx 保留原状态，断网 NET、TLS 错误映射 TLS，原缓存、下载哈希与导入校验保持。

`GuidCompatibilityProbe` 的 IL 审计新增拒绝蓝图模块 `TaskFactory.FromAsync` 以及生产适配器对 HTTP 内容适配器的引用，防止回归。957/DATA0、Hook、239/254 协议、材料/家具/schema、施工/撤销、书典源码、版本号和 modinfo 均未改；修复 DLL MVID `94bc260d-014b-4110-bcb1-8e230d6fcfac`。Windows net472 编译、蓝图 781 项（含真实 loopback HTTP 全路径）与 27 项 GUID/IL 审计通过；Android 7723 的新通道、刷新/下载/离线缓存与真实 Local/Server/普通 Client 仍**待验证**。用户应替换旧 0.80.0 开发包（不要同时保留两份相同 PackageName）再刷新；若仍报错，以新完整缺失成员堆栈及日志 MVID 继续定位。实际命令与结果见[测试记录](TEST-RESULTS.md)。

2026-11-11；基线 Builder Assistant 0.25.1 / main `3b6b08e`。用户明确要求把蓝图全部功能移植为荒野探险家的神器，并要求普通联机玩家也能使用。因此本次允许将蓝图相关原创源码独立适配至 SCIEPort；不添加项目引用，不打包 BuilderAssistant.dll，不移植快艇、自动房屋/巨树生成器或建筑工作台。建筑助手原有包及文件保持独立。

## 功能与身份

- 天工绘卷：957 / DATA0，神器分类，青玉蓝图底色、古金边框。持有步速自有增量 +20%，近战频率 ×1.1，与四圣兽倍率相乘。
- 保持 schema 1/2/3、材料别名、朝向/DATA、家具模板、JSON 导入导出、本地保存/删除/重名保护、云端分页/下载/离线缓存、两点复制、90°旋转、镜像、升降/重选落点、范围/冲突预览、清障二次确认、分批施工/取消/撤销。
- 私有库 `app:/SCIEPort/Blueprints/blueprints`，不自动改写建筑助手文件；两者通过相同 JSON 交换。
- 新增图谱第九位（bit8），旧八位位置保持，包254字段仍为原 uint；旧存档只新增位。旧版混装不支持，房间各端须同版。

## 联机与安全设计

客户端仅做读取、蓝图库和可视预览。施工蓝图通过有界分片上传给主机，主机重新验证 JSON/材料/范围、真实地形、身份/持有/图谱权限，保存快照并返回确认票据；玩家确认后主机再次核对快照再开始。清障默认关闭，覆盖须明确开启并二次确认。所有地形写入及回滚在世界子系统主线程 Update，每帧最多32格；客户端不调用 ChangeCell。原生地形同步负责向房间分发结果。取消和20层撤销历史按 PlayerGuid 隔离；同世界施工串行，避免多个玩家交错事务。断线、死亡、换物品或权限丢失停止施工并分批回滚；世界卸载释放所有会话。容器、实体、未知模组方块等不在可覆盖白名单内，不能用清障破坏内部物品/未知状态。精确限制见下表。

## PixelFX选型（实现前记录）

已阅读开发标准、V0.1、0.77.1优化、0.78.0多神器接入及固定目录（上游 `a84159688af3ca37a4be05a0d422b94451b2e24e`）。检查已有 holy/blessing、holy/sunburst 及目录候选 arcane/magic-circle-cast、scifi/hologram、scifi/laser-grid。既有九个clip是短时战斗演出，无法表示可旋转/升降的精确方块边界；目录候选未取得源码/导出证据，不能当作已适配。结论：不采用新精灵；复用真实3D包围盒、朝向和最多128个冲突标记，无全屏闪烁、额外图集/持续循环/实例预算。三维预览随换物品、取消、死亡、退出释放。新HUD图标使用内置 image_gen，来源另存。

## 验收状态

Windows固定ABI/net472编译及原生隔离/模拟联机检查通过；Android/7723、触屏多分辨率及实际 Local/Server/普通 Client 均为待验证。编译或模拟网络成功不等于真实联机已验证。

| 约束 | 本版取值 |
|---|---|
| 蓝图 | UTF-8 2MiB；schema1/2/3；单轴64；每事务10000格；原81材料/10家具模板 |
| 传输 | 4096字节分片；逐片ACK；每命令2秒重发、最多4次/15秒；整体180秒；最多4份上传或待确认蓝图 |
| 主机请求队列 | 最多128包，每Update最多处理8包；每玩家Begin间隔0.5秒；最多32会话；满时只淘汰离线且无待恢复记录的旧会话 |
| 位置与权限 | X/Z ±1000000，Y 0–255；发起时原点距玩家128格内；最终快照每格检查边界/原生领地权限；冒险世界禁止新施工 |
| 确认 | 主机快照票据30秒；单次消费；确认和每次写入重查身份/连接/持有/真名/地形，地形变化不自动覆盖 |
| 每帧预算 | 复制扫描256格；权威施工/失败回滚/取消/撤销总计32格，门的同步上半格计入预算；世界内只推进一个地形任务 |
| 撤销 | 每玩家最多20笔；全世界已完成历史合计100000格上限；只改仍等于本次写入值的格子；未恢复记录保留并阻止新施工，允许暂停后再撤销 |
| 表现 | 自有3D绘卷；image_gen透明圆形256px运行图；72逻辑单位HUD/圆形命中；预览最多128冲突框；布局无安全区域时隐藏面板 |

图谱原uint字段追加第九位而不改变254包布局。旧255掩码只解放原八种神器，新绘卷仍须单独解放；回退旧版本时旧位可读，但957及239通道不能由旧包支持，回退前备份世界并移除新物品。原有书典956、图谱955及全部旧ID/GUID不变。`kind`沿用`builderassistant.blueprint`以读取既有文件，并不依赖建筑助手安装。

本地库按设备保存，联机上传仅用于本次施工，不向其他玩家公开私人库，也不自动写入房主蓝图库。撤销历史与未完成任务保存在本次世界内存，会话结束不提供跨重启撤销；正常退出前完成施工/恢复。原生植物/重力/流体/电路等后续世界副作用不承诺完整回滚。清障额外拒绝无法安全还原的旧方块；不会复制容器库存或未知实体。新绘卷不可放置，不属于家具回收范围。旧建筑助手与SCIEPort既有900–904号段有冲突，不应把两个包同时安装来取得蓝图能力；通过JSON交换即可。

## 实际开发验证命令

以下为发布流程外的手动开发检查；快速打包仍只编译与压包，不调用测试/包审计，也不创建GitHub Release。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:/SCNET/SCNETx25.01.17
dotnet run --project SCIE-Port/tests/SCIEPort.Blueprints.Tests/SCIEPort.Blueprints.Tests.csproj -c Release -- E:/SCNET/SCNETx25.01.17 E:/Survivalcraft2/SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll
dotnet run --project SCIE-Port/tests/SCIEPort.Inventory.Tests -c Release --nologo
powershell.exe -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-Codex.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -AssemblyPath SCIE-Port/src/SCIEPort/bin/Release/net472/SCIEPort.dll
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-WildernessExplorerFast.ps1 -GameDir E:/SCNET/SCNETx25.01.17
```

环境检查登记Windows三哈希匹配；C#编译0警告/0错误。自有蓝图检查涵盖原生包239编解码/ID避冲突、模拟非主玩家且非管理员/房主权限、身份伪造、乱序/重传、过期票据、快照变化、并发串行、按人撤销、断线/失败/取消回滚、未加载撤销与旧票据不能绕过恢复锁、默认不覆盖/容器及领地保护、扫描与写入预算、原生PNG解码与alpha、3D网格、物品属性、云端白名单/限流/断网缓存/哈希/版本/取消/12秒响应体超时。云端检查使用模拟HTTP，不代表手机实际HTTPS成功。图谱测试通过439项原生权限/存档/输入检查，兼容解析1347项、27项GUID审计（3801方法/29780调用位点）；物品栏主题793项。蓝图专项750项通过，最终本地包见[测试记录](TEST-RESULTS.md)。

| 实机步骤 | 状态 |
|---|---|
| Android7723新建单人：解放绘卷、复制、剪贴板、本地/云端、旋转/镜像/升降、清障与恢复 | 待验证 |
| 同0.80.0真实房间：房主、两名普通客户端各自施工/取消/撤销，相互观察及晚加入 | 待验证 |
| 高延迟/断线重连、切换持有/死亡、领地变更、区块卸载/恢复，无重复/丢失/越权 | 待验证 |
| 左右手、20:9/16:9/窄屏、不同UI比例及原生触控避让，面板隐藏后HUD/点天空退出 | 待验证 |
| 复制上限/大蓝图与多神器同场，帧时/峰值内存/发热、读档及世界退出释放 | 待验证 |

图标提示词、原图、PNG与SHA来源见[来源目录](assets/tiangong/prompt.md)及[source.json](assets/tiangong/source.json)；蓝图源码适配范围与原文SHA见[blueprint-source.json](assets/tiangong/blueprint-source.json)。
