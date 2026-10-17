# 测试结果

日期：2026-10-17  
目标：`Survivalcraft NET 2.4 Net_x25.01.17`  
本机仓库目录：`D:\Survivalcraft2\SCIE-Port`（文档口径中的 `E:\Survivalcraft2`）  
本机游戏程序集目录：`D:\SCNET`

## 结果矩阵

| 项目 | 状态 | 证据/说明 |
|---|---|---|
| 上游 SHA 固定 | 已确认 | `git rev-parse HEAD` = `4864d956793b6e670413019f1f6b1cc5c8bb0970`；工作树干净 |
| SCIE 许可文件检查 | 已确认 | 固定树没有 LICENSE/COPYING；因此当前不分发上游内容 |
| x25 Windows 程序集指纹 | 已确认 | 三项 SHA-256 与 `Test-Environment.ps1` 固定值一致 |
| 目标 ABI 检查 | 已确认 | 版本 2.4.0.0、API 1.42、SC 2.4.10.6、net472；ModLoader/CSV/ID 及 Boat/Mount/Rider/SpawnCreature 签名检查通过 |
| 原 SCIE 工程原样构建 | 不支持 | 旧 csproj 硬编码 `D:\Survivalcraft`；缺引用时产生 1378 错误，不能作为目标构建方式 |
| 旧源码直接引用 x25 编译 | 不支持 | target probe 暴露 56 个第一层兼容错误：PluginLoader、几何、UpdateOrder、交互、配方、类型冲突等 |
| SCIE-Port Release 编译 | 已确认 | `dotnet build`：0 warning，0 error |
| 0.3.0/0.3.1 世界加载 `Database not loaded` 根因 | 已确认（机理） | 反编译 `ModsManager.CombineDataBase`：合并按 `LocalName + Guid` 匹配。0.3.1 的 `Watercraft.xdb` 三个原版文件夹用了自定义 GUID（Entities `3ea62aa2…`、Gameplay `fa264e34…`、BlockBehaviors `36e1b3bf…`），与游戏真实原版文件夹 GUID 不一致，导致合并时追加“同名不同 GUID”的游离 `<Folder>`，蒸汽船内容被放进非标准容器而非并入原版 `Entities`。**旧的“根节点造成嵌套 DatabaseObjects”是误诊**：能正常运行的 Builder Assistant `Speedboat.xdb` 恰恰以 `<DatabaseObjects>` 为根且使用正确原版文件夹 GUID。 |
| 0.3.2 修复 | 已确认（编译+静态） | 仅把三个文件夹 GUID 改回原版值（Entities `5fcdf35f…`、Gameplay `d3d4b692…`、BlockBehaviors `00c97f0f…`）；保留 `<SCIEPortWatercraft>` 容器根、全部模板、GUID、InheritanceParent、ProjectTemplate。Release 0 warning / 0 error。 |
| 真实合并验证 harness | 已确认（本机 Windows 程序集） | 反射调用 `D:\SCNET\Survivalcraft.exe` 的 `ModsManager.CombineDataBase`，对合成原版 `<DatabaseObjects>` 分别合并修复前/后 xdb：修复后每种原版文件夹各 1 个、`SCIEPortSteamBoat` 并入真实 `Entities(5fcdf35f…)`；修复前各 2 个（含 rogue），蒸汽船被隔离在游离文件夹。见 `outputs/merge-evidence.txt`。 |
| 完整反序列化端到端 | 已确认（本机 Windows 程序集真实代码路径） | 用游戏自身的 `SurvivalCraftModEntity.GetDecipherStream` 解出 `Content.scpak`（明文即标准 ZIP，head `50 4b 03 04`，987 个条目），从中取到真实原版 `Assets/Database.xml`（463,610 字节，含 `<DatabaseObjectTypes>`，10 个类型定义），再调用游戏真实的 `Game.DatabaseManager.LoadDataBaseFromXml`（正是加载界面 `b__16_8` 里被 try/catch 吞成 `Warning(e.Message)` 的那一步）跑端到端。结果：**0.3.1(BROKEN)** 首条真异常为 `System.InvalidOperationException: Another database object with name "Entities" is already nested in database object "Root".`（栈：`DatabaseObject.set_NestingParent` ← `XmlDatabaseSerializer.LoadDatabase:77` ← `DatabaseManager.LoadDataBaseFromXml:42`）——即 rogue 同名 `Entities` 文件夹让两个同名 Folder 同时挂到 Root，反序列化器直接抛异常，`m_gameDatabase` 保持 null，进世界时 `get_GameDatabase()` 才补刀 `Database not loaded.`。**0.3.2(FIXED)** 与**阳性对照 Builder Assistant Speedboat(手机已知能跑)** 均 `LoadDataBaseFromXml: SUCCESS`。对照通过同时反证 harness 忠实还原了手机加载路径。见 `outputs/MergeHarness-v2-evidence.txt`。注：本机为桌面 .NET Framework 构建；Android 同源代码，最终仍以手机实测为准。 |
| `Test-Database.ps1` 校验脚本 | 已确认 | 新增脚本并接入 `Build-NetMod.ps1`：禁止“同名不同 GUID 的原版文件夹”“单文件内同名不同 GUID 重复文件夹”“悬空/未知 InheritanceParent”。对修复后 xdb PASS(0)，对 0.3.1 rogue 版 FAIL(1) 并精确点出三处 GUID。 |
| `.netmod` 内容校验 | 已确认 | 0.3.2 包含 DLL、modinfo、两块 CSV、修复后的 Watercraft XDB、授权蒸汽船模型/纹理及第三方声明 |
| `.netmod` 大小 | 已确认 | 0.3.2：21,121 字节（重新构建后；构建把时间戳写入 PE/zip，字节较首次的 21,117 有 ±4 差异，内容同为 0.3.2 修复） |
| `.netmod` SHA-256 | 已确认 | 0.3.2（本次重新构建）：`E0C3568E8741EE268D2B8E1332DC2572BD1F91DE1E93A6FE89E4A8A375EA95EA`。首次构建同内容版本为 `AC22833A…`（因 PE/zip 时间戳不同，与上面差 4 字节，属正常非确定性构建）。0.3.1 旧值 `78E44A1C…`，进入世界报 `Database not loaded`。 |
| 蒸汽船物品/组件编译 | 已确认 | ID 999；独立实体、原生 Boat/Mount/Body/Damage 和本地推进组件均以 Release 0 warning/0 error 编译 |
| SCIE 蒸汽船模型 | 已确认 | 固定提交文件 25,178 字节；SHA-256 `E3C2F54513D10486FA14927418E6ABB76CB7AC270B947A57C338A2423911C4F8`；`Cylinder` 节点静态检查通过 |
| SCIE 蒸汽船纹理 | 已确认 | 64×64 PNG，6,036 字节；SHA-256 `BB0CC5F9886E30BA9DE12D79F1C7724CBF9C6341DCE7606C65906441ABE8F6A4` |
| Windows 游戏加载 | 待验证 | 本次没有启动游戏进程 |
| Windows 创造栏显示材料 | 待验证 | 必须在新建可丢弃单人世界确认 |
| Windows 蒸汽船生成/骑乘/驾驶 | 待验证 | XDB、内置模型路径、触屏输入和实体运行行为必须在游戏中确认 |
| 0.3.2 世界加载（进入世界） | 已确认（桌面真实反序列化）/ Android 待实测 | 桌面 harness 已用真实 `DatabaseManager.LoadDataBaseFromXml` 证明：修复后首异常 `Another database object with name "Entities" is already nested in database object "Root".` 消失、加载成功，且阳性对照同样成功。Android 与桌面同源，逻辑应一致；唯一剩余门槛是手机实机确认（须确保加载的确实是 0.3.2 而非旧 0.3.1）。 |
| Windows 保存/重载 | 待验证 | 仅创造物品也需验证存档不报错 |
| Android 7723 实测 | 待验证 | 尚未部署到手机，Windows 指纹不能替代 Android 证据 |
| 多人主机/客户端 | 不支持 | 0.3.0 的额外推进明确仅启用 `WorkType.Local`；尚无服务器权威输入同步 |

