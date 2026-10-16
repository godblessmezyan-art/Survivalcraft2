# 测试记录

## 2026-10-16：Phase 1 Windows 编译前取证

- 目标：SCNET x25.01.17 Windows 发布目录。
- 路径：`E:\SCNET\SCNETx25.01.17`（仅本机，不提交）。
- `Survivalcraft.exe`：程序集版本 `2.4.0.0`，TargetFramework `.NETFramework,Version=v4.7.2`。
- 已确认类型：`Game.ModLoader`、`Game.ModEntity`、`Game.ModInfo`、全局 `ModsManager`。
- 已确认初始化方法：`Game.ModLoader.__ModInitialize()`。
- 已确认日志方法：`Engine.Log.Information(string)`。
- 已确认版本标记：Mod API `1.42`、SC `2.4.10.6`、产品标识 `Net_x25.01.17`。
- Android 公开 APK：未测试。
- Android 7723 包：未测试。
- 单人存档：未测试。
- 联机：未测试。

## 2026-10-16：Phase 1 Release 构建

- 命令：`./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release`
- 结果：成功，0 警告、0 错误。
- TargetFramework：`net472`。
- 输出：`artifacts/BuilderAssistant.netmod`。
- 包内容：`BuilderAssistant.dll`、`modinfo.json`。
- 当次包 SHA-256：`ED3417BAA17BD8371C5BF9BC717C4A25B83DEE0C111CA8724E68D8B8D8A92F7B`。
- Windows 游戏进程加载：未执行。
- Android/7723 实机加载：未执行。

后续每次实机测试追加日期、客户端来源、程序集哈希、设备/Android 版本、步骤、预期结果、实际结果和日志位置。

## 2026-10-16：Android/7723 最小加载验证

- 客户端：用户实际安装的 7723 渠道 SCNET `Net_x25.01.17`。
- 包：`BuilderAssistant.netmod` v0.1.0。
- 结果：用户报告验证通过，已成功安装并加载。
- 已确认范围：客户端能接受当前 `.netmod` 包结构、元数据和最小 `Game.ModLoader` 初始化入口。
- 未确认范围：渠道程序集哈希、触屏 UI、地形修改、存档写入、撤销、主机/客户端同步及其他 Hook。
- 证据等级：用户实机结果；尚未收集设备型号、Android 版本和完整日志。

## 2026-10-16：v0.2.0 快艇推进原型构建

- 目标：在不新增专有资源、不修改地形的前提下验证水上载具操控链路。
- 实现：骑乘原生船时读取原生 `PlayerInput.Move`，追加 `5 m/s²` 等效推进，并把水平速度限制为 `10 m/s`。
- 安全范围：只在 `Game.NetWork.WorkType.Local` 启用；远程客户端主动禁用。
- 命令：`./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release`
- 结果：成功，0 警告、0 错误。
- 包内容：`BuilderAssistant.dll`、`modinfo.json`。
- 当次包 SHA-256：`CED1D7DF36139B9FF87066A61E6763942DD50E6AC578536A49F831BE55F909C8`。
- Android/7723 运行：待验证。
- 联机运行：未测试，功能主动禁用。

## 2026-10-16：v0.3.0 独立快艇构建

- 新增：方块编号 `900`、创造栏 `Items/Speedboat`、`Speedboat` 实体模板、专用生成子系统、原创 DAE 占位模型和 PNG 占位贴图。
- 原版编号证据：x25.01.17 原版 `Block.Index` 最高为 `263`；`BlocksManager` 内容数组长度为 `1024`。
- 资源加载证据：`ModEntity` 仅注册 ZIP 中以 `Assets/` 开头的资源；`.csv` 和 `.xdb` 分别进入方块表与数据库合并流程。
- 安全：8 格附近最多 4 艘；生成和额外推进仅在 `WorkType.Local` 启用；普通 `Boat` 不再加速。
- 命令：`./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release`
- 编译：成功，0 警告、0 错误。
- 包内容：DLL、元数据、方块 CSV、实体 XDB、模型 DAE、贴图 PNG。
- 静态资源检查：CSV 为 `21/21` 列；24 个新 GUID 无原版碰撞或内部重复；DAE 为 13 个三角形、117/117 个索引字段且索引均未越界；PNG 为有效 32×32 文件。
- 包 SHA-256：`505CD1D545C72F183F7CF03410FB64365910DAB84050C234AAF0916C57BEF6AC`。
- Android/7723：待用户实机验证，不能标记兼容。
- 联机：未测试且主动禁用。

## 2026-10-16：v0.4.0 正式贴图、提速与联机开放

- 用户贴图：`1254×1254`、24 bpp PNG，源文件 SHA-256 为 `F7470FC5D5D3F417205C89880B78D0F0BF2FEB393C790EA65AE3D6895512584F`。
- 包内贴图：字节数 `2301068`，SHA-256 与源文件完全一致；占位 `.base64` 已移除。
- 速度：额外推进由 `5` 提高为 `9`，水平速度硬上限由 `10` 提高为 `18`。
- 联机：移除生成与推进的 `WorkType.Local` 限制；仍保留附近数量和速度上限。
- 命令：`./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release`
- 结果：成功，0 警告、0 错误。
- `.netmod` SHA-256：`037E5D49CC71F612059628CAD62544C370693E05B28A6C4895B4541D2479C4C9`。
- Android/7723 单机渲染与速度：待验证。
- 联机主机/普通客户端：待验证；不能仅凭编译结果确认服务器不会纠正客户端速度。
