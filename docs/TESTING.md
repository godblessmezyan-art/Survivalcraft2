# 测试记录

## 2026-10-22：v0.16.0 方块能力审计与第一批扩展

- 取证命令：`scripts/Audit-BuilderAssistantBlocks.ps1 -GameDir 'D:\tmp\builderassistant-block-audit-x25.01.17\game' -OutputPath 'docs/blueprint-block-audit-x25.01.17.md'`。
- 取证结果：目标哈希门通过；从实际程序集反射得到 258 个具体 `Block` 类型，从同包 `Content.scpak/Assets/BlocksData.txt` 得到 165 个 `IsPlaceable=TRUE` 项；生成完整 165 行能力表，能力目录启用 60 种。
- 离线测试：`scripts/Test-Logic.ps1`，`ALL 492 CHECKS PASSED`。新增覆盖具体 key/逻辑 key 的 `f` 族冲突、`materials[].block` 冲突、各族非法 DATA、彩色状态和横向原木拒绝、60 项目录完整性及容器/流体仍排除。
- ABI 检查：`scripts/Test-Environment.ps1 -GameDir 'D:\tmp\builderassistant-block-audit-x25.01.17\game'`，程序集哈希、net472、API/SC 版本和既有 ABI 守卫通过。
- Release 编译：`dotnet build src/BuilderAssistant/BuilderAssistant.csproj -c Release -p:SCNetGameDir='D:\tmp\builderassistant-block-audit-x25.01.17\game'`，0 警告、0 错误。
- `.netmod` 全流程打包：`scripts/Build-NetMod.ps1 -GameDir 'D:\tmp\builderassistant-block-audit-x25.01.17\game' -Configuration Release` 全部通过；`artifacts/BuilderAssistant.netmod` 为 2,382,028 字节，SHA-256 `55340E2E03711DF1F47CF38B72A36F5AF39DCEA6D167F5BBE1EE282D29D31791`。包内 8 个预期条目，`modinfo.json`/DLL 版本为 0.16.0/0.16.0.0，不含游戏程序集。
- Android/7723：本轮未在设备执行。60 种方块的渲染、实际放置/朝向、复制往返、存读档、撤销与主机/客户端同步全部为**待验证**；不得用 JSON 解析、桌面离线测试或编译成功替代这些结论。

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

### 3. net472 游戏侧编译 + `.netmod` 打包（本机成功 — Features 层升级为已确认）

用户提供官方 Windows x25.01.17 客户端目录 `D:\SC`。先核对引用程序集哈希（与既有登记的公开 Windows 发布完全一致）：

```bash
cd /d/SC && sha256sum Survivalcraft.exe Engine.dll EntitySystem.dll
# Survivalcraft.exe 3d14b8d6…cc74a7ad（= 记录值）
# Engine.dll       98f2035b…e770694（= 记录值）
# EntitySystem.dll 7a87e643…b67ed19c（= 记录值）
```

- 直接编译 Mod 工程（引用真实程序集）：

  ```bash
  export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet"; export DOTNET_ROLL_FORWARD=Major
  "$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" build -c Release -p:SCNetGameDir='D:\SC'
  ```

- 结果：`已成功生成，0 个警告，0 个错误`，产出 `BuilderAssistant.dll`（net472）。这确认了 `GameTerrainGateway.InspectCell` 与 `WorldEditRuntime` 预检/延迟回滚用到的游戏侧成员（`GetChunkAtCell`、`TerrainChunk.State==Valid`、`IsLoaded`、`IsCellValid`、`ChangeCell`）签名全部正确。Features 层由此从“待验证”升级为**已确认（编译期 ABI）**。
- 官方脚本全流程（环境校验 + 逻辑测试 + Release 编译 + 打包 + 内容守卫）：

  ```bash
  powershell.exe -NoProfile -ExecutionPolicy Bypass \
    -Command "& ./scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release"
  ```

- 结果：`Test-Environment` 通过（哈希匹配、assembly 2.4.0.0、net472、API 1.42、SC 2.4.10.6、快艇 ABI 检查）、`72/72` 逻辑测试、`0 错误 0 警告` 编译、打包通过全部守卫（必需条目、CSV 类名唯一 `BuilderToolBlock/SpeedboatBlock/UndoToolBlock`、经 `Test-XdbMergeSafety` 校验的原版 GUID/跨文件同名/父级可解析性断言、不含专有程序集）。
- `.netmod`（v0.6.0，重新打包）SHA-256：`E454EC15254845DD816358DA3C3BF287CC0F443E7C3C6634D157185F408CCA67`。
- 边界：**Windows 编译+打包 ≠ 运行验证**。Windows 游戏进程内加载与 Android 实机仍各自待验证（见第 4 节）。

### 4. Android / 7723 实机（待验证）

