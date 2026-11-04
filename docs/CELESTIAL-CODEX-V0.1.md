# 云天书典 · Celestial Codex V0.1 交付与验收

后续记录：用户已确认0.1.0在其Android运行时因List.Exists缺失而使用闪退；本页保留原始0.1.0交付与桌面证据。最新0.1.1安装包及手机复测范围见 [0.1.1闪退修复](CELESTIAL-CODEX-ANDROID-HOTFIX-0.1.1.md)。

日期：2026-11-04。两仓库均基于现有本地 main；不改远端、不 push、不部署 Digital-Study 生产、不执行内容同步。Digital-Study 本次本地提交 `008e793`；已有前端改动保留，不包含在本次提交中。

## 工程边界与身份

| 项目 | 值 |
|---|---|
| 工程 / 程序集 / 根命名空间 / PackageName | `src/CelestialCodex` / `CelestialCodex` |
| 版本 | `0.1.0`；Assembly/File `0.1.0.0` |
| 目标 | `.NETFramework,Version=v4.7.2`，SCNET `Net_x25.01.17`，ApiVersion `1.42`，ScVersion `2.4.10.6` |
| 物品 | `CelestialCodexBlock`，Index **956**，DATA **0**，非放置、非武器、最大堆叠1、独立创造分类「云天书典」 |
| 子系统模板 | `8e7fc4af-2b7a-4b68-a02e-5c5443b1ece2` |
| Class 参数 GUID | `58451814-3a27-4453-b96b-8d87e9241368` |
| Project Member GUID | `00999a64-5d27-4a0a-a4d8-7b42c7593782` |
| 唯一 Mod 资源 | `Assets/Blocks/CelestialCodex.csv`、`Assets/Database/CelestialCodex.xdb`、`Assets/Textures/CelestialCodex/Book.png` |
| 正式包 | `artifacts/CelestialCodex/云天书典.netmod`；版本副本 `云天书典-0.1.0.netmod` |

BlockBehaviors/Project GUID 为原生合并容器，不是新建身份。新增3个 GUID 已与原生完整数据库及本仓库其他工程 XDB 比对。956 与原生 Block 常量、其他工程源码中的物品常量及本机 NetMods 中1个已安装包的 DLL 常量均不冲突。PackageName 与 BuilderAssistant、SCIEPort、SurvivalcraftRuins 分离。对未来或任意第三方 Mod 的 ID 不作保证。

本工具不是神器。用户明确要求非武器且不改玩家属性，因此不套用神器 +20% 移速、+10% 攻速与神器物品栏主题规则；该例外不改变 SCIE-Port 标准。近战/投掷威力为0，没有新武器技能；原生挖掘/物品丢弃等游戏动作仍由游戏控制。

## 前置审计与实际 ABI

已阅读两仓库 AGENTS、根兼容性矩阵；检查 BooksService、BookResourcesService、ContentService、SCNET ModEntity/ModLoader/Block/BlocksManager/Dialog/ScrollPanelWidget/LabelWidget/DialogsManager/ModsManager/Engine.Storage。

本机开发引用：`E:\SCNET\SCNETx25.01.17`。不把此桌面路径嵌入运行时 Mod。三程序集 SHA-256：

| 文件 | SHA-256 |
|---|---|
| Survivalcraft.exe | `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD` |
| Engine.dll | `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694` |
| EntitySystem.dll | `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C` |

- 加载器扫描 ModLoader 子类，调用 `__ModInitialize()`；Block 子类必须有 public static int Index。没有注册自定义 Hook 或网络包。
- CSV 按 `Class Name` 查类型、其余列按真实 public 字段解析；目标原生 `BlocksManager.LoadBlocksData` 已执行本 CSV。自有纹理必须 `GetTextureSlotCount=1`，否则原生 FlatBlock 仍按16×16图集采样；已补齐并检查。
- XDB 使用目标原生 `ModsManager.CombineDataBase` 合并，再经 `TemplatesDatabase.XmlDatabaseSerializer.LoadDatabase` 反序列化验证。行为入口是 `SubsystemBlockBehavior.OnUse(Ray3,ComponentMiner)`，世界清理为 `Dispose()`。
- 原生 `PlayerData.IsMainPlayer` 识别本机玩家；Server 中远端玩家、Client 中其他玩家、无 GameWidget 的服务端不打开 UI。阅读无角色权威写入需求。
- Native Dialog 的 Canvas/StackPanel/ScrollPanel/Label/BevelledButton 实现目录、章节分页、正文滚动、5档字号和返回。44–54逻辑单位触屏按钮；按 GUI 根视口留至少16或4%边距，限制面板最大720×720。尺寸数学覆盖320×240、480×320、1280×720、720×1280、2340×1080；这不等于已观察 Android 刘海/系统手势区或实际触屏效果。
- 使用实际字段 `ModsManager.UserDataPath` 的父目录。Windows 字段是文件 `app:/UserId.dat`；运行时反射取值，避免编译器内联 Windows const，后续路径全部交给 Engine.Storage。Android 渠道此字段及 Storage 实现仍须比对。

