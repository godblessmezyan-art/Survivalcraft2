# 云天书典0.1.1：Android使用闪退

日期：2026-11-04。修复基于本地main `a33ef46`，仅本地提交，不push；Digital-Study和生产环境未改动。

## 证据与原因

用户在7723 Android SCNET x25.01.17点击使用后立即退出，提供日志：

```text
19:15:27.502 ERROR: Method not found: bool System.Collections.Generic.List`1.Exists(System.Predicate`1<!0>)
  at CelestialCodex.UI.CodexDialog.Update () [0x0004c] in <74b90e06da8646c4bf65b617b31829cf>:0
  at Game.Widget.UpdateWidgetsHierarchy ... in <c690fa8547734ab8bc808e79264babbc>:0
```

原始0.1.0包SHA-256 `781FE40EAC1CEB8EE854CBF2CB034773AEC0CB419306BC39CD304C55C3EBF32D`，其中DLL MVID确为 `74b90e06da8646c4bf65b617b31829cf`，与日志一致。其唯一Exists调用位于书籍目录结果的续读检查；即使还没收到网络数据，Android运行时解析/执行UI更新路径时也可能先报接口缺失。不能把错误归因于API 404，也不能以Windows该方法存在为手机兼容证据。

手机游戏MVID `c690fa8547734ab8bc808e79264babbc`，固定Windows游戏MVID `49b1d4e25f4540d49d152db94c285f8c`。未取得Android APK/BCL文件与哈希，日志只证明这台手机缺少上述成员，不能声明其它集合方法也已证缺失。0.1.0在该日志环境的使用入口为 **不支持**。

## 修复

- `ChapterNavigation.IndexOf`采用Count/索引循环，替代续读Exists及章节导航FindIndex；首次/中间/末章、已删除章节、空列表路径均测试。
- 关闭的Dialog用反向RemoveAt清理，移除RemoveAll；缓存按一次读取的名称/大小/修改时间快照选最旧项，移除Sort(Comparison)与FindIndex。后两类属于避免继续依赖未经手机取证的同类便利接口，不声称它们在用户手机上必然缺失。
- `CodexDialog.Update`捕获内部更新异常，内部方法标记NoInlining，以保留调用者异常边界。失败取消后台会话、清除玩家引用、隐藏书典并记录完整异常，不能把这类托管异常传播到原生ScreensManager。不是移除阅读功能；正常更新仍执行同一阅读流程。
- 最终DLL扫描拒绝List.Exists及已替换的List委托搜索/排序方法。既有TryParse/LINQ/世界修改守卫继续保留。
- 使用测试程序集内故意含Exists的负对照，以及本机保留的真实0.1.0 DLL验证检查能拒绝故障引用。旧DLL只在忽略的artifacts下，不提交、不打包。开放仓库构建不要求保有旧DLL，始终运行源码负对照。

版本升级0.1.1/程序集0.1.1.0。PackageName/AssemblyName仍CelestialCodex；956/DATA0、新模板GUID、资源路径、net472/API1.42/SC2.4.10.6、API契约、缓存与进度格式、容量限额、原生握手保持。不修改BuilderAssistant/SCIE-Port，不新增网络包或服务器状态，不改玩家/地形。

## 验证

实际命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir E:\SCNET\SCNETx25.01.17
tests\CelestialCodex.Tests\bin\Release\net472\CelestialCodex.Tests.exe artifacts\celestial-api-fixtures
```

环境检查在构建脚本内调用根Test-Environment.ps1；继续固定原三程序集SHA，并执行Release、纯逻辑、真实目标CSV/XDB/PNG、冲突和IL检查、5项包内容逐字节验证。新增章节查找、确定性最旧缓存淘汰/保护进度；原生类型构造不完整的玩家触发托管异常，验证实际Update override不会向外抛出且释放玩家引用。该隔离夹具不等同Android完整UI运行或真实JIT模拟。

实际结果：环境与三固定哈希检查通过；Release **0错误、0警告**。纯客户端 **366项**通过，读取上一轮API真实HTTP夹具后 **369项**通过；本机带真实旧DLL负对照的目标检查 **5410项**通过，**313条**外部成员引用解析，已装1个包的ID审计及原生CSV/XDB/PNG通过。自编译负对照和MVID匹配的0.1.0 DLL均被Exists守卫按预期拒绝。

两个0.1.1包均 **21629字节 / 5条目**，SHA-256 **`007D4E38DAC6D758391C4530A153147223BDD7531EA9C0C61BB8F4DB3AFFDB56`**；包条目逐字节匹配本次Release及源码资源，不含原版游戏DLL。没有运行手机或完整游戏，不能以这些桌面/隔离结果宣称Android已兼容。

本次0.1.1 DLL MVID为 `6b2579c650a14acaa60a152e06ea64c8`，用于手机后续日志核对包身份。

## 替换与手机复测

安装 `artifacts/CelestialCodex/云天书典-0.1.1.netmod`，或同内容的 `云天书典.netmod`。先替换旧版，重启并核对Mod列表0.1.1；不要同时安装多个书典副本。缓存/进度目录未改，无需删除，更不要删除UserId.dat或世界文件。联机所有端统一0.1.1，旧版混装不支持。

1. 可丢弃单人创造世界拿到956，点击使用；即使API仍404，也应显示书典/短错误状态并可关闭，不应退出游戏。
2. 连续打开/关闭10次，加载中返回与退出世界。若另有渠道接口缺失，检查完整`[CelestialCodex] Reader update failed`日志；不能把短反馈视为阅读功能已通过。
3. API可用后打开一本多片书籍，测试首/中/末章导航、字号、关闭和重新进入继续；断网验证已读缓存，更新章节验证revision变化。
4. 分别验证房主/普通客户端、本机与远端界面不联动、触屏与安全区。

| 范围 | 状态 |
|---|---|
| 0.1.0在用户日志对应运行时使用 | 不支持 |
| 0.1.1固定Windows编译、IL故障引用移除、负对照、隔离与包检查 | 已确认 |
| 0.1.1 Android7723点击使用、实际阅读/缓存、Local/Server/Client、触屏 | 待验证 |

修复交付后仍需用户手机复测才能提高Android结论；没有以Windows结果代替真机证据。