- 本轮未在 Android 实机执行任何验证，**不能宣称该修复在手机上生效**。待实测：安装后能否进世界、跨区块小屋是否会触发有限等待并最终开工、延迟回滚/撤销的重试提示是否符合预期、撤销是否不覆盖玩家后续改动、存档重进是否正常、快艇是否不受影响。

### 5. 复现实验（本机当前可跑的部分）

```bash
# 离线逻辑（本机可跑，无需游戏程序集）
export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet"; export DOTNET_ROLL_FORWARD=Major
"$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release --project tests/BuilderAssistant.Tests

# net472 打包（本机游戏目录为 D:\SC，已验证可用）
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ./scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release"
```

### 6. XDB 合并安全：守卫回归 + 对真实库交叉验证（本机 Windows，成功）

本轮针对“是否复现历史 `already nested` 合并失败”做了专项取证。本机可直接复现两条：

- 守卫回归测试（喂入构造样本，证明规则会咬、且不误报合法的跨文件原版文件夹/Project 重复）：

  ```bash
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-XdbGuards.ps1
  ```

  结果：`CHECKS: 7 passed, 0 failed` → `XDB GUARD TESTS PASSED`。其中第 1 项对**真实随包** `Building.xdb`/`Speedboat.xdb` 判定为 0 冲突；新增的第 5b 项专门验证“跨文件重复 `<Parameter>` GUID”会被抓（即下面 v0.6.1 修的那类）。

- 用游戏自身解密器把 `Content.scpak` 解成明文 ZIP、取出真实 `Assets/Database.xml`，再逐个核对 Builder Assistant 依赖的 GUID 是否存在于真实库（不采信记录值）：

  ```powershell
  # 关键 API：Game.SurvivalCraftModEntity.GetDecipherStream(System.IO.Stream) -> MemoryStream（静态）
  # 解密后 head 50 4b 03 04（标准 ZIP），取 Assets/Database.xml（463,610 字节）
  ```

  结果：12 个原版 GUID（`Entities 5fcdf35f…`、`BlockBehaviors 00c97f0f…`、`Project 85023bf8…`、八个组件/子系统基类含 `fefb9590…`）在真实库中**存在且元素名匹配**；7 个自定义结构 GUID 在真实库中**不存在**（纯增量、不覆盖原版）。

- 真实运行日志佐证：`D:\SC\Bugs\Game.log` 一次完整启动+进世界（`WorldName=Bysnow`）全程无异常、无 `already nested`、无 `Database not loaded`；该次 `D:\SC\NetMods` 为空，即 Builder Assistant 未装于此环境，本机当前无可复现的 v0.6.0 运行期报错。历史首异常属 SCIE-Port 0.3.1 的游离 `Entities` 文件夹（0.3.2 已修）。

**边界**：以上为“桌面数据库结构 + 真实库交叉验证”层面的确认，**不等于** Windows 进程内加载、也不等于 Android 实机运行。若真机复现，需回传读条阶段第一条 `Warning(...)` 原文 + 完整日志再据实定位；不臆造根因、不随机替换 GUID。

### 7. v0.6.1：真机日志定位到“Class 参数 GUID 重复”并修复（本机复核，成功；待真机复测）

用户回传真机 `Game.log`，首异常为 `[Builder Assistant] 加载.xdb` 之后 `WARNING: An item with the same key has already been added. Key: ea25b78e-7d8c-4998-85d3-2b1baad3ba34`，连锁 `Database not loaded.` 与 `EntityTemplate "MalePlayer" not found.`（进世界栈：`DatabaseManager.get_GameDatabase ← GameManager.LoadProject`）。交叉验证真实 `Database.xml` 确认：原版每个 `<Parameter>` 实例 GUID 各不相同、`ea25b78e` 在原版出现 0 次——它是 Builder Assistant 在 Building.xdb 里**同时贴给 BuilderTool 与 UndoTool 两个 Class 参数**（Speedboat 又用一次）的复制粘贴键，加载器按 `Guid=` 建字典即冲突。

修复：只把 Building.xdb 两个 Class 参数改为新的唯一 GUID（`ecf3ac44…`、`82d5d8fd…`，经核不在原版、彼此唯一），`ea25b78e` 全包仅留 Speedboat.xdb 一处；**Speedboat.xdb 一字未动**。版本升到 0.6.1，加载横幅改为按程序集版本动态打印（此前写死 `v0.5.0`，也正说明手机装的是旧包）。

本机复测：`Test-XdbGuards` 7/7（含新增“跨文件重复 `<Parameter>` GUID 被抓”）；重新 Release 编译 0 错 0 警并重打包，守卫全过；**核对随包产物内容**——`Building.xdb` 无重复 GUID、`ea25b78e` 计数 0、`Speedboat.xdb` 仅 1、`modinfo Version=0.6.1`。`.netmod`（v0.6.1）SHA-256：`C4B022D3566E4066AFB2EA15ED541D8AC68B4829903D593F207C3B3E3FF60C30`（取代此前 v0.6.0 的 `E454EC15…`）。

