# 测试结果

日期：2026-10-17
目标：`Survivalcraft NET 2.4 Net_x25.01.17`
本机仓库目录：`E:\Survivalcraft2\SCIE-Port`
本机游戏程序集目录：`E:\SCNET\SCNETx25.01.17`

## 结果矩阵

| 项目 | 状态 | 证据/说明 |
|---|---|---|
| 上游 SHA 固定 | 已确认 | `git rev-parse HEAD` = `4864d956793b6e670413019f1f6b1cc5c8bb0970`；工作树干净 |
| SCIE 许可文件检查 | 已确认 | 固定树没有 LICENSE/COPYING；用户声明已获明确许可，车辆资源及公开发布留档边界见第三方声明 |
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
| `.netmod` 内容校验 | 已确认 | 0.3.3 包含 DLL、modinfo、两块 CSV、修复后的 Watercraft XDB、**朝向修正后的**蒸汽船模型（Y_UP）/授权纹理及第三方声明；打包级校验强制模型 `up_axis=Y_UP` 且顶点数组保持 98 顶点（294 float）+ `Cylinder` 节点 |
| `.netmod` 大小 | 已确认 | 0.3.3：20,975 字节（较 0.3.2 变化源于打包模型由上游 Z_UP 原文件换成项目内修正后的 Y_UP 资产，且 zip 时间戳非确定性 ±若干字节） |
| `.netmod` SHA-256 | 已确认 | 0.3.3：`B86B9B152BE738697FB2AE9A4A901208FD0EBD577C9F93BA20B5344CDDCDBCCA`（20,975 字节）。历史：0.3.2（世界加载修复）`E0C3568E…` / 首次构建 `AC22833A…`；0.3.1 旧值 `78E44A1C…`（进入世界报 `Database not loaded`）。 |
| `.netmod` 内容校验 | 已确认 | 0.5.1 包含 DLL、modinfo、CSV、Watercraft XDB、四种载具/铁轨代码、三份 Y_UP 派生模型、车厢模型、纹理及第三方声明 |
| `.netmod` 大小 | 已确认 | 0.5.1：136,966 字节；DAE 等文本资源经 ZIP 压缩 |
| `.netmod` SHA-256 | 已确认 | 0.5.1：`68DD8F38C7962953C655BC84A3CECD542F7E2063E21E22CF70D06E036A1070A6` |
| 0.6.0 Kelly 源码迁移 | 已确认 | 三个实现文件已移至 `src/SCIEPort/Features/KellyPlants`，命名空间及 XDB 类名改为 `SCIEPort.Features.KellyPlants`；Builder Assistant 不再包含或注册 Kelly 功能 |
| 0.6.0 Kelly 资源 | 已确认 | 14 条 CSV、`KellyPlants.xdb`、`KellyFruits.dae`、`KellyExpansion.webp` 均位于 SCIE-Port；模型 SHA-256 `C35D3B5DDD7D42464F9DA55C88A5A87AEE832FDB1E91B8864B6F1E4E4A6B40A9`，贴图 SHA-256 `E4C99B07E7F4D2FEB8420D889C3CC0559DC835B951D1AC56CA5FDD87320CE0` |
| 0.6.0 Kelly 原名守卫 | 已确认 | 14 个名称按 CSV 顺序拼接的 UTF-8 SHA-256 为 `5b868dfafcb2fd858f71f73d0997c16779a2cbf7f5696848c1df341651cb3656`；无中英文括号，构建脚本会拒绝改名或追加括号移植标注 |
| 0.6.0 原版 ID 扫描 | 已确认 | `Test-Environment.ps1 -GameDir D:\SC` 通过；目标程序集读出 260 个原生方块常量，903–916 与 995–1000 均未冲突 |
| 0.6.0 Release 编译 | 已确认 | 使用已安装 .NET SDK 8.0.400 的 MSBuild 直接构建 net472，`TreatWarningsAsErrors=true`，成功生成 `SCIEPort.dll`，无错误或警告 |
| 0.6.0 XDB 静态校验 | 已确认 | `KellyPlants.xdb` 与 `Watercraft.xdb` 同时通过 `Test-Database.ps1`；原版文件夹 GUID、内部继承与已知原版父级均可解析 |
| 0.6.0 `.netmod` 完整打包 | 待验证 | 本机缺少被 Git 忽略的 `upstream/SCIE` 固定快照；自动获取固定提交时 GitHub 443 不可达，因此既有 SCIE 三个授权资源无法重新取入，本轮未生成新的完整 `.netmod`，不得伪报包哈希 |
| 0.6.0 Android Kelly 运行 | 待验证 | 尚未在 7723 客户端验证原名显示、贴图/模型、播种成长、生态生成、雨后蘑菇、存读档与性能 |
| 蒸汽船物品/组件编译 | 已确认 | ID 999；独立实体、原生 Boat/Mount/Body/Damage 和本地推进组件均以 Release 0 warning/0 error 编译 |
| SCIE 蒸汽船模型 | 已确认 | 上游授权源模型（保持不动、仅溯源）：`upstream/SCIE/IndustrialMod/Models/SteamBoat.dae`，25,178 字节，SHA-256 `E3C2F54513D10486FA14927418E6ABB76CB7AC270B947A57C338A2423911C4F8`，`up_axis=Z_UP`。**实际打包的模型**（项目内朝向修正资产）：`src/SCIEPort/Assets/Models/SteamBoat.dae`，25,042 字节，`up_axis=Y_UP`，仍含 `Cylinder` 节点与 98 顶点（294 float）。 |
| 蒸汽船“竖着”朝向修复 | 已确认（文件/打包级）/ Android 待实测 | 现象：手机进世界能加载，但船竖直立着。根因：SCIE 的 `SteamBoat.dae` 是 Blender 导出的 `Z_UP`（把高度放在 Z 轴），而 SCNET 的 `SimpleModel` 按游戏约定直接读原始坐标、把 Y 当上、Z 当前——于是 Z_UP 模型的高度轴被当成“前后”，船就竖起来；对照能正常横放的 Builder Assistant `Speedboat.dae` 是手写的 `Y_UP`。修复：对 `Cylinder-mesh-positions-array`（98 顶点）与 `Cylinder-mesh-normals-array`（135 法线）逐三元组烘焙绕 X 轴 −90° 旋转 `(x,y,z)->(x,z,-y)`（右手系不翻转），并把 `<up_axis>` 改为 `Y_UP`；不改 `SimpleModel` 参数、不动 `Body` 的 `BoxSize`、不动模板/GUID。结果包围盒由（宽 X±1.06 / 高 Z0.06..1.56 / 长 Y−2.74..2.35）变为（宽 X±1.06 / 高 Y−0.13..1.56 / 长 Z−2.35..2.74），与 `BoxSize=2,0.5,4.5`（Z 为船长）一致。**边界**：本机只能在文件与打包级验证几何已旋转、`up_axis=Y_UP`、顶点数未丢，且已确认随包模型确为 Y_UP；手机里船是否真正平放仍须实机确认，不提前宣称已修复。 |
| SCIE 蒸汽船纹理 | 已确认 | 64×64 PNG，6,036 字节；SHA-256 `BB0CC5F9886E30BA9DE12D79F1C7724CBF9C6341DCE7606C65906441ABE8F6A4` |
| 飞艇物品/实体/飞行组件编译 | 已确认 | ID 998；独立实体、Mount/Body/Damage、触屏输入和本地限速飞行均为 Release 0 warning/0 error |
| 0.5.0 Android 飞艇缺陷 | 已确认 | 用户实测飞艇竖直且无法骑乘；源模型 Z_UP，且 9 格宽物理体让玩家无法进入原生 2.5 格座位搜索范围 |
| 0.5.1 SCIE 飞艇派生模型 | 已确认 | Y_UP、926 顶点，范围 X `-4.628..4.628`、Y `0..11.162`、Z `-15.925..16.054`；145,775 字节，SHA-256 `E1A4DD77D02334607A88349472471B711FE1B1DEE5EE75DF510DB38DCAC3664A` |
| 0.5.1 飞艇骑乘参数 | 已确认（静态/编译） | Body `3,2,6`、Mount `0,1,-0.8`、Dismount `1.75,0.5,0`，生成点为命中点上方 0.1 格；Android 实际上船待验证 |
| 铁轨/火车/车厢编译 | 已确认 | ID 997/996/995；轨道几何、邻接形状、实体生成、骑乘及本地沿轨组件为 Release 0 warning/0 error |
| SCIE 火车模型/纹理 | 已确认 | 0.5.1 派生 `Train.dae` 为 Y_UP、220 顶点、49,781 字节，SHA-256 `9269DC2A0047EDC7A68D93F6C88236B81BA26292DA74723520B4C5244185A29D`，`Cylinder` 节点通过；`Train.png` SHA-256 `413E44943BA96517FB0B1712EB0ACC157CE60E14BE7B96C7D0B3FB37C3A5CD57` |
| SCIE 车厢模型 | 已确认 | `Minecart.dae` 432,139 字节，SHA-256 `CDB55F28AA29BAB7067DF8D68217953BB70EB8D3544BC10A337C2BDD639D88F5`，`obj1` 节点检查通过 |
| Watercraft XDB 静态结构 | 已确认 | 自定义根 `SCIEPortWatercraft`；109 个 XML 节点、108 个 GUID、无重复 GUID |
| Windows 游戏加载 | 待验证 | 本次没有启动游戏进程 |
| Windows 创造栏显示材料 | 待验证 | 必须在新建可丢弃单人世界确认 |
| Windows 蒸汽船生成/骑乘/驾驶 | 待验证 | XDB、内置模型路径、触屏输入和实体运行行为必须在游戏中确认 |
| 世界加载（进入世界） | 已确认（桌面真实反序列化 + 手机实测） | 桌面 harness 已用真实 `DatabaseManager.LoadDataBaseFromXml` 证明：修复后首异常 `Another database object with name "Entities" is already nested in database object "Root".` 消失、加载成功，且阳性对照同样成功。手机实机反馈“经过测试可以了”——能进世界，不再报 `Database not loaded`，仅剩“船竖着”的外观问题（见“蒸汽船朝向修复”行）。0.3.3 未改 XDB（只换模型），故该世界加载结论直接继承。 |
| 0.5.1 Android 四种载具 | 待验证 | 蒸汽船、修复后飞艇、修复后火车、车厢的姿态、生成、骑乘、驾驶和保存恢复均需实机复测 |
| 0.5.1 铁轨运行 | 待验证 | 直轨、四种弯轨、四种坡轨、自动形状更新及沿轨转向未启动游戏验证 |
| Windows 保存/重载 | 待验证 | 仅创造物品也需验证存档不报错 |
| Android 7723 实测 | 待验证 | 尚未部署到手机，Windows 指纹不能替代 Android 证据 |
| 多人主机/客户端 | 不支持 | 四种载具的自定义推进/飞行/轨道物理仅启用 `WorkType.Local`；尚无服务器权威输入同步 |
| 火车蒸汽燃料、车厢编组/货箱 | 不支持 | 依赖尚未迁移的工业机器、库存、父体同步和存档链；0.5.1 未以占位实现冒充完成 |

