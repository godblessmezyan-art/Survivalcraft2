# 云天书典 · Celestial Codex（游戏内图书导入开发版，待 SCNET 实机验证）

新增本地 TXT/EPUB 书库与原 Digital-Study 云端书库切换；本地扫描目录为 app:/CelestialCodex/Imports（真实 Android 可访问路径待确认）。本地与云端阅读进度独立，原 API、物品956/DATA0 不变。详见 docs/CELESTIAL-CODEX-V0.2.md。本版本尚未取得 SCNET x25.01.17 目标编译及 Android 真机验收证据，不是已验证的 .netmod。

## 游戏内导入 TXT / EPUB（2026-11-09 源码更新）

在**本地书库**目录页点击「导入图书」，新弹窗优先通过**运行时反射探测** `Engine.Storage.ChooseFile` 启动系统文件选择器。若当前 SCNET 客户端没有可兼容的 `ChooseFile` 签名，显示明确反馈，并提供游戏内可访问的 `app:/` 虚拟目录浏览器；**该浏览器不能保证访问 Android 公共 Downloads 或任意手机文件**。

选中 TXT/EPUB 后复制到游戏私有的 `CelestialCodex/Imports`，8MiB 单文件上限，最多 64 本；同名文件采用 `(2)` 后缀，临时文件在失败时清理；完成后自动刷新原本地书库，无需玩家手动修改内部目录。原云端 Digital-Study 阅读不变。

技术限制：不同于新版 Survivalcraft API，**SCNET x25.01.17 的 Android 原生系统文件选择器签名尚未经目标游戏程序集确认**，因此系统选书按钮可能仅提示不可用；如果宿主提供的是 `content://` URI 而 `Engine.Storage.OpenFile` 不支持该地址，也会导入失败并显示错误。真正支持手机公共 Downloads 需要在本机 Android 实测并可能补充 SCNET 专属桥接。**当前源码尚未真实编译，也尚未与荒野探险家集成为一个安装包；不会单独发布云天书典 Release。**

## V0.1.3 及历史说明

0.1.3修复手机日志证实缺失的 `CreateLinkedTokenSource(CancellationToken[])`。请求改用自有取消源和后台轮询，保持12秒预算、关闭取消和有序释放；未禁用HTTPS验证。见 [0.1.3取消机制修复](../../docs/CELESTIAL-CODEX-CANCELLATION-0.1.3.md)。Android阅读仍待复测。

0.1.2显式请求未压缩响应，补充网络错误码及Game.log完整异常。公开API现已由Digital-Study项目上线，本机用实际Mod DLL读取目录、详情、正文均通过；Android仍显示连接失败的具体原因尚未确定，本版需手机复测取证。见 [网络诊断记录](../../docs/CELESTIAL-CODEX-NETWORK-0.1.2.md)。

0.1.1修复手机点击使用后因 `List<T>.Exists` 缺失而退出游戏的问题，并补齐界面异常隔离。0.1.0已在用户所示Android运行时证实闪退；0.1.1手机复测仍待验证。详见 [修复与验证记录](../../docs/CELESTIAL-CODEX-ANDROID-HOTFIX-0.1.1.md)。

独立客户端阅读工具，目标 SCNET `Net_x25.01.17` / API `1.42` / SC `2.4.10.6`，程序集 `net472`。Windows 目标 ABI、Release 与离线检查已确认；Android 7723 与实际 Local/Server/Client 游戏行为待验证。

创造栏「云天书典」内获取物品 **956 / DATA0**，持有并使用打开书库。选择书籍/章节，竖向滑动正文，`A− / A+` 调整字号，`章节 / 返回 / 关闭` 导航；重新进入后点「继续」。原生物品使用会受 SCNET 原有使用/领地规则约束，测试时可朝天空使用。

书典是非武器阅读工具，不是神器；依用户本轮“不修改玩家属性”要求，不提供神器移速或攻速加成。没有自定义网络包、服务器阅读状态、世界方块写入、属性/生命变更或世界存档结构变更。物品本身仍由原生库存/握手机制管理。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir <本机x25.01.17目录>
```

输出 `artifacts/CelestialCodex/云天书典.netmod` 与 `云天书典-0.1.3.netmod`。将同一份正式包导入各参与端 `NetMods`，替换旧版并重新启动游戏，在 Mod 列表核对0.1.3。不要同时安装旧版或两个副本；不是 `.scmod`，不携带原版 DLL。与 Builder Assistant、SCIE-Port 的构建、资源、版本和包完全分离。

默认公开来源为 `https://wxhappylife.top/digital-study/api/game-library/v1`。2026-11-04另一个项目完成API上线，本轮已验证公开目录、详情及正文200；此结果不代替Android游戏的HTTPS/运行时验收。手机失败时查看界面短错误码与Game.log的`[CelestialCodex] Network`及其后续异常。

阅读过的目录/分片自动缓存；未打开的内容没有预下载。首个网络失败可等待最多 12 秒后回退；进入离线状态后，本次 Dialog 后续读取直接用缓存，点「刷新」重试联网。主动关闭/返回取消请求。收到明确 401/403/404/409 时不使用该请求的旧缓存；章节更新须重载目录，跨 revision 不恢复旧滚动位置。

缓存与位置放在运行时 `ModsManager.UserDataPath` 的父目录下 `CelestialCodex/`。固定 Windows ABI 该字段为 `app:/UserId.dat`，因此路径为 `app:/CelestialCodex`；代码通过反射读取运行时字段、使用 Engine.Storage，不拼接桌面路径、不读取 UserId 内容。`endpoint.txt` 可选（≤2048 字节），只允许同一 HTTPS 来源 `wxhappylife.top:443` 的 `/digital-study/api/game-library/v1` 或 `/api/game-library/v1`，不能配置任意服务器。普通玩家无需编辑。

缓存每文件 ≤256 KiB、最多128个受管 JSON、总量≤8 MiB，写入临时文件再移动，超限按最后写入时间淘汰并保留最后阅读位置；暂存峰值额外≤256 KiB。目录异常、损坏/超限/非法 JSON、无权限或磁盘写入失败均可降级。已下载的公开文本离线快照不能远程撤回，恢复网络后按服务器结果处理。

详细实现、API、验证结果及 Android 单人/房主/普通客户端验收见 [交付与验收记录](../../docs/CELESTIAL-CODEX-V0.1.md)。