**待办**：把 v0.6.1 包装上 7723 真机，确认加载横幅显示 v0.6.1、不再出现该 WARNING / `Database not loaded`，且进世界、建造、撤销、快艇均正常。仍不能仅凭桌面结论宣称手机已修复。

### 8. v0.6.2：真机日志定位到“区块就绪误判（`IsLoaded` 被当成可写性）”并修复（本机复核，成功；待真机复测）

用户装 v0.6.1 后回传真机日志：数据库崩溃已消失、能跑到建造逻辑，但报 `区块在等待后仍未全部就绪，已停止(未改动世界) … 470 格中有 470 格不可编辑 … 示例:ChunkNotLoaded @(833,66,-28) chunk(-2147483648,-2147483648) state=9 loaded=False … 结论:均为区块尚未就绪…建议稍后重试而非改坐标`。

**先排除假线索（反射 x25.01.17 程序集取证，非猜测）**：
- `chunk(-2147483648,-2147483648)` = `CellEditability.UnknownCoord`（`int.MinValue`）占位，不是坐标换算崩了；
- `state=9` = `TerrainChunkState.Valid`（反射枚举 0..9，Valid 末位），即区块内容其实**已生成就绪**；
- `GetChunkAtCell(x,z)`/`ChangeCell`/`IsCellValid` 同用 cell 坐标，无块↔原子错配；
- 落点来自 `RaycastMode.Interaction` 近身射线命中，是玩家眼前正在渲染的地，且 `IsCellValid` 已通过。

**真因**：`GameTerrainGateway.InspectCell` 把“可编辑”硬性要求 `chunk.IsLoaded==true`。`TerrainChunk.IsLoaded` 是磁盘/流送态标志，对玩家站立的 Valid 新生成区块仍为 `false`，于是眼前可建区被误判“未加载”，等待 150 帧后放弃（保守中止本身对，但中止是误判）。此判据自 v0.5.0 起从未真机验证。

**修复（最小）**：判据改为引擎就绪信号 `chunk.State==Valid && chunk.Cells!=null`，`IsLoaded` 只记日志不再把关；`ChunkNotLoaded`→`ChunkContentsNotReady`、中文标签“区块未加载”→“区块内容未就绪”（`CellEditability`/`BuildPlanner`/测试双件同改）；诊断回填真实区块坐标（`chunk.Coords.X/.Y`）并如实改写“结论”文案。

**本机复测结果**：`net472` 对真实程序集 Release 编译 `0 错误 0 警告`（编译期确认 `Cells`/`Coords`/`State` 签名可用）；`BuilderAssistant.Tests` 离线逻辑 `ALL 72 CHECKS PASSED`（`TestCrossChunkNotReadyClassifiedAndRecoverable` 已断言“区块内容未就绪 vs 区块缺失”分类且均可恢复、不写入）；`Test-XdbGuards` `CHECKS: 7 passed, 0 failed`；随包内容核对 `modinfo Version=0.6.2`、Building.xdb `ea25b78e`=0（GUID 修复未回退）、Speedboat.xdb 仅 1。`.netmod`（v0.6.2）SHA-256：`404D1798666B28ED85B611AD635760F7EAE497B3D07CFEA071F6B6E60EA11D61`（取代 v0.6.1 的 `C4B022D3…`）。

**待办（关键）**：把 v0.6.2 装上 7723 单人创造测试世界，对准**脚下已渲染的地面**建造应能成功；若仍失败，请回传新诊断行（重点看 `state=` 值与“区块内容未就绪/区块状态未就绪/区块缺失”各自计数）与完整日志。桌面+编译+离线全绿仍≠手机已修复，Android 能否真正落建筑材**待验证**。

## 2026-10-18：v0.7.0 通用 JSON 蓝图导入/导出 + 复制/粘贴（AI 建筑接口的桥）

范围：仅 `src/BuilderAssistant/`（新增 Core/Json、Core/Blueprints/BlueprintJson、Features/WorldEdit/BlueprintCopier、Infrastructure/BlueprintStorage；改写 VanillaBlockPalette、GameTerrainGateway、WorldEditRuntime）与 `tests/BuilderAssistant.Tests/`。未改 SCIE-Port、未改快艇数据、未触及联机、未绕过任何区块校验（第 11 步）。目标：先确保蓝图能被可靠保存、重导入并复现同一建筑，再谈接 AI。

### 1. 新增离线逻辑测试（真实执行，本机）