## 执行命令

```powershell
cd E:\Survivalcraft2\SCIE-Port
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Test-Database.ps1                 # 单独运行数据库静态校验
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'   # 构建时会自动再跑一次 Test-Database.ps1
```

以上三条命令在修复后的 0.3.2 代码树上都应成功。`Build-NetMod.ps1` 在打包前会自动调用 `Test-Database.ps1`；一旦出现“同名不同 GUID 的原版文件夹”或悬空 `InheritanceParent`，构建即失败，防止本类缺陷再次进入 `.netmod`。

## 包清单

```text
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Blocks/KellyPlants.csv
Assets/Database/Watercraft.xdb
Assets/Database/KellyPlants.xdb
Assets/Models/Airship.dae
Assets/Models/KellyFruits.dae
Assets/Models/Minecart.dae
Assets/Models/SteamBoat.dae
Assets/Models/Train.dae
Assets/Textures/SteamBoat.png
Assets/Textures/Train.png
Assets/Textures/KellyExpansion.webp
modinfo.json
SCIEPort.dll
THIRD-PARTY-NOTICES.md
```

包内未发现：`Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll`、`Jint.dll`、`System.Reflection.TypeExtensions.dll`、SCIE 音频/旧 XDB/配方/翻译。

## 下一轮运行测试步骤

