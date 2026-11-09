# Builder Assistant 云端蓝图库 V1.0 / Mod 0.25.1

2026-11-09。只在 Builder Assistant 增量接入，书典网络客户端仅作只读参考，无跨工程源码/资源依赖，不改世界二进制存档。

## 0.25.1：云端刷新修复与诊断

用户反馈 0.25.0 点击云端「刷新」显示「蓝图库响应无效」。线上公开接口返回 HTTP 200、合法 `apiVersion=1` 空目录。旧 DLL 的请求路径校验有两处 `Int32.TryParse`，共用 JSON 字符串解码另有一处；项目已有 Android 缺失 System.TryParse 成员的历史证据，但本次尚无手机异常堆栈，不能据此断言用户故障的唯一根因。

0.25.1 将这三处改为有界 ASCII 十进制/十六进制逐位解码，保留规范路径、溢出拒绝和原 JSON 导入入口。网络错误区分 `[BCL]`、`[TLS]`、`[TIME]`、`[NET]`、`[DATA]`、`[ERR]`，游戏日志以 `[BuilderAssistant] Cloud` 记录 http/decode/cache 阶段与最多 8192 字符异常；日志失败不替换原错。只有网络中断、超时或 HTTP 5xx 可自动回退快照，包在 HttpRequestException 内的运行库异常不会伪装为断网成功。

本轮开发结果：Windows 固定目标环境检查通过；net472 Release 0 警告/0 错误；3292 项离线检查、2024 项目标检查通过。目标检查遍历实际 DLL 的云端/JSON/导入/家具/存储共 47 个类型及其异步状态机，未发现已知缺失 TryParse/Guid.Parse/LINQ/List 辅助接口；旧 0.25.0 DLL 负对照精确检出三处 TryParse 引用。此守卫只覆盖已知风险，不能证明所有 Android 运行库接口兼容。

通过实际 0.25.1 Mod DLL 的 List 方法直连生产 HTTPS，得到 total=0、非缓存结果；没有运行游戏进程或加载世界。没有已发布样本，因此本轮公网验证仅覆盖列表读取，未验证公网下载、手机刷新、触屏、施工、联机或重启离线完整流程。原有模拟网络、schema 1/2/3、SHA、版本隔离、材料/预览/权限检查继续通过。

开发检查可用 `BUILDER_CLOUD_LIVE=1` 启用真实 HTTPS 列表探针；`BUILDER_CLOUD_LEGACY_DLL` 可指定本地保留的旧 DLL 做负对照（均不提交二进制）。本地开发日志在 `artifacts/blueprint-cloud-0251-{environment,logic,target}.log`；完成开发检查后单独编译打包 `artifacts/BuilderAssistant-0.25.1.netmod`，未创建 GitHub Release，不运行发布路径的测试或安装包审计。手机需更新包并重启后再次点击「刷新」；如仍失败，记录错误短码和上述游戏日志，不能以 Windows 请求成功替代手机验收。

## 使用

903 蓝图工具的管理界面新增「本地 / 云端」、刷新、分页、下载并载入。切到云端先展示该页已有缓存，手动刷新请求已发布目录。下载成功经过材料校验后才替换当前蓝图，同时保存到原有本地蓝图库；保存失败有明确反馈。新导出仍为 schema 3，schema 1/2/3 继续导入。

同一 ID/revision 固定保存为 `cloud-<UUID>-r<revision>.json`，重复下载复用该文件，不无限生成同名副本；用户改过的同名文件不覆盖。新版另存为新 revision。主动点击原有「保存当前蓝图」仍是玩家创建命名副本的操作。

## 请求、缓存与线程