- 命令：`"$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release --project tests/BuilderAssistant.Tests`（工程新增编译 `Core/Json/Json.cs` 与 `Core/Blueprints/BlueprintJson.cs`，仍不引用游戏程序集）。
- 结果：`ALL 133 CHECKS PASSED`（在原 95 项基础上新增 14 个 JSON/蓝图用例）。
- 新增用例覆盖：字节稳定往返复现同 (坐标,材料) 集、负相对坐标归一化后复现、重复坐标拒、小数坐标拒、schemaVersion=999 拒、缺 schemaVersion 拒、>1024 拒、单轴 65 拒、悬空材料 id 拒、空 cells 拒、per-cell `d`（朝向/数据预留位）拒、非空 `buildOps` 拒、声明 size≠重算包围盒拒、四分之一转向与 JSON 往返一致性。

### 2. XDB 合并安全守卫回归（本机）

- 命令：`powershell -File scripts/Test-XdbGuards.ps1` → `CHECKS: 7 passed, 0 failed`（Building.xdb/Speedboat.xdb 判定 0 冲突；v0.6.1 参数 GUID 修复未回退）。

### 3. net472 游戏侧编译（本机，引用 D:\SC 真实程序集）

- 命令：`dotnet build -c Release -p:SCNetGameDir='D:\SC'` → `0 错误 0 警告`。编译期确认 `ModsManager.UserDataPath`（public static string）、`Terrain.ExtractContents`、`BlocksManager.FindBlockByTypeName`、`Block.GetType().Name`、`chunk.Coords/State/Cells` 等复制/存储所用签名可用。
- 过程中修掉两处编译错：CS1570（BlueprintStorage doc 注释里未转义的 `<`）与 CS0051（`SetMode` 取私有 `RuntimeMode` 可访问性不一致，已将 enum 提为 internal、方法降为 internal）。

### 4. 全流程打包（本机，GameDir=D:\SC）

- 命令：`powershell -File scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release`（内含 Test-Environment + Test-Logic(133/133) + Release 编译 + 打包 + 内容守卫）。
- 结果：通过全部守卫（必需条目、CSV 类名唯一 `BuilderToolBlock/SpeedboatBlock/UndoToolBlock`、`Test-XdbMergeSafety` 全局 Guid 唯一性、不含专有程序集）。
- 随包核对：`modinfo Version=0.7.0`、DLL 程序集版本 0.7.0.0、Building.xdb `ea25b78e`=0、Speedboat.xdb 仅 1。
- `.netmod`（v0.7.0）SHA-256：`4B227BB59C731D02BC1724473A69ABFD1B5A5D6E440C8C761C1487C1F1F51064`（取代 v0.6.3 的 `D88B1E1A…`）。

### 5. Android / 7723 实机（待验证）

本轮未在任何设备执行验证。桌面离线全绿 + 编译期 ABI ≠ 手机可用。待实测：①复制模式两角框选一段立方方块建筑，应报“已捕获 N 个…/已存入蓝图库：copy_….json”，切 Paste 后两次点击原样重建；②撤销整体回退粘贴体且不覆盖玩家手改；③选区含台阶/植物/水时如实列为“暂不支持”并跳过、不硬凑；④导入先前保存的 `.json` 再粘贴；⑤`ModsManager.UserDataPath` 下蓝图库读写在真机是否可落地（此前从未在设备上跑过一次文件 IO）。若失败请回传首条 `Warning/Error` 原文 + 完整 `Game.log`。

### 6. 复现实验（本机当前可跑）

```bash
# 离线逻辑（无需游戏程序集）
export DOTNET_ROOT="$HOME/AppData/Local/Microsoft/dotnet"; export DOTNET_ROLL_FORWARD=Major
"$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release --project tests/BuilderAssistant.Tests

# 守卫
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Test-XdbGuards.ps1

# net472 打包（游戏目录 D:\SC）
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ./scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release"
```

## 2026-10-18：v0.8.0 蓝图工具（外部 AI → 导入 → 落成 闭环）

范围：仅 `src/BuilderAssistant/`（新增 `Features/WorldEdit/BlueprintToolBlock.cs`(903) 与 `SubsystemBlueprintToolBlockBehavior.cs`、纯 Core `Core/Blueprints/BlueprintLibraryBrowser.cs`；升级 `WorldEditRuntime` 加 `OnBlueprintTool`+`CycleImportLibrary` 及浏览游标状态并在 `ResetSession` 清空；`Assets/Blocks/BuilderTools.csv` 新增一行；`Assets/Database/Building.xdb` 新增行为 `SubsystemTemplate` + `Class` 参数 + `Project` 成员，三者用全新且彼此唯一的 GUID）与 `tests/BuilderAssistant.Tests/`。未改 SCIE-Port、未改快艇数据、未触及联机；蓝图工具只读文件+校验、**从不写地形**；载入的蓝图仍经与小屋**完全相同**的 `PlanAndStart`→`BuildPlanner`→`BuildTask`→`TransactionHistory` 主链落成，安全规则（单人门禁、仅主线程、默认不覆盖+显式确认、写前全量校验、有界区块等待、clear-soft、延迟回滚/撤销）一字不改。目标：补上游戏内触屏导入入口，闭环“外部 AI 生成 `.json` → 系统文件管理器投递 → 蓝图工具浏览/载入/校验 → 建造工具两次落成”。