1. 备份并使用新建单人世界。
2. 把 `artifacts/SCIEPort.netmod` 放入该精确客户端的 `NetMods`。
3. 启动后保存完整加载日志，确认 `SCIEPort` 被发现且无 CSV/Block 初始化警告。
4. 进入创造模式，搜索并拿取 `SCIE Port Test Alloy`；检查图标、名称、描述、堆叠、丢弃、拾取。
5. 搜索 `SCIE 蒸汽船（原型）`，对水面使用；确认只生成一艘实体且物品减少。
6. 验证上船、触屏前进/后退/转向、松手减速、下船、碰撞损坏及附近最多 4 艘限制。
7. 保存退出并重进，确认玩家库存、船实体和位置可恢复且日志无实体模板错误。
8. 搜索 `SCIE 飞艇（授权移植预览）`，在开阔地生成；验证乘坐、触屏前后推进、视角控制升降、转向、下船、浸水损坏和附近 2 艘限制。
9. 保存退出并重进，确认飞艇位置和状态可恢复。
10. 搜索并铺设 `SCIE 铁轨（授权移植预览）`：分别验证直线、四向弯道和一格高坡道是否自动成形。
11. 把 `SCIE 火车（授权移植预览）` 放到轨道上，验证非轨道拒绝生成、乘坐、触屏前后驾驶、弯道和坡道。
12. 对 `SCIE 车厢（授权移植预览）` 重复生成和独立低速试乘；当前不应期待自动编组或货箱。
13. 保存退出并重进，确认铁轨 data、两种轨道实体位置/方向恢复且日志无模板错误。
14. Android 上重复 2–13，并记录 APK/程序集指纹、设备、Android 版本和日志。