## 执行命令

```powershell
cd D:\Survivalcraft2\SCIE-Port
.\scripts\Test-Environment.ps1 -GameDir 'D:\SCNET'
.\scripts\Test-Database.ps1                 # 单独运行数据库静态校验
.\scripts\Build-NetMod.ps1 -GameDir 'D:\SCNET'   # 构建时会自动再跑一次 Test-Database.ps1
```

以上三条命令在修复后的 0.3.2 代码树上都应成功。`Build-NetMod.ps1` 在打包前会自动调用 `Test-Database.ps1`；一旦出现“同名不同 GUID 的原版文件夹”或悬空 `InheritanceParent`，构建即失败，防止本类缺陷再次进入 `.netmod`。

## 包清单

```text
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Database/Watercraft.xdb
Assets/Models/SteamBoat.dae
Assets/Textures/SteamBoat.png
modinfo.json
SCIEPort.dll
THIRD-PARTY-NOTICES.md
```

包内未发现：`Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll`、`Jint.dll`、`System.Reflection.TypeExtensions.dll`、SCIE 音频/旧 XDB/配方/翻译或其他载具资源。

## 下一轮运行测试步骤

1. 备份并使用新建单人世界。
2. 把 `artifacts/SCIEPort.netmod` 放入该精确客户端的 `NetMods`。
3. 启动后保存完整加载日志，确认 `SCIEPort` 被发现且无 CSV/Block 初始化警告。
4. 进入创造模式，搜索并拿取 `SCIE Port Test Alloy`；检查图标、名称、描述、堆叠、丢弃、拾取。
5. 搜索 `SCIE 蒸汽船（原型）`，对水面使用；确认只生成一艘实体且物品减少。
6. 验证上船、触屏前进/后退/转向、松手减速、下船、碰撞损坏及附近最多 4 艘限制。
7. 保存退出并重进，确认玩家库存、船实体和位置可恢复且日志无实体模板错误。
8. Android 上重复 2–7，并记录 APK/程序集指纹、设备、Android 版本和日志。