### 1. 新增离线逻辑测试（真实执行，本机）

- 命令：`"$HOME/AppData/Local/Microsoft/dotnet/dotnet.exe" run -c Release --project tests/BuilderAssistant.Tests`（测试工程新增编译 `Core/Blueprints/BlueprintLibraryBrowser.cs`，仍不引用游戏程序集）。
- 结果：`ALL 147 CHECKS PASSED`（在原 133 项基础上新增 14 项，全部针对纯游标 `BlueprintLibraryBrowser.Advance`/`PositionLabel`）。
- 新增用例覆盖：空库 `count<=0` 返回 -1、负数按空处理、游标 -1 首点选 0、单调前进 0→1→2、到末尾回绕 2→0、单条目恒为 0、两条目连点六次严格交替 `0,1,0,1,0,1`；`[i/N]` 标签：首条 `[1/4]`、末条 `[4/4]`、单条 `[1/1]`，空库 / 预选游标 -1 / 越界游标一律返回空串（防御）。不依赖文件 IO 或游戏程序集。

### 2. Building.xdb 独立校验（临时 .ps1，本机）

- 用 `XmlDocument.Load` 确认新 `Building.xdb` **XML 合法**；正则抽取全部 `Guid="…"`（11 个）逐一小写去重——`NO_DUPLICATE_GUIDS_IN_FILE`；并确认三个新 GUID 均在位：子系统模板 `c1f4a9d6-8b7a-4f11-9c3d-5a1b2e77f4c9`、Class 参数 `9d6b253e-47f8-4f1d-a29b-47e51cf0c6d8`、Project 成员 `6a7b1b7f-4143-459e-83ac-1c22f6d9b0e1`。
- **环境坑（复现既有铁律）**：此前试图用 `powershell -Command '...'`（经 bash）内联跑校验，正则里的反斜杠转义双引号在 bash 单引号包裹下把 PowerShell 解析打崩（`MissingEndParenthesisInMethodCall` 等乱码报错，exit 1）。改把校验写进临时 `.ps1` 用 `-File` 运行即稳定通过——与记忆里“PS 命令经 bash 须整体单引号、复杂内容落 `.ps1` 文件”一致。

### 3. XDB 合并安全守卫回归（本机）

- 命令：`powershell -File scripts/Test-XdbGuards.ps1` → `CHECKS: 7 passed, 0 failed`。第 1 项对**真实随包** `Building.xdb`/`Speedboat.xdb`（已含新 `BlueprintToolBlockBehavior`）判定 **0 冲突**；第 5b 项继续验证“跨文件重复 `<Parameter>` GUID”会被抓；第 6 项确认合法的共享父级 `fefb9590…`（引擎方块行为基类）复用**不误报**。v0.6.1 参数 GUID 修复未回退。

### 4. net472 游戏侧编译（本机，引用 D:\SC 真实程序集）

- 命令：`dotnet build -c Release -p:SCNetGameDir='D:\SC'` → `已成功生成，0 个警告，0 个错误`。编译期即确认 `BlueprintToolBlock : PaintedCubeBlock(15)` 基类、`SubsystemBlockBehavior.OnUse(Engine.Ray3, ComponentMiner)`/`HandledBlocks`、`Terrain.ExtractContents`、`ComponentMiner.ActiveBlockValue`、`ComponentGui`、`Ray3` 等签名可用；item 903 与纹理槽 15 经编译验证。

### 5. 全流程打包（本机，GameDir=D:\SC）

- 命令（须走包装脚本以修正 dotnet 环境）：
  ```powershell
  powershell -File package_wrapper.ps1   # 内部先把 %LOCALAPPDATA%\Microsoft\dotnet(9.0.300) 前置 PATH、设 DOTNET_ROOT + DOTNET_ROLL_FORWARD=Major，再调用 scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release
  ```
