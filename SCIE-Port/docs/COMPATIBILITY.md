# SCIE-Port 兼容性记录

目标：Survivalcraft NET 2.4 `Net_x25.01.17`，Android 为最终平台。

本表只使用“已确认 / 待验证 / 不支持”。编译和 Windows 程序集取证不能替代游戏运行、
Android 或多人测试。

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| Windows 目标程序集 | 已确认 | `Survivalcraft.exe` 2.4.0.0；固定哈希见 `API-DIFF.md` |
| 目标框架 | 已确认 | `.NETFramework,Version=v4.7.2` |
| SCNET ModLoader 与 CSV | 已确认 | 对真实程序集编译通过；运行加载待验证 |
| 物品 ID 995–1000 | 已确认 | 反射扫描目标原生常量未占用；其他 Mod 冲突仍待运行检查 |
| Kelly 植物 ID 903–916 | 已确认 | `Test-Environment.ps1` 对 x25.01.17 的 260 个原生方块常量完成反射扫描，目标区间未占用；其他 Mod 冲突仍需安装时检查 |
| XDB 合并格式 | 已确认（机理） | `ModsManager.CombineDataBase` 按 `LocalName + Guid` 匹配文件夹后再合并内容。**真正的判别项是文件夹 GUID 是否等于游戏原版值**：原版文件夹（Entities `5fcdf35f…`、Gameplay `d3d4b692…`、BlockBehaviors `00c97f0f…`）若使用自定义 GUID，合并不会并入原版文件夹，而是追加“同名不同 GUID”的游离 `<Folder>`（0.3.1 即此）。根节点用 Mod 容器（`<SCIEPortWatercraft>`，0.3.2 采用）还是 `<DatabaseObjects>`（正常运行的 Builder Assistant 采用）本身都可用——“根节点导致嵌套 `DatabaseObjects` 才报错”属早期误诊，已推翻。`scripts/Test-Database.ps1` 现强制校验该规则。**已用真实原版 `Assets/Database.xml`（解自 `Content.scpak`）+ 游戏真实 `DatabaseManager.LoadDataBaseFromXml` 端到端复现**：0.3.1 的确切首异常是 `Another database object with name "Entities" is already nested in database object "Root".`（`DatabaseObject.set_NestingParent`），0.3.2 与阳性对照 Builder Assistant 均加载成功。 |
| Boat/Mount/Rider/SpawnCreature ABI | 已确认 | `Test-Environment.ps1` 逐项反射签名通过 |
| 蒸汽船本地推进代码 | 已确认 | 仅 `WorkType.Local`，Release 0 warning/0 error |
| Windows 游戏加载与驾驶 | 待验证 | 尚未启动目标游戏进行生成、骑乘与存档测试 |
| Android 7723 数据库加载 | 已确认 | 用户确认 0.3.1 已修复 `Database not loaded`；渠道程序集哈希仍待取得 |
| 多人蒸汽船推进 | 不支持 | 尚未实现服务器权威驾驶输入和纠偏 |
| SCIE 燃料/发动机库存 | 不支持 | 0.5.0 未实现，不能以占位代码宣称兼容 |
| 世界加载（进入世界） | 已确认（桌面真实反序列化 + 手机实测） | `ModsManager.CombineDataBase` 按原版文件夹 GUID 合并；用真实原版 `Assets/Database.xml` + 真实 `DatabaseManager.LoadDataBaseFromXml` 端到端证明修复后无 rogue `Entities`，且手机反馈“经过测试可以了”，能进世界不再报 `Database not loaded`。 |
| SCIE 蒸汽船纹理 | 待验证 | 64×64 PNG，上游授权文件哈希已确认；游戏渲染待验证 |
| SCIE 蒸汽船模型朝向（Y_UP） | 已确认（文件/打包级）/ Android 待实测 | SCNET 的 `SimpleModel` 按游戏约定直接读原始顶点坐标，Y=上、Z=前，故 `.dae` 必须 `up_axis=Y_UP`。上游授权 `SteamBoat.dae` 是 Blender 导出的 `Z_UP`（高度放在 Z），进世界后船竖直立着；对照能正常横放的 Builder Assistant `Speedboat.dae` 是手写 `Y_UP`。0.3.3 改为打包**项目内派生的修正副本** `src/SCIEPort/Assets/Models/SteamBoat.dae`：对 positions(98 顶点) 与 normals(135) 烘焙绕 X −90° 旋转 `(x,y,z)->(x,z,-y)`（保持右手系），`up_axis` 改 `Y_UP`，`Cylinder` 节点与顶点数不变；**上游源文件保持不动**（仅作授权溯源）。`Build-NetMod.ps1` 打包级强制校验 `up_axis=Y_UP`+294 float。手机里船是否真正平放仍须实机确认，不提前宣称已修复。 |
| 0.5.0 飞艇朝向与骑乘缺陷 | 已确认 | 用户在 Android 实测飞艇竖直且无法骑乘；源模型为 Z_UP，同时 9 格宽碰撞体使驾驶座超出原生 2.5 格骑乘搜索范围 |
| 0.5.1 飞艇朝向与座位修复 | 已确认（文件/编译级） | 派生模型为 Y_UP、926 顶点，底面归零；碰撞体改为 `3,2,6`、座位 `0,1,-0.8`、侧向下船 `1.75,0.5,0`；Android 复测待验证 |
| SCIE 铁轨、火车和车厢编译/资源 | 已确认 | 0.5.1 的 RailBlock、形状更新、两实体、模型节点和纹理通过 Release 与包检查；火车已由 Z_UP 转为 Y_UP |
| 其他载具坐标轴 | 已确认（文件级） | 蒸汽船 Y_UP；火车 0.5.1 Y_UP；车厢源模型本身 Y_UP。三者实际 Android 姿态仍待验证 |
| SCIE 铁轨和轨道载具运行 | 待验证 | 未在 Windows/Android 验证放置、弯坡轨、生成、骑乘、驾驶和存档 |
| 车厢自动编组和货箱 | 不支持 | 旧实现依赖延迟父体复制及旧 GUI/库存链；0.5.0 仅提供独立试乘 |
| 轨道载具多人同步 | 不支持 | 自定义轨道物理仅在 `WorkType.Local` 执行 |
| Kelly 植物代码与资源 | 已确认（编译/静态） | 0.6.0 的 14 个方块类、4 个行为模板、原图集与果实模型已迁入 `SCIE-Port`；net472 对目标程序集编译通过，资源哈希与授权来源记录一致 |
| Kelly 创造栏原名 | 已确认（静态） | CSV 严格为野莓植株、野莓、香蒲、蒲菜、云莓植株、云莓、野生蘑菇簇、野生蘑菇、毒蝇伞簇、毒蝇伞、大豆、黄豆、仙人掌果、橡子；构建脚本按顺序哈希并拒绝中英文括号 |
| Kelly XDB 合并 | 已确认（静态） | `KellyPlants.xdb` 复用原版 `BlockBehaviors` GUID，4 个模板与 4 个成员继承均由 `Test-Database.ps1` 解析通过 |
| Kelly Android 资源加载 | 已确认（缺陷） | 用户 Android 日志确认 0.7.0 在创造栏绘制 `KellyFruitBlock` 时无法将仅打包的 `KellyExpansion.webp` 解析为 `Texture2D`，导致退出世界 |
| Kelly PNG 修复 | 已确认（文件/打包级） | 0.7.1 将同一 512×512 RGBA 图集无缩放转为 PNG，运行包只含 `KellyExpansion.png`；Android 修复结果待验证 |
| Kelly 其余运行行为 | 待验证 | `.dae`、创造栏完整显示、播种/成长、生态生成、雨后蘑菇、存读档和性能仍需 Android 实测 |
| 飞艇世界贴图 | 已确认（机理/修复） | 0.7.1 中 `AirshipDrive.Load` 先写运行时白贴图、随后 `SimpleModel.Load` 以空 XDB 值覆盖为 null；0.7.2 改为 XDB 明确引用包内 `Textures/Airship.png` |
| 载具世界黑色保底 | 已确认（编译/静态） | 蒸汽船、飞艇、火车和车厢的实体模型均设白色漫反射与低强度自发光，避免世界地形采样光接近 0 时渲染为纯黑；Android 实机待验证 |
| 载具资源引用守卫 | 已确认（打包级） | 构建检查 Watercraft XDB 的每个自有模型/贴图都在包内；仅车厢 `Textures/Creatures/Jaguar` 明确为 x25 原版资源；拒绝任意非 PNG 的 Mod 贴图 |
| ATV 物品与 ID 923 | 已确认 | `Test-Environment.ps1` 对固定 x25.01.17 的 260 个原版方块常量完成扫描；923 未占用且与本工程 903–922、995–1000 无内部冲突。其他 Mod 的 ID 冲突仍待安装时检查 |
| ATV 实体/XDB 合并 | 已确认 | `SCIEPortATV`、单座 Mount、Body、Spawn、Damage 和 `SCIEPortATVDrive` 通过 XDB GUID/继承完整性校验；`AutoDespawn=False` 依赖原生实体保存链 |
| ATV 地面物理 | 已确认 | 仅在 `WorkType.Local`、原生 body 接地且未浸水时读取触屏 Move；`ComponentBody` 负责碰撞、重力和 `MaxSmoothRiseHeight=0.35` 平滑上台阶。真实手感/坡度/碰撞待验证 |
| ATV 模型与贴图 | 已确认 | 项目创建的 `ATV.dae` 为 Y_UP，含车身与四个轮节点；`ATV.png` 是项目内白色基础纹理副本，着色由模型组件完成。Android 渲染待验证 |
| ATV Windows/Android/联机运行 | 待验证 | 未启动 Windows 游戏或 Android 7723 客户端；本阶段明确不实现多人自定义输入同步、燃料和复杂悬挂 |
| SCIE 工业家具 ID 917–922 | 已确认 | x25.01.17 的 260 个原生方块常量反射扫描未占用；本工程内部无重复，其他 Mod 冲突仍待安装时检查 |
| SCIE 工业家具模型 | 已确认 | Gear、Wheel、GearMould、WheelMould、Piston 已从 Z_UP 转为 Y_UP，Cylinder 源文件本身为 Y_UP；节点名、position float 数和包内资源由构建脚本校验 |
| SCIE 工业家具代码 | 已确认 | 六件静态可放置方块、四向 data、碰撞箱、掉落路径对 x25.01.17 Release 编译通过后方可维持此状态；不含机器语义 |
| SCIE 工业家具 Windows/Android 运行 | 待验证 | 创造栏、模型比例/颜色、四向放置、碰撞、拆除掉落、保存重载尚未进游戏验证 |
| SCIE 工业家具机器/配方/库存功能 | 不支持 | 0.7.0 明确只移植静态家具外观，不伪造上游工业逻辑 |

## 当前包布局

```text
SCIEPort.dll
modinfo.json
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Blocks/KellyPlants.csv
Assets/Blocks/IndustrialFurniture.csv
Assets/Blocks/LandVehicles.csv
Assets/Database/Watercraft.xdb
Assets/Database/KellyPlants.xdb
Assets/Database/LandVehicles.xdb
Assets/Models/Airship.dae
Assets/Models/ATV.dae
Assets/Models/KellyFruits.dae
Assets/Models/Gear.dae
Assets/Models/GearMould.dae
Assets/Models/Piston.dae
Assets/Models/Minecart.dae
Assets/Models/SteamBoat.dae
Assets/Models/Train.dae
Assets/Models/Wheel.dae
Assets/Models/WheelMould.dae
Assets/Models/Cylinder.dae
Assets/Textures/SteamBoat.png
Assets/Textures/Train.png
Assets/Textures/ATV.png
Assets/Textures/KellyExpansion.png
THIRD-PARTY-NOTICES.md
```

兼容性结论发生变化时，必须同时更新 `TEST-RESULTS.md` 中的测试证据。