- 默认唯一服务：`https://wxhappylife.top/digital-study/api/game-blueprints/v1/blueprints`。另接受同域根路径 `/api/game-blueprints/v1/blueprints`，无任意域名/端口/路径配置。只生成 `?page=N&pageSize=20` 与 `/<UUID>/json?revision=N`；拒绝重定向、cookies 和压缩响应，不发送管理员密钥或 Token，不绕过证书校验。
- 公开 wire `apiVersion=1`，列表 `{page,pageSize,total,items}`。条目包括 UUID v4、正整数 revision、原始字节 SHA-256、schemaVersion/kind、名称、尺寸、展开总格数及 byteLength。必须已发布；2 MiB 网络上限，单次 12 秒截止时间覆盖响应体，UTF-8 严格解码，未知版本、重复字段和过深嵌套均拒绝。
- 原始蓝图经 `BlueprintImportService.FromString` 唯一解析入口，网络代码只做 JSON 安全守卫和元信息/哈希校验，不另写 cells/家具解析器。下载后在游戏主线程使用 `WorldEditRuntime.QuickValidate`/`QuickAdopt` 复核现有材料/状态，未通过不更新当前蓝图。
- 请求与结构解析在 Task 后台执行。结果只在 `BlueprintToolDialog.Update` 消费；关闭、世界销毁、换工具、切源或导入剪贴板会取消/丢弃旧请求，后台没有 UI、玩家或地形引用。
- 私有虚拟目录 `app:/BuilderAssistant/cloud-blueprints`，本地蓝图库为 `app:/BuilderAssistant/blueprints`。缓存只管理 `.cache`，最多 128 个完成文件、32 MiB；文件有界读取、同目录临时写入后原子替换。缓存失败不伪报已离线保存，原有蓝图库文件不受缓存回收影响。目标 `UserDataPath` 是 `app:/UserId.dat` 文件，先以 `Storage.GetDirectoryName` 取得父目录，再通过真实 ABI `Storage.GetSystemPath` 解析为当前平台物理目录；不把虚拟 URI 或该文件名直接交给 System.IO。旧代码拼出的 `app:/UserId.dat/BuilderAssistant/blueprints` 不是有效的游戏蓝图库路径；若曾由外部工具保存到其他位置，可保留原文件并用已有剪贴板导入，不自动移动未知路径。
- 离线缓存以明确的 ID/revision/SHA 绑定；网络断开、超时或 HTTP 5xx 可回退到同版本已验证快照。HTTP 401/403/404/409、重定向、无效 JSON、哈希或版本不符不会回退旧缓存。玩家已保存的本地副本是独立文件，网站下架不会远程删除这些副本。
- 网络客户端没有地形网关。原有转向、选点、预览、冲突确认、每批 32 格、每事务 10000 格/每轴 64、取消/撤销和 SCNET 权限入口沿用；远程普通客户端可浏览/下载，原有施工入口仍拒绝写入。
- 本地目录排序和家具模板生成原有的两个 `List.Sort` 调用改用有界归并排序，保持原来的 Ordinal 名称顺序及 Y/X/Z 方块顺序，避开项目已经记录的 Android BCL 缺失接口；不改变模板形状或材料规则。

## 配套服务

Digital-Study 的 `game-blueprints` 模块复用 MySQL/Prisma 和网站 `StudyAdminGuard`。后台 `index.html#blueprints` 支持选 `.json`、草稿、详情、下载、更新、发布/下架和删除；公开接口仅发布条目。详情见该仓库 `docs/GAME-BLUEPRINTS-V1.md`。内容更新递增 revision 并回草稿，需要重新发布；服务端原文持久化和数据库需同时备份。原 `game-library` API 独立保留。

## 开发验证与实机验收

实际开发检查（不创建 GitHub Release）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir E:\SCNET\SCNETx25.01.17
dotnet build src/BuilderAssistant/BuilderAssistant.csproj -c Release -p:SCNetGameDir=E:\SCNET\SCNETx25.01.17
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-BuilderAssistantStates.ps1 -GameDir E:\SCNET\SCNETx25.01.17
dotnet run --project tests/BuilderAssistant.Tests/BuilderAssistant.Tests.csproj -c Release
```

离线网络测试使用模拟 HttpMessageHandler；真实服务契约另由 Digital-Study 隔离 MySQL/Nest 响应文件交叉导入。0.25.1 新增实际 Mod DLL 的 Windows 公网列表探针，不能将其称为游戏或 Android 已联网测试。历史目标程序集证据只覆盖已登记哈希的 Windows 版本，不能自动套用于 7723 重新打包客户端。

| 场景 | 自动/开发覆盖 | 实机状态 |
|---|---|---|
| schema 1/2/3、SHA、revision、分页 | 离线检查 | 待验证 |
| 无效 JSON/未来版本、2 MiB、无长度响应、12 秒响应体超时 | 模拟 HTTP 检查 | 待验证 |
| 断网缓存、新对象重开缓存、同版本重复下载、本地改动保护 | 离线文件/网络检查 | 待验证 |
| 不支持材料、预览只读、原有权限/规划/施工/撤销 | 现有及新增离线检查 | 待验证 |
| 网站真实管理员上传 → 游戏刷新下载 → 预览施工 → 重启离线 | HTTP 子流程可隔离验证，完整流程尚未运行 | 待验证 |
| Android/7723 触屏、字体/缩放、Local/Server/Client 同步、桌面实际游戏 | 无真实设备/游戏进程测试 | 待验证 |

实机使用新建可丢弃世界，分别记录目标安装包指纹、角色和实际结果。下载失败、无效 JSON、旧 revision、不支持材料时观察当前蓝图保持；断网重启从本地列表加载；重复下载查看仅有一份同版本文件；Client 可以浏览，但不能进入施工。此版本不能标为 Android 或联机兼容已确认。

0.25.0 历史开发结果：Windows 已登记的 x25.01.17 三程序集哈希匹配，net472 Release 0 警告/0 错误，2018 项目标静态检查通过（含虚拟路径到物理路径解析），3258 项离线检查通过（包含真实 Nest/MySQL 响应交叉导入）。本地编译后用 `python artifacts/package-cloud-blueprints.py` 仅打包 BuilderAssistant DLL、modinfo 和原 Assets，产出 `artifacts/BuilderAssistant-0.25.0.netmod`（2412144 字节），未创建 GitHub Release，未运行游戏或 Android。安装包生成与开发测试为分离步骤，没有调用荒野探险家发布流程或其旧全量构建脚本。