- **环境坑（关键）**：直接跑 `Build-NetMod.ps1` 时其 `Test-Logic.ps1` 用 `& dotnet run`，PATH 上的 `dotnet` 解析到 `C:\Program Files\dotnet`（只装了 5.0.408/8.0.400，而 `global.json` 钉 9.0.300）→ 报 `A compatible .NET SDK was not found / Requested SDK version: 9.0.300`、逻辑测试 exit `-2147450735`。解法：用 wrapper `.ps1` 把带 9.0.300 的 `%LOCALAPPDATA%\Microsoft\dotnet` 前置到 PATH 并设 `DOTNET_ROOT`/`DOTNET_ROLL_FORWARD=Major`（bash `export` 传不进子 powershell，故必须在 `.ps1` 内设置）。
- 结果：`Test-Environment` 通过（哈希匹配、2.4.0.0、net472、API 1.42、SC 2.4.10.6、快艇 ABI）、`ALL 147 CHECKS PASSED`、Release `0 错误 0 警告`、打包通过全部内容守卫：必需条目齐、CSV 方块类名唯一（`BlueprintToolBlock, BuilderToolBlock, SpeedboatBlock, UndoToolBlock`）、`Test-XdbMergeSafety` 对打包后真实 `.xdb` 全局 Guid 唯一性 0 问题、不含任何专有游戏程序集。
- 随包核对：`modinfo Version=0.8.0`、DLL 程序集版本 0.8.0.0、Building.xdb `ea25b78e`=0、Speedboat.xdb 仅 1。
- `.netmod`（v0.8.0）SHA-256：`D1FD40CF4EE61DC74E3C9F96106DE101900C829F596134466BB40220581AC700`（取代 v0.7.0 的 `4B227BB5…`）。交付副本：`outputs/BuilderAssistant_v0.8.0.netmod`（字节 2,336,310，哈希与上一致）。

### 6. Android / 7723 实机（待验证）

本轮未在任何设备执行验证；桌面离线全绿 + 编译期 ABI + 打包守卫 ≠ 手机可用。待实测：
1. 装 v0.8.0，进单人创造世界，加载横幅应显示 v0.8.0（不再写死）。
2. 手持蓝图工具点击 → 记下消息打印的真实蓝图库目录；用安卓自带文件管理器把合法蓝图 `.json`（纯 ASCII 文件名）放进该目录（**关键待验证：scoped storage 下该目录是否可被文件管理器写入**；不可写则逃生绳为 ADB push 到同一路径）。
3. 再点蓝图工具 → 应报“已载入蓝图 [1/1]「name」 尺寸 …”；切建造工具对准地面两次点击 → 应落成，撤销工具整体回退。
4. 放非法 JSON（不支持方块/超界/坏语法）→ 应“导入失败：<原因>”，且不污染剪贴板。
5. 多份 `.json` → 连点蓝图工具循环浏览、到末尾回绕，`[i/N]` 计数正确。
6. 建造/预检等待中点蓝图工具 → 应被挡下不误触发。
若失败请回传首条 `Warning/Error` 原文 + 完整 `Game.log`（尤其 `DisplaySmallMessage` 里的真实路径，用于判定 scoped storage 可写性）。

## 2026-10-18：v0.8.1 剪贴板粘贴导入（免建目录/免手写文件）

范围：仅 `src/BuilderAssistant/` 与其离线测试工程。新增纯 Core `Core/Blueprints/BlueprintImportService.cs`（string→`Blueprint`，复用同一 `BlueprintJson.Decode` + 注入材料预检 + 前置尺寸上限，失败恒 `blueprint=null`）、`Core/Blueprints/BlueprintNamePolicy.cs`（显示名→可移植词干，纯中文走 `bp_<FNV 哈希>` 不再塌成 `blueprint`，`ResolveUnique` 撞名 _2/_3 且永不返回已存在词干）；新增游戏侧防御适配器 `Features/WorldEdit/GameInterop.cs`（`TryReadClipboardText` 包 `Game.ClipboardManager.ClipboardString` 全 try/catch；`TryPickBlueprintFileText` 为预留 SAF 桩恒返回未接入）；升级 `WorldEditRuntime` 加 `ImportOrBrowse`/`ImportFromText`/`TextHash` 及 `_lastClipboardImportHash` 去重、`ResetSession` 清空；`Infrastructure/BlueprintStorage.cs` 的 `SanitizeName` 改走 `BlueprintNamePolicy.ToStem`、`Save` 改为枚举既有词干→`ResolveUnique`→写入（自动建目录、不覆盖）。`modinfo.json`/`csproj` 版本 0.8.0→**0.8.1**。**未改 SCIE-Port、未改快艇、未触及联机与既有事务安全策略**；蓝图工具仍只读+校验、**从不写地形**；落成仍走同一 `PlanAndStart`→`BuildPlanner`→`BuildTask`→`TransactionHistory` 主链，安全规则一字不改。

关键发现与取舍（护栏）：反射真实 x25.01.17 Windows 程序集发现，除文档 SCNET Mod API 外还暴露了 public `Game.*` 内部 UI/剪贴板类型（`ClipboardManager.ClipboardString`、`DialogsManager.ShowDialog/Alert/Confirm/Promit`、`TextBoxDialog`、`ListSelectionDialog`、`MessageDialog` 等）——推翻了 v0.8.0“无文本框/文件选择器/对话框”的旧结论（那仅对**文档 API**成立）。它们能编译但 7723 运行时未证实。按“未验证接口绝不当作可用、且勿一次堆太多未验证 UI”的要求，本版**只用最简单的剪贴板读取**，包在 try/catch 里，不可用即**优雅回退** v0.8.0 的 toast+文件浏览；对话框/文本框/SAF 全部留作第二阶段。