原生 `WebManager.Get` 在此 Windows 版放行证书错误、吞掉请求异常且无容量上限，故不使用。选择同一目标程序集实际使用的 System.Net.Http / Task / Stream 异步技术，保留默认证书验证、不重定向、无 Cookie、无压缩、12秒取消令牌覆盖请求与正文读取、检查 MIME/UTF-8/响应长度。全部最终 DLL 外部成员引用在本机 Windows .NET Framework 和固定游戏引用下可解析；另运行实际 HTTPS，只读 GET 得到 HTTP 404，证明当前 Windows 通道可连接，但生产新端点尚未就绪。

没有 Android APK/BCL 程序集或真机可用，因此不能把上述 HttpClient/Task/CTS/UTF8Encoding 或其他 BCL 成员的 Windows 可用性推断为 Android 已确认。DLL 不引用已知手机缺失的 System TryParse、Guid.Parse/TryParse 或 LINQ。若渠道 ABI 不符，读书入口/后台失败显示简短状态，不能用空实现宣称已适配。

## API 与缓存

Digital-Study 独立只读模块的完整契约见该仓库 `docs/GAME-LIBRARY-V1.md`。运行前需由运营者另行发布 API，本轮禁止生产部署。

```text
GET /api/game-library/v1/books?page=1&pageSize=20
GET /api/game-library/v1/books/:slug
GET /api/game-library/v1/books/:slug/chapters/:id?revision=<64位SHA256>
```

目录响应 `{schemaVersion:1,page,pageSize,hasMore,books:[{slug,title,author,resourceId,sourceMode,revision:null,status,reason}]}`。详情同样的书籍字段加 `chapters:[{id,title}]`；可读详情的 revision 为实际内容 hash。正文响应同样的书籍字段加 `chapter:{id,title,text}`。AI `sourceMode=ai_reading`，原著 `original`；UI 的 AI 来源标记保留。例：

```json
{"schemaVersion":1,"slug":"example-book","title":"示例书","author":"作者","resourceId":"legacy","sourceMode":"ai_reading","revision":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","status":"ready","reason":null,"chapter":{"id":"c1","title":"1. 第一章","text":"第一章\n\n正文。"}}
```

这里 hash 仅为示意；客户端必须使用实际目录返回值。分片按段落≤12000 UTF-16单位，最多512片，V0.1 不解析 EPUB/PDF 或还原其原生目录。服务端转换去掉脚本、样式、外链和图像，只返回文本。原著文本优先，AI 备选明确标记；旧站公开 content.html 仅在完全没有资源记录时做只读虚拟 AI 映射，不补写数据库。

缓存键是 SHA-256(endpoint+请求路径)，正文键包含 revision；网络 JSON 完整校验成功才入缓存。UTF-8 严格解码；自有有限 JSON 读取器拒绝重复键、尾随内容、过深嵌套、异常 surrogate、超长字符串与非法类型。无需外部 JSON 库。只允许配置的 `https://wxhappylife.top:443` 来源和两个明确 API 前缀，服务端返回的链接不会被跟随。

缓存限额每文件256 KiB、受管 JSON ≤128个、总量≤8 MiB；临时写入额外≤256 KiB，超过按最后写入时间淘汰，保留最后阅读位置；扫描最多1024目录项，异常则缓存失败降级。配置 endpoint.txt ≤2048字节；可选配置缺失或I/O不可读时使用固定允许的默认源，内容无效则拒绝连接。所有 Storage/缓存/配置 I/O、HTTP、解码与 JSON 校验都在工作线程；启动纹理由游戏 ContentManager 按原生资源流程加载。

进度保存最后一本书的 slug/revision/分片/滚动比例/字号，每5秒和导航/关闭时提交后台；PositionWriter 合并待写快照，避免旧请求覆盖新位置。不是云同步，不按账号关联，不修改世界存档。强制杀进程可能丢失未完成的异步检查点，原生移动操作中断也可能导致快照需重取或位置重置；仅缓存打开过的内容，不能凭目录离线读取未访问分片。

