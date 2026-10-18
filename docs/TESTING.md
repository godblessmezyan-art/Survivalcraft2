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

## 2026-10-17：快艇调研、ABI 检查与驾驶员保护

- 调研固定提交：SCIE `4864d956793b6e670413019f1f6b1cc5c8bb0970`、ZanJhatCraft `756d849df4a8ecf0e6e0c57324b0e110aa99ebd8`、SurvivalcraftRuins `53425779dc499c6f08b94f4b973f329217f92765`、SurvivalcraftTemplateModForAPI `80a9e325ec8b68fa5d7632b36bed1fdf008f3690`。
- 新增 `scripts/Test-SpeedboatAbi.ps1`，检查 x25.01.17 的 Boat、Mount、Rider、Body、输入、生成和网络包公开签名；目标程序集三个已记录 SHA-256 均匹配。
- 代码改动：额外推进只对 `ComponentMount.Rider` 所指的首位 Rider 生效；没有实现或宣称双座完成。
- 命令：`./scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'`。
- 环境结果：成功；程序集版本 `2.4.0.0`、TargetFramework net472、API `1.42`、SC `2.4.10.6`、快艇 ABI 检查通过。
- 命令：`./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release`。
- Release 结果：成功，0 警告、0 错误。
- 包内容：`BuilderAssistant.dll`、`modinfo.json`、Speedboat CSV/XDB/DAE/PNG；未包含游戏程序集。
- `.netmod` SHA-256：`CACAC83621FF6D38C2222AB469BAC55B4CD0518F08B267E1DDE493E1538C312D`。
- Windows 游戏进程运行：未执行。
- Android/7723 快艇生成、渲染、乘坐和驾驶：未执行。
- 多人联机：未执行。

## 2026-10-17：v0.5.0 Builder Assistant 离线自动建造基础

本轮范围：仅 Builder Assistant，未改动 SCIE-Port。目标为可靠、可撤销的离线自动建造基础（架构 + 安全施工 + 中世纪小屋 + 可扩展生成器 + 验证）。

### 证据分级（严格区分）

1. 静态检查（源码/包内文件级）

   - 新增架构模块：`Core/CellPos`、`Core/Safety`、`Core/Blueprints`、`Core/Transactions`、`Core/BuildEngine`、`Features/Generators`、`Features/WorldEdit`；均为纯 C#，`Core`/`Generators`/`WorldEdit` 逻辑类为 `internal`，仅被游戏反射发现的方块与行为类为 `public`。
   - 小屋蓝图：`9×10×11`（宽×高×长），470 个方块，仅使用已核验的原版普通立方材质（Cobblestone/StoneBrick/Planks/Bricks/Glass），不引入台阶/门/梯等需朝向数据位的方块；确认为有内部空腔与屋顶的真实结构，非空心盒子。
   - 编号：建造工具 `901`、撤销工具 `902`，位于原版 `0–263` 之外，与快艇 `900` 互不重叠。
   - `Building.xdb` 复用原版 `BlockBehaviors`（`00c97f0f…`）与 `Project`（`85023bf8…`）GUID，四个新子系统/成员模板使用新生成的唯一 GUID；打包脚本对“原版同名文件夹必须使用原版 GUID”做强制断言，规避 SCIE-Port 曾遇到的合并异常。
   - CSV 类名唯一性：`BuilderToolBlock, SpeedboatBlock, UndoToolBlock`（打包脚本报出，无重复）。

2. Windows 侧验证（本机，真实程序集）

   - 命令：`./scripts/Test-Environment.ps1 -GameDir 'D:\SCNET'` —— 通过：程序集版本 `2.4.0.0`、TargetFramework net472、API `1.42`、SC `2.4.10.6`，三程序集 SHA-256 与记录一致，快艇 ABI 检查通过。
   - 命令：`./scripts/Build-NetMod.ps1 -GameDir 'D:\SCNET' -Configuration Release` —— 该脚本现会先跑 `Test-Environment.ps1` 与 `Test-Logic.ps1`，再 Release 编译、打包并做上述冲突/ GUID 断言。
   - Release 编译：`0 错误、0 警告`。编译期即完成对 `SubsystemTerrain.ChangeCell`、`Terrain.GetCellValue/IsCellValid/GetChunkAtCell/MakeBlockValue/ExtractContents`、`BlocksManager.FindBlockByTypeName`、`PaintedCubeBlock(int)`、`SubsystemBlockBehavior.OnUse(Engine.Ray3,ComponentMiner)`、`ComponentMiner.Raycast<TerrainRaycastResult>`、`Game.NetWork.CommonLib.WorkType`、`ComponentGui.DisplaySmallMessage` 的真实签名绑定；任何签名不符都会编译失败。
   - 离线逻辑测试：`tests/BuilderAssistant.Tests`（net8 控制台，仅编译 Core+Generators 源码，不引用游戏程序集），`38/38` 断言通过，覆盖：蓝图重建确定性、尺寸与内部空腔、材料键可解析、越界 Y 拒绝、默认不覆盖非空气、显式覆盖开关、数量上限(1024)拒绝、单轴上限(64)拒绝、未知材料拒绝、分批执行完成、撤销不覆盖玩家后续改动、执行中区块卸载触发失败并回滚、取消并回滚、旋转四步复原、镜像对合。
   - 包内容：`BuilderAssistant.dll`、`modinfo.json`、`Assets/Blocks/{Speedboat,BuilderTools}.csv`、`Assets/Database/{Speedboat,Building}.xdb`、`Assets/Models/Speedboat.dae`、`Assets/Textures/Speedboat.png`；不含任何专有游戏程序集。
   - 本次 `.netmod`（v0.5.0）SHA-256：`9EF20A19C779DC2FF8C1CAAAFF08442E710AA34C0D63E341B053C8F295FA8B19`。