## Android (SCNET x25.01.17 / 7723) 安装、卸载旧包、隔离测试步骤

这一步是判断 `Database not loaded` 是否真正解决的唯一权威证据；本机 Windows 合并验证不能替代它。

1. 记录当前包（用于回滚定位）：在手机上删除旧版前先确认 NetMods 目录里存在的是 0.3.1 的 `SCIEPort.netmod`。0.3.1 与 0.3.2 的 `PackageName` 都是 `SCIEPort`，同名会互相遮蔽，**必须先彻底卸载旧包**。
2. 卸载旧包：进入 Survivalcraft 的 Mod/NetMods 管理界面删除 `SCIEPort`，或直接用文件管理器删掉 `NetMods` 内的旧 `SCIEPort.netmod`（`PackageName=SCIEPort`，文件名可能是 `SCIEPort.netmod` 或安装器改写过名字，以 `modinfo.json` 里 `PackageName` 为准）。确认目录内不再残留任何 `SCIEPort` 相关文件。
3. 确认真实 NetMods 路径：Survivalcraft 在 Android 上读取应用私有目录下的 `NetMods`（典型为 `Android/data/<包名>/files/NetMods`，部分版本在 `.../Documents/NetMods`）。以游戏内“已加载 Mod 列表”能显示的路径为准；放错目录不会报错，只会“看不到 Mod”。
4. 部署 0.3.2：把 `artifacts/SCIEPort.netmod`（或交付的 `SCIEPort-0.3.2.netmod`，内容相同）拷进上述 `NetMods`，确保**目录里只有这一个 `SCIEPort`**。核对 SHA-256 = `E0C3568E8741EE268D2B8E1332DC2572BD1F91DE1E93A6FE89E4A8A375EA95EA`（21,121 字节），与本次重新构建一致。
5. 冷启动游戏：完全退出进程后重开（避免旧 DLL/DB 缓存残留），进入 Mod 管理确认 `SCIE Industrial Era Port (Audit Build) 0.3.2` 已启用。
6. 隔离世界测试：新建一个可丢弃的**单人创造世界**（不要用会污染主存档的世界），确认能否正常进入世界——重点看是否还出现 `Database not loaded`。
7. 抓加载期 WARNING：真实首异常只以 `Warning(e.Message)`（只有文字、无堆栈）出现在**读条/加载阶段**，晚一点才会在进世界时报 `Database not loaded`（那只是下游症状）。已在本机复现出 0.3.1 的确切首异常原文是 `Another database object with name "Entities" is already nested in database object "Root".`。请在日志里翻到**第一次进 `GameLoading` 之前/期间的 WARNING 行**：如果看到的仍是这条（或同类的“…is already nested…”“…already present…”），说明手机加载的还是 0.3.1；如果 0.3.2 确实已加载却换了**别的** WARNING 文字（例如某个具体模板/父级 GUID），把那条原文+设备/Android 版本/完整日志回传，据此继续定位，不宣称已修复。
8. 若成功进入世界：继续步骤 4–7 的蒸汽船功能实测（搜索/拿取、放置、上船、驾驶、下船、存档重载），全部通过后才可把“0.3.2 世界加载”等运行项从“待验证”改为“已确认”。

只有以上证据完成后，才能把相应运行项目从“待验证”改为“已确认”。