首次网络失败/超时后回退有效缓存，并进入本 Dialog 离线读取模式，后续缓存读取不再等待网络；「刷新」恢复联网尝试。没有缓存显示“尚未缓存”。明确401/403/404/409不恢复该请求旧缓存；409重新读取目录，revision不同不恢复旧滚动位置。损坏缓存删除后尝试网络或简短失败。离线公开快照无法远程撤回，这是离线阅读边界。

异步任务只产生数据，RequestGate 按代数发布；关闭、返回取消及新请求递增代数，迟到结果被丢弃。worker不捕获 Dialog、Widget、Player、Project。主线程 Update 取结果；UpdateCeases/Dispose/子系统 Dispose 取消会话。响应、流、HttpClient、handler 与 CTS 均释放。

## 实际命令与结果

| 命令 | 实际结果与范围 |
|---|---|
| Digital-Study `pnpm build` | shared、现有 web、API 构建通过；没有部署。web 构建包含保留的用户工作区改动，不纳入本次提交。 |
| `pnpm --filter @digital-study/api verify:game-library` | Nest真实HTTP + mock Prisma +临时文件：转换、分片/emoji、分页/参数/并发503、公开查询条件、旧内容回退限制、资源上限/来源优先、版本409、空/404、不支持格式、路径/符号链接/2MiB/无效UTF-8通过。非实际 MySQL/生产验收。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:\SCNET\SCNETx25.01.17` | 依赖、net472、API/版本标记、固定三哈希与原生 ABI 通过（构建脚本内实际调用）。 |
| `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir E:\SCNET\SCNETx25.01.17` | 最终Release 0错误/0警告；358项纯客户端隔离检查、4653项目标原生/IL检查与5条目包逐字节校验通过。具体最终指纹见下方。 |
| API HTTP夹具 → Mod协议解析 | 设置 `GAME_LIBRARY_FIXTURES=E:\Survivalcraft2\artifacts\celestial-api-fixtures` 运行 API检查，再执行 `tests\CelestialCodex.Tests\bin\Release\net472\CelestialCodex.Tests.exe artifacts\celestial-api-fixtures`；361项通过（358+3跨仓库契约）。 |
| `tests\CelestialCodex.TargetTests\bin\Release\net472\CelestialCodex.TargetTests.exe E:\SCNET\SCNETx25.01.17 E:\Survivalcraft2 --https-probe` | 4653项目标检查通过，316条外部成员引用全部解析；本机1个已装包另行扫描。实际HTTPS GET返回404；没有部署或写入生产。 |

纯客户端覆盖离线目录/正文/超时、未知格式/异常UTF8/JSON、严格来源、超限响应（包括未知Content-Length）、404/409/权限拒绝不复活旧缓存、进度重载/损坏、容量和文件数独立上限、临时写入残留、资源释放、取消、迟到响应/关闭、进度写入顺序、布局边界。测试用的注入 HTTP handler 仅存在测试入口，正式 Mod 总是创建受限默认 handler。

最终包两个副本均为 **21601字节 / 5条目**，SHA-256 **`781FE40EAC1CEB8EE854CBF2CB034773AEC0CB419306BC39CD304C55C3EBF32D`**。只确认桌面证据，不将规则检查数量解释为实机游戏验证。构建环境为 .NET SDK 9.0.314、Windows .NET Framework4.7.2引用，API侧Node22.22.3/pnpm11.25.0。

## Android/SCNET 验收矩阵

| 场景 | 期望行为 | 状态 |
|---|---|---|
| Windows固定目标的程序集/CSV/XDB/PNG/Release/包 | 可解析/构建，物品身份独立 | 已确认 |
| Windows实际HTTPS通道 | 验证证书、不重定向、当前端点404 | 已确认 |
| Android7723安装、加载、HTTP/TLS/BCL、Storage | 精确APK/程序集比对后可用 | 待验证 |
| Local单人 | 本机阅读、缓存/进度持久化，不改属性/地形 | 待验证 |
| Server房主 | 仅房主自己操作触发本机阅读，远端不弹窗，不保存远端阅读进度 | 待验证 |
| Client普通玩家 | 仅自己的本机阅读，可独立断网/恢复；服务器无需处理阅读请求 | 待验证 |
| 真实多分辨率/刘海/手势区/触屏拖动/字号/性能 | 按钮可达、正文可滚动、不与原生操作冲突 | 待验证 |
| 原生握手所有端同包同版本/资源摘要 | 原生校验，不绕过或改写握手 | 待验证 |
| 主机装Mod、客户端不装；不同版本混装 | 原生握手拒绝 | 不支持 |
| SCAPI、`.scmod`、HTTP来源、账号/云进度/AI问答、PDF/EPUB解析 | 不提供这些功能 | 不支持 |

7723可能重新打包，显示同版不代表DLL/BCL一致。必须取得精确APK或对应游戏/Engine/EntitySystem/BCL哈希，核对 ModLoader、Dialog、Block、Storage、System.Net.Http/Task/CTS/UTF8Encoding 成员后再提升 Android 状态。Windows运行成功不能替代此步骤。

## 安装与最短真机流程

1. 由运营者在测试/预发布环境提供上述HTTPS API，并用手机确认目录200；生产新路由当前未由本任务部署。来源不同需要单独审计并修改允许源，不能临时关闭TLS验证或随意跟随跳转。
2. 将**一份** `云天书典.netmod` 导入 Android SCNET `NetMods`，重启；核对 PackageName CelestialCodex /0.1.0。新建可丢弃创造单人世界，保留备份。
3. 从创造栏「云天书典」获取956/DATA0，持有朝天空使用；选一本原著和一本AI解读，核对AI标记；打开长正文、滑动、切换上下节、字号两端、返回、关闭。
4. 关闭并退出世界/重启游戏，点「继续」核对分片、字号、位置。先打开多个分片后启用飞行模式，验证已读可用、未缓存显示短反馈；恢复网络点刷新。模拟慢网，加载中立即关闭/返回/退出世界，观察无晚到UI访问或错误刷屏。
5. 在测试API用空库、PDF/EPUB-only、404、409、新revision、错误JSON、超长/错误编码响应逐项复测；备份后破坏本 Mod 缓存 JSON，确认可安全重取且不影响原生存档。不要损坏 UserId.dat/世界文件。
6. 横屏/竖屏可用设备、不同游戏UI缩放与刘海/手势区域检查按钮≥44逻辑单位、正文滚动、关闭不遮挡原生关键操作；记录分辨率、帧耗时、截图和日志。
7. 与 BuilderAssistant/SCIE-Port 同时安装，确认独立分类、物品/贴图/加载日志。房主与普通客户端安装**同一包**后入房，各自打开不同书；核对彼此界面/位置不联动、房主不替客户端请求、退出无错误；缺包/版本不同应由原生握手拒绝。分别记录 Local/Server/Client，不能只用一次入房代替全部验收。

## 修改文件清单

Digital-Study（`E:\Stduy`）：

- `apps/api/src/game-library/game-library.module.ts`
- `apps/api/src/game-library/game-library.controller.ts`
- `apps/api/src/game-library/game-library.service.ts`
- `apps/api/src/game-library/game-library.text.ts`
- `apps/api/src/scripts/verify-game-library.ts`
- `apps/api/src/app.module.ts`
- `apps/api/package.json`、`pnpm-lock.yaml`
- `docs/GAME-LIBRARY-V1.md`、`README.md`

Survivalcraft2：

- `src/CelestialCodex/CelestialCodex.csproj`、`modinfo.json`、`README.md`
- `src/CelestialCodex/Core/StrictJson.cs`、`Protocol.cs`、`BoundedCache.cs`（含ReadingPosition）、`PositionWriter.cs`、`ReaderLayout.cs`
- `src/CelestialCodex/Networking/LibraryClient.cs`、`RequestGate.cs`
- `src/CelestialCodex/Mod/CodexBlock.cs`、`StorageAdapter.cs`、`SubsystemCodexBehavior.cs`
- `src/CelestialCodex/UI/CodexDialog.cs`
- `src/CelestialCodex/Assets/Blocks/CelestialCodex.csv`
- `src/CelestialCodex/Assets/Database/CelestialCodex.xdb`
- `src/CelestialCodex/Assets/Textures/CelestialCodex/Book.png`（原创程序绘制，无外部Mod资产）
- `tests/CelestialCodex.Tests/CelestialCodex.Tests.csproj`、`Program.cs`
- `tests/CelestialCodex.TargetTests/CelestialCodex.TargetTests.csproj`、`Program.cs`
- `scripts/Build-CelestialCodex.ps1`、`New-CelestialCodexIcon.py`
- `docs/CELESTIAL-CODEX-V0.1.md`、`docs/COMPATIBILITY.md`、`docs/blueprint-block-reference.md`、`README.md`

生成的bin/obj、目标反编译材料、成员引用审计、API夹具、netmod均不入Git；没有原版DLL/PDB/内容包、APK、第三方工程源码或资产被打包。