### 1. 新增离线逻辑测试（真实执行，本机）

- 命令：`run_offline_tests.ps1` 内先设 `%LOCALAPPDATA%\Microsoft\dotnet`(9.0.300) 到 PATH + `DOTNET_ROOT` + `DOTNET_ROLL_FORWARD=Major`，再 `dotnet run -c Release --project tests/BuilderAssistant.Tests`（测试工程新增编译 `BlueprintImportService.cs`/`BlueprintNamePolicy.cs`，仍不引用游戏程序集）。
- 结果：`ALL 194 CHECKS PASSED`（在原 147 项基础上新增 47 项，exit 0）。
- 覆盖：`BlueprintImportService`——合法粘贴复现正确 3 方块/2×2×1/名称；null/空/纯空白拒；不以 `{` 开头的非 JSON 形拒；`{"…{ + 256K` 超尺寸 blob 拒（证明尺寸门先于 tokenizer）且留 `blueprint=null`；坏语法 JSON 拒；重复坐标拒；1025 cells 超限拒（计数门先于坐标门）；材料预检拒未知块且同 JSON 在“全支持”下成功；七类坏输入逐一断言“失败⇒`blueprint=null`”即剪贴板完整性。`BlueprintNamePolicy`——sanitize 仅留 `[A-Za-z0-9_-]`/裁首尾分隔/截 48；纯中文名 `ToStem` 得 `bp_<hash>` 且两个不同中文名不同、同名确定；`ResolveUnique` 空集返回基名且 `collided=null`、撞名 `_2/_3`、结果必不在已存在集合、重复中文名得 `…_2`；`StableHash` 确定性且 8 位十六进制。均不依赖文件 IO 或游戏程序集。

### 2. XDB 合并安全守卫回归（本机）

- 命令：`powershell -File scripts/Test-XdbGuards.ps1` → `CHECKS: 7 passed, 0 failed`。本轮未改任何 `.xdb`，第 1 项对真实随包文件判 0 冲突，`ea25b78e` 仍 0、Speedboat 仍 1，v0.6.1 参数 GUID 修复未回退。

### 3. net472 游戏侧编译（本机，引用 D:\SC 真实程序集）

- 命令：`build_net472.ps1` → `dotnet build -c Release -p:SCNetGameDir='D:\SC'` → `已成功生成，0 个警告，0 个错误`。编译期确认 `Game.ClipboardManager.ClipboardString`、`BlueprintToolBlock/BuilderToolBlock/UndoToolBlock`、`ComponentGui`、`SubsystemBlockBehavior.OnUse` 等签名可用；`GameInterop` 编译通过只证明“能编”，不等于“真机能跑”。

### 4. 全流程打包（本机，GameDir=D:\SC）

- 命令：`run_pack.ps1`（内含 dotnet 环境修正 wrapper，调 `scripts/Build-NetMod.ps1 -GameDir 'D:\SC'`）。
- 结果：逻辑测试 `ALL 194 CHECKS PASSED`、Release `0 错误 0 警告`、打包内容守卫全过——必需条目齐、CSV 方块类名唯一（`BlueprintToolBlock, BuilderToolBlock, SpeedboatBlock, UndoToolBlock`）、打包后真实 `.xdb` 全局 Guid 唯一性 0 问题、不含任何专有游戏程序集。
- 随包核对：`modinfo Version=0.8.1`、DLL 程序集版本 0.8.1.0、Building.xdb `ea25b78e`=0、Speedboat.xdb 仅 1。
- `.netmod`（v0.8.1）SHA-256：`E71E73866B0DA2B3C603F875E6DB6989C6A1287A221FE20E45139891B1F1310C`（2,338,624 字节，取代 v0.8.0 的 `D1FD40CF…`）。交付副本：`outputs/BuilderAssistant_v0.8.1.netmod`（哈希与上一致）。

### 5. Android / 7723 实机（待验证）

本轮未在任何设备执行验证；桌面全绿 + 编译期 ABI + 打包守卫 ≠ 手机可用。待实测见 `docs/COMPATIBILITY.md`「v0.8.1」清单，核心：**剪贴板读取(`Game.ClipboardManager`)在 7723 是否可用、拿到的是否系统剪贴板文本**——若不可用应自动回退到 v0.8.0 文件浏览（不阻塞可用版本）；真机文件落盘（scoped storage 写 `UserDataPath`）仍待验证；SAF 系统文件选择器为**不支持/未接入**（第二阶段）。

## 2026-10-18：v0.8.2 扩充纯立方石/砖方块（Phase A，只扩白名单）