3. Android 实机验证（待用户在单人创造测试世界执行）

   - 尚不能宣称建造/撤销在手机上工作；以下需在 7723 Android 客户端实测：安装 `.netmod` 后能否进世界、建造/撤销工具是否在创造栏出现、落点选取与二次确认手感、分批写入可见性（32/帧）、整体撤销、存档并再次进入后世界是否正常、以及快艇功能是否不受影响。

### 复现实验

- 打包（本机实际游戏目录为 `D:\SCNET`，注意不是文档里旧写的 `E:\SCNET\...`）：

  ```powershell
  ./scripts/Build-NetMod.ps1 -GameDir 'D:\SCNET' -Configuration Release
  ```

- 仅跑离线逻辑测试：

  ```powershell
  ./scripts/Test-Logic.ps1
  # 或： dotnet run --project tests/BuilderAssistant.Tests -c Release
  ```

## 2026-10-18：v0.6.0 M1 修复「目标区块未加载或越界」（Builder Assistant）

范围：仅 `src/BuilderAssistant/` 与 `tests/BuilderAssistant.Tests/`，分支 `feat/ai-builder-foundation`（自 main `0badabb` 自动建造小屋提交）。未改动 SCIE-Port，未改动快艇功能，未删除任何安全检查，未在写前全量校验上做任何放宽。

### 1. 环境修复（M0 阻塞）

- 现象：`global.json` 钉住 SDK `9.0.300`，`rollForward: latestFeature` 无法跨大版本回退，机器只有 net8 运行库 → `dotnet` 直接解析失败。
- 处理：用 `dotnet-install.ps1` 以用户作用域安装 SDK `9.0.300` 到 `%LOCALAPPDATA%\Microsoft\dotnet`，调用该目录下 `dotnet.exe` 并设 `DOTNET_ROOT`；离线测试工程为 `net8.0`，而该根下只有 net9 运行时，故额外设 `DOTNET_ROLL_FORWARD=Major` 才能跑起测试应用。
- 注意：机器级（PATH 上的）`dotnet` 仍缺 9.0.300，属本机现状，不代表 CI/其他机器。
- 实际调用（git-bash）：

  ```bash
  export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet"
  export DOTNET_ROLL_FORWARD=Major
  "$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release \
    --project tests/BuilderAssistant.Tests
  ```

### 2. 离线逻辑测试（真实执行，本机）

- 命令：`"$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release --project tests/BuilderAssistant.Tests`（工程仅编译 `Core` + `Features/Generators` 源码，不引用游戏程序集）。
- 结果：`72/72 CHECKS PASSED`（在原 v0.5 的 38 项基础上新增 M1 相关断言）。
- M1 新增测试方法（6 个，注册于 `Program.Main`）：
  - `TestCrossChunkNotReadyClassifiedAndRecoverable`：跨区块、部分格区块未就绪时，逐格分类正确且判定为“全部可恢复”。
  - `TestOutOfBoundsIsHardNotRecoverable`：越出有效域的落点判为硬性不可恢复（有限等待救不了）。
  - `TestWorldHeightYBoundary`：Y=255 在界内、Y=256 判高度越界且非“等待可解”。
  - `TestPartialRollbackIsTrackedAndRetried`：回滚遇个别不可编辑格时登记延迟条目、如实报告未完成，区块恢复后 `RetryRollback` 补齐并转为干净完成。
  - `TestPartialUndoRetainedAndRetried`：部分撤销后事务保留在栈上，再次撤销续做，全部恢复才出栈。
  - `TestUndoPendingYieldsToLaterPlayerEdit`：延迟撤销的格子若被玩家后续改动则不再重试、绝不覆盖。

### 3. net472 游戏侧编译（本机阻塞 — 待验证）

- 本机不存在 SCNET x25.01.17 引用程序集，`dotnet build -c Release` 在引用校验步骤即失败：

  ```
  error : Missing SCNET x25.01.17 reference: E:\SCNET\SCNETx25.01.17\Survivalcraft.exe.
          Pass -p:SCNetGameDir=... or set SCNET_GAME_DIR.
  ```

- 因此 `Features/WorldEdit/GameTerrainGateway.InspectCell` 与 `WorldEditRuntime` 预检/延迟回滚两处改动为“源码完成、本机未编译”，其调用的游戏 ABI 成员均为 v0.5.0 已编译绑定过的（`GetChunkAtCell`/`TerrainChunk.State==Valid`/`IsLoaded`/`IsCellValid`/`ChangeCell`），未引入新签名。
- 复现（在装有 x25.01.17 的机器上指向真实游戏目录）：`./scripts/Build-NetMod.ps1 -GameDir '<游戏目录>' -Configuration Release`。通过后方可把 Features 层与 `.netmod` 打包标记升级为“已确认（编译期 ABI）”。

### 4. Android / 7723 实机（待验证）

- 本轮未在 Android 实机执行任何验证，**不能宣称该修复在手机上生效**。待实测：安装后能否进世界、跨区块小屋是否会触发有限等待并最终开工、延迟回滚/撤销的重试提示是否符合预期、撤销是否不覆盖玩家后续改动、存档重进是否正常、快艇是否不受影响。

### 5. 复现实验（本机当前可跑的部分）

```bash
# 离线逻辑（本机可跑，无需游戏程序集）
export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet"; export DOTNET_ROLL_FORWARD=Major
"$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release --project tests/BuilderAssistant.Tests

# net472 打包（需游戏程序集，本机当前缺失）
# ./scripts/Build-NetMod.ps1 -GameDir '<游戏目录>' -Configuration Release
```