## Android (SCNET x25.01.17 / 7723) 安装、卸载旧包、隔离测试步骤

这一步是判断 `Database not loaded` 是否真正解决的唯一权威证据；本机 Windows 合并验证不能替代它。

1. 记录当前包（用于回滚定位）：在手机上删除旧版前先确认 NetMods 目录里存在的是 0.3.1 的 `SCIEPort.netmod`。0.3.1 与 0.3.2 的 `PackageName` 都是 `SCIEPort`，同名会互相遮蔽，**必须先彻底卸载旧包**。
2. 卸载旧包：进入 Survivalcraft 的 Mod/NetMods 管理界面删除 `SCIEPort`，或直接用文件管理器删掉 `NetMods` 内的旧 `SCIEPort.netmod`（`PackageName=SCIEPort`，文件名可能是 `SCIEPort.netmod` 或安装器改写过名字，以 `modinfo.json` 里 `PackageName` 为准）。确认目录内不再残留任何 `SCIEPort` 相关文件。
3. 确认真实 NetMods 路径：Survivalcraft 在 Android 上读取应用私有目录下的 `NetMods`（典型为 `Android/data/<包名>/files/NetMods`，部分版本在 `.../Documents/NetMods`）。以游戏内“已加载 Mod 列表”能显示的路径为准；放错目录不会报错，只会“看不到 Mod”。
4. 部署 0.3.3：把 `artifacts/SCIEPort.netmod`（或交付的 `SCIEPort-0.3.3.netmod`，内容相同）拷进上述 `NetMods`，确保**目录里只有这一个 `SCIEPort`**。核对 SHA-256 = `B86B9B152BE738697FB2AE9A4A901208FD0EBD577C9F93BA20B5344CDDCDBCCA`（20,975 字节），与本次构建一致。0.3.1/0.3.2/0.3.3 的 `PackageName` 都是 `SCIEPort`，同名互相遮蔽，务必先按步骤 2 彻底卸载旧版再放入 0.3.3。
5. 冷启动游戏：完全退出进程后重开（避免旧 DLL/DB 缓存残留），进入 Mod 管理确认 `SCIE Industrial Era Port (Audit Build) 0.3.3` 已启用。
6. 隔离世界测试：新建一个可丢弃的**单人创造世界**（不要用会污染主存档的世界）。本轮重点：①世界仍能正常进入、无 `Database not loaded`（0.3.3 的 XDB 与已实测通过的 0.3.2 完全相同，只换了模型，理论上不会回归）；②生成蒸汽船后**船是否平放在水面**、不再竖着——这是本次朝向修复要验证的核心。
7. 抓加载期 WARNING：真实首异常只以 `Warning(e.Message)`（只有文字、无堆栈）出现在**读条/加载阶段**，晚一点才会在进世界时报 `Database not loaded`（那只是下游症状）。已在本机复现出 0.3.1 的确切首异常原文是 `Another database object with name "Entities" is already nested in database object "Root".`。请在日志里翻到**第一次进 `GameLoading` 之前/期间的 WARNING 行**：如果看到的仍是这条（或同类的“…is already nested…”“…already present…”），说明手机加载的还是旧包；如果 0.3.3 确实已加载却换了**别的** WARNING 文字（例如某个具体模板/父级 GUID），把那条原文+设备/Android 版本/完整日志回传，据此继续定位，不宣称已修复。
8. 若船已平放：继续蒸汽船功能实测（搜索/拿取、放置、上船、驾驶、下船、存档重载），全部通过后才可把“蒸汽船朝向修复”行从“Android 待实测”改为“已确认”。若仍竖着或躺倒方向不对（如侧翻/上下颠倒），记录实际朝向并把 `src/SCIEPort/Assets/Models/SteamBoat.dae` 与 `modinfo.json` 版本回传，据旋转方向继续调整，不提前宣称已修复。

只有以上证据完成后，才能把相应运行项目从“待验证”改为“已确认”。