范围：**仅** `src/BuilderAssistant/Features/WorldEdit/VanillaBlockPalette.cs`（`SupportedPlainCubes` 由 5→12，追加 Basalt/Granite/Marble/Sandstone/Clay/Limestone/Malachite）、`tests/BuilderAssistant.Tests/Program.cs`（新增 2 个纯逻辑测试）、`modinfo.json`/`csproj`（0.8.1→**0.8.2**）与 `docs/`。**未改** schema（仍 `schemaVersion=1`）、`GameTerrainGateway` 写路径（仍 `data=0/light=0`、不含颜色）、`BlueprintJson`/`BlueprintCopier`（材料键本就是 CLR 类型名）、`Building.xdb`（无新方块条目/行为）、SCIE-Port、快艇、联机、既有事务安全策略。依据：反射 `D:\SC\Survivalcraft.exe` 立方族全谱，只取 data=0 可完整复现、无朝向/实体/相态位的纯立方；实体/相态/重力/蔓延/不可破坏/朝向原木/AlphaTest 均明确排除。

### 1. 新增离线逻辑测试（真实执行，本机）

- 命令：`DOTNET_ROLL_FORWARD=Major` + `dotnet run -c Release --project tests/BuilderAssistant.Tests`（测试工程仍不引用游戏程序集）。
- 结果：`ALL 203 CHECKS PASSED`（在 v0.8.1 的 194 项基础上新增 9 项，exit 0）。
- 新增用例：`TestBlueprintJsonPreservesConcreteBlockNameKeys`——以 7 个具体方块类型名作 material key 造蓝图，`Serialize`→`Decode` 逐字复现全部 key、cell 数一致、再序列化二次稳定（证明**扩白名单无需改 schema/Decode**）；`TestImportGateAcceptsOrRejectsNewConcreteKeys`——同一 JSON 在 resolver 全放行时导入 7/7、在拒收其中一块时失败并 `blueprint=null` 且原因点名该块（证明 resolver 是唯一门控、未注册块只会干净中止绝不塞垃圾）。均不依赖游戏程序集或文件 IO。

### 2. XDB 合并安全守卫回归（本机）

- 命令：`powershell -File scripts/Test-XdbGuards.ps1` → `CHECKS: 7 passed, 0 failed`。本轮未改任何 `.xdb`，第 1 项对真实随包文件判 0 冲突，`ea25b78e` 仍 0、Speedboat 仍 1，v0.6.1 参数 GUID 修复未回退。

### 3. net472 游戏侧编译（本机，引用 D:\SC 真实程序集）

- `scripts/Test-Environment.ps1 -GameDir D:\SC` → 哈希匹配、程序集 2.4.0.0、net472、API 1.42、SC 2.4.10.6、快艇 ABI 全部通过。
- `dotnet build -c Release -p:SCNetGameDir='D:\SC'` → `已成功生成，0 个警告，0 个错误`。`SupportedPlainCubes` 是 `HashSet<string>` 字面量，编译期即确认无语法/类型问题（新块名的实际可解析性经 `FindBlockByTypeName` 属**运行时**，故 7723 渲染仍待验证）。

### 4. 全流程打包（本机，GameDir=D:\SC）

- 命令：临时 `package_v082.ps1`（前置 `%LOCALAPPDATA%\Microsoft\dotnet` 到 PATH + `DOTNET_ROOT` + `DOTNET_ROLL_FORWARD=Major` 后调 `scripts/Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release`）。
- 结果：逻辑 `ALL 203 CHECKS PASSED`、Release `0 错误 0 警告`、打包内容守卫全过——必需条目齐、CSV 方块类名唯一、打包后真实 `.xdb` 全局 Guid 唯一性 0 问题、不含任何专有游戏程序集。
- 随包核对（解包 `outputs/BuilderAssistant_v0.8.2.netmod` 校验）：`modinfo Version=0.8.2`、`BuilderAssistant.dll` 程序集版本 `0.8.2.0`、`Building.xdb` `ea25b78e`=0、条目仅 8 项无游戏程序集。
- `.netmod`（v0.8.2）SHA-256：`7FE91876B0C9A9AF93E750EC8DE0985E891207C2C925448C593C6C0ABDCD04AA`（2,339,153 字节，取代 v0.8.1 的 `E71E7386…`）。交付副本：`outputs/BuilderAssistant_v0.8.2.netmod`（哈希与上一致）。

### 5. Android / 7723 实机（待验证）

本轮未在任何设备执行验证；桌面全绿 + 编译期 ABI + 打包守卫 ≠ 手机可用。逐块（玄武岩/花岗岩/大理石/砂岩/黏土/石灰岩/孔雀石）在 7723 的渲染、复制/落成往返、以及“上色块落成是否默认色”均为**待验证**，实测清单见 `docs/COMPATIBILITY.md`「v0.8.2」。任何一块未实测前不得标兼容。染色方块（Phase B）留待后续，需 `schemaVersion=2` + 按材料 color + `IPaintableBlock`/地形上色 API + 真机验证。
