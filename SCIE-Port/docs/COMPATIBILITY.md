# SCIE-Port 兼容性记录

## 0.15.0 吞噬之眼 / Eye of Oblivion V1（2026-10-28）

分支 `port/scnet-x25.01.17`；新增 ID 950，与全知之眼 949 分离。新增真实 UpdateInput Hook
将该物品普通 Hit/Dig 转为 Interact 确认流程，消除原生挖掘/攻击旁路。复用既有 GuiUpdate
注册，按 Project 管理确认窗口/队列/事务锁，Dispose 取消任务并释放 UI 引用。
不变更原生类、网络包、已有实体 GUID 或部署物品；T-34 新增只读 CanRecover 状态，
禁止丢弃仍在飞行的炮弹。目标 EntityTemplate 名/GUID、五个组件类型、持久化状态、
骑乘/父子/所有权依赖、移动/射击、待重生登记均复查。

已确认：Windows 固定程序集/API/net472 编译；真实模型导入 1 bone / 27 meshes /
816 triangles；Y_UP/identity 根变换/不透明 128×16 PNG；75 项纯规则与故障注入测试；
真实 Project 移除/保存成员列表、背包满额/回滚、创造槽、Terrain DATA 往返、原生 JSON
回执往返的隔离测试。实际坦克完整组件、Android、触屏、正常退出重进和多人待验证。

权限来自原生 `SubsystemBedrockBlockBehavior.CheckIsInTerritoriy/AllowPlayerAction`；
V1 保守拒绝所有原生领地（含本人领地）。没有通用第三方区域保护 API；额外保护模组
需要独立适配，不能据此宣称任意保护模组兼容。块修改还遵守原生 TerrainChangeCell Hook，
实际读取结果确认删除后才返还物品。细节与边界见 [DEVOURING-EYE.md](DEVOURING-EYE.md)。

## 0.14.0 全知之眼 V0.1（2026-10-28）

目标分支 `port/scnet-x25.01.17`，基线 `a7e313a`；没有合并 main 或修改 Builder Assistant。
新增物品 Block ID 949（扫描目标原生 260 个常量及本分支全部物品，未占用）。
新增 `SubsystemOmniscientEye`，通过标准 `.xdb` / CSV 注册；新增现有 ABI
`GuiUpdate(ComponentGui)` Hook，沿用当前 Loader 的一次注册入口，无额外网络包。
使用真实 `CanvasWidget` / `LabelWidget` / `RectangleWidget` / `BevelledButtonWidget` /
`Dialog` / `DialogsManager`。玩家 UI 属于当前 Project 的子系统，Dispose 移除 HUD、
关闭对话框并释放玩家引用；不注册静态世界退出事件。坐标/区块每 0.2 秒、气候每 1 秒。
温湿度来自 `Terrain.GetTemperature/GetHumidity(x,z)` 的 0–15 柱气候等级，
不是摄氏度、百分比或玩家体温；高度为世界 Y。未加载区块显示未就绪。

已确认：固定 Windows 程序集指纹/ABI，net472 Release；真实 Engine 模型导入
1 bone / 25 meshes / 784 triangles；Y_UP、identity 根变换、UV、不透明 128×16 PNG；
27 个 HUD 布局场景；现有规则套件、7 份 XDB、包资源守卫。
待验证：Windows 游戏内使用、Android 7723 冷启动/手持/触屏/帧率、多本地玩家与联机。
布局离线检查不能替代实机控件遮挡测试。清单见 [OMNISCIENT-EYE.md](OMNISCIENT-EYE.md)。

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
| 双人快艇推进代码 | 已确认（编译/规则） | 不再挂原生 `ComponentBoat`，避免乘客覆盖驾驶输入；Local 或 Server 主机玩家为驾驶员时才写车体速度，`ComponentBody` 保留浮力、阻力、碰撞和持久化。Windows/Android 运行待验证 |
| Windows 游戏加载与驾驶 | 待验证 | 尚未启动目标游戏进行生成、骑乘与存档测试 |
| Android 7723 数据库加载 | 已确认 | 用户确认 0.3.1 已修复 `Database not loaded`；渠道程序集哈希仍待取得 |
| 双人快艇联机 | 待验证/部分不支持 | 主机驾驶员 + 普通客户端乘客为实机目标；Client 只接收座位分配且不写车辆速度。普通客户端作为驾驶员尚不支持，缺少服务器输入/预测/纠偏协议 |
| SCIE 燃料/发动机库存 | 不支持 | 0.5.0 未实现，不能以占位代码宣称兼容 |
| 世界加载（进入世界） | 已确认（桌面真实反序列化 + 手机实测） | `ModsManager.CombineDataBase` 按原版文件夹 GUID 合并；用真实原版 `Assets/Database.xml` + 真实 `DatabaseManager.LoadDataBaseFromXml` 端到端证明修复后无 rogue `Entities`，且手机反馈“经过测试可以了”，能进世界不再报 `Database not loaded`。 |
| SCIE 蒸汽船纹理 | 待验证 | 64×64 PNG，上游授权文件哈希已确认；游戏渲染待验证 |
| SCIE 蒸汽船模型朝向（Y_UP） | 已确认（文件/打包级）/ Android 待实测 | SCNET 的 `SimpleModel` 按游戏约定直接读原始顶点坐标，Y=上、Z=前，故 `.dae` 必须 `up_axis=Y_UP`。上游授权 `SteamBoat.dae` 是 Blender 导出的 `Z_UP`（高度放在 Z），进世界后船竖直立着；对照能正常横放的 Builder Assistant `Speedboat.dae` 是手写 `Y_UP`。0.3.3 改为打包**项目内派生的修正副本** `src/SCIEPort/Assets/Models/SteamBoat.dae`：对 positions(98 顶点) 与 normals(135) 烘焙绕 X −90° 旋转 `(x,y,z)->(x,z,-y)`（保持右手系），`up_axis` 改 `Y_UP`，`Cylinder` 节点与顶点数不变；**上游源文件保持不动**（仅作授权溯源）。`Build-NetMod.ps1` 打包级强制校验 `up_axis=Y_UP`+294 float。手机里船是否真正平放仍须实机确认，不提前宣称已修复。 |
| 双人快艇尺寸与座位 | 已确认（静态/目标导入） | 授权模型原比例边界约 X±1.058、Y 0..1.694、Z -2.353..2.743；Body 为 2.3×1.75×5.6。驾驶/乘客偏移为 (-0.42,1,-0.45)/(0.42,1,-0.45)，侧向下船点 X=1.65。目标 Engine 导入必须为 1 bone/1 mesh；Android 乘坐观感待验证 |
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
| ATV 实体/XDB 合并 | 已确认 | `SCIEPortATV` 保持实体名、原 GUID、唯一原生 Mount、Body、Spawn、Damage 和 Drive，并新增座位管理组件；`AutoDespawn=False` 继续依赖原生实体保存链 |
| ATV 地面物理 | 已确认 | v0.3 不改变 v0.2 驱动规则；Local 或 Server 主机玩家驾驶时读取触屏 Move，普通 Client 不改车辆速度；`ComponentBody` 继续负责碰撞、重力和 1.05 格 smooth rise |
| ATV 模型与贴图 | 已确认（静态/目标导入） | 项目创建的 `ATV.dae` 已改为 12 部件双座越野摩托，Y_UP、单 identity 骨骼、12 meshes；32×8 四色不透明 `ATV.png` 为项目脚本生成。Android 新外观待验证 |
| ATV Windows/Android/联机运行 | 待验证 | 旧 0.8.3 模型显示结论不自动覆盖 v0.3 新模型。Windows 未运行；Android 单人、保存重载、主机驾驶+客户端乘客、独立观察及上下车均待验证；普通客户端驾驶不支持 |
| ATV v0.2 模型/贴图重建 | 已确认 | 九个独立 Y_UP geometry，identity 矩阵、法线、UV、12 triangles/part；32×8 PNG 为四种不透明颜色；可见范围由构建脚本验证完全落入 `1.7,1.15,2.1` Body |
| ATV v0.2.1 Android 世界模型空白 | 已确认（缺陷/机理） | 用户在 Android 7723 确认 0.8.2 物品栏图标可见、生成实体无模型；目标 `ModLoader.GetMaxInstancesCount()` 返回 7，而旧 DAE 经目标 `Engine.Media.ModelData.Load` 导入为 10 bones/9 meshes。物品栏将网格烘焙进 `BlockMesh`，世界实体则把 10 个骨骼变换交给实例模型着色器，因此两条路径表现不同 |
| ATV v0.2.1 单骨骼修复 | 已确认 | 0.8.3 保留九个独立 geometry，但都挂到单个 identity `ATV` 场景节点；目标 Engine 实际导入结果为 1 bone/9 meshes，低于七骨骼限制。物品代码改为枚举全部 meshes；2026-10-20 用户在 Android 7723 确认物品栏与世界模型显示通过。驾驶、越障、保存和多人不继承此结论 |
| ATV v0.2 阻力修复 | 已确认 | 真实 `ComponentBody.Update` 的 `6*dt` 接地阻力已反编译确认；驾驶时禁用该项并使用帧率无关的纵向/横向控制，无人时恢复。Android 手感待验证 |
| ATV v0.2 一格越障 | 已确认 | ABI/代码级确认使用原生三轴碰撞和连续 `4.5*dt` smooth rise，`MaxSmoothRiseHeight=1.05`；不写 Position/地形且不抬高实体碰撞。Android 一格台阶与净空仍待验证 |
| ATV v0.2 诊断日志 | 已确认 | 仅本地单人、有人驾驶时每 5 秒输出速度、油门、地形/实体支撑、离地时间、原生阻力开关和状态；Android 日志格式待验证 |
| ATV v0.3 原生双 Mount 可行性 | 不支持 | `ComponentMountPackage` 只传目标实体 ID，接收端取首个 Mount；同实体第二个 Mount 无法寻址。多层乘客锚点又会因原生碰撞只排除直接父子 Body 而让乘客碰撞祖父车体 |
| ATV v0.3 座位关联 ABI | 已确认 | 两名玩家均直接挂 ATV Body；座位管理器由 Local/Server 最终分配，服务端以自定义 `IPackage` ID 240 下发离散角色/偏移。目标原生包占 ID 0–38，240 未占用；其它 Mod 冲突仍待安装时检查 |
| ATV v0.3 联机权威 | 待验证/部分不支持 | 主机本地驾驶员 + 普通客户端乘客为本轮实机目标；Client 只发原生上/下车请求并应用服务器座位事件，不改车辆速度。普通客户端作为驾驶员因原生父车 BodyUpdate 权威路径而不支持 |
| 双人越野车 ID 925 | 已确认 | 固定 x25.01.17 的原生方块常量反射扫描未占用 925，且与本工程其它 ID 无内部冲突；其它 Mod 冲突仍需安装时检查 |
| 双人越野车模型与尺寸 | 已确认（静态/目标导入） | 项目原创 Y_UP 模型为 1 bone/24 meshes，范围 X±1.15、Y 0.08..1.48、Z -1.78..1.68，完整落入 2.4×1.55×4.0 Body；40×8 五色 PNG 全不透明。Android 世界显示待验证 |
| 双人越野车驾驶与双座 | 已确认（代码/规则） | 独立地面驱动，不使用船物理；原生 Body 负责重力、碰撞与 1.05 格 smooth rise。驾驶/乘客为左右固定座位，只有驾驶员输入进入 Drive；Windows/Android 运行待验证 |
| 双人越野车联机 | 待验证/部分不支持 | 目标为主机驾驶员 + 普通客户端乘客；普通 Client 不写车辆速度。普通客户端驾驶员仍不支持，原因与 ATV 相同 |
| SCIE 工业家具 ID 917–922 | 已确认 | x25.01.17 的 260 个原生方块常量反射扫描未占用；本工程内部无重复，其他 Mod 冲突仍待安装时检查 |
| SCIE 工业家具模型 | 已确认 | Gear、Wheel、GearMould、WheelMould、Piston 已从 Z_UP 转为 Y_UP，Cylinder 源文件本身为 Y_UP；节点名、position float 数和包内资源由构建脚本校验 |
| SCIE 工业家具代码 | 已确认 | 六件静态可放置方块、四向 data、碰撞箱、掉落路径对 x25.01.17 Release 编译通过后方可维持此状态；不含机器语义 |
| SCIE 工业家具 Windows/Android 运行 | 待验证 | 创造栏、模型比例/颜色、四向放置、碰撞、拆除掉落、保存重载尚未进游戏验证 |
| SCIE 工业家具机器/配方/库存功能 | 不支持 | 0.7.0 明确只移植静态家具外观，不伪造上游工业逻辑 |
| T-34 坦克 ID 924 | 已确认 | `Test-Environment.ps1` 对固定 x25.01.17 程序集扫描 260 个原生方块常量；924 未被原版占用，仍需安装时检查其他 Mod 冲突 |
| T-34 内部炮弹 ID 926 | 已确认 | 固定 x25.01.17 原生常量扫描未占用 926，且本工程无内部冲突；`TankShellBlock.GetCreativeValues()` 返回空，不在创造栏显示。其他 Mod 的 926 冲突仍需安装时检查 |
| T-34 实体/XDB/资源布局 | 已确认 | `Tanks.xdb` 复用原版文件夹 GUID；静态构建守卫检查实体组件、模型、纹理、1.05 格台阶限制与三处模型节点；v0.12.2 明确禁止重新加入 `Damage` 组件 |
| T-34 单人地面驱动代码 | 已确认 | 仅 `WorkType.Local`；真实程序集反射检查 Mount/Rider/Input/Body/CreatureModel 所需表面，且 Release 编译通过后方可维持该状态 |
| T-34 Windows 游戏运行 | 待验证 | 未启动游戏；生成、骑乘、触屏/键鼠驱动、碰撞、坡度、下车、视角与存档恢复均待验证 |
| T-34 Android 7723 基础运行 | 已确认 | 2026-10-20 用户确认坦克测试通过；模型显示、生成、骑乘与基础驾驶可继承该实测结论。用户同时确认旧版不能开炮、不能跨越完整一格且占地偏小；这些缺陷不属于通过项 |
| T-34 多人同步 | 不支持 | 第一阶段明确只在 `WorkType.Local` 驱动，未实现服务器权威输入、状态复制或纠偏 |

| T-34 v0.2 模型/贴图 | 已确认（静态/导入） | 项目自有 Y_UP 资源由生成脚本重建为 20 网格；目标 `Engine.Media.ModelData.Load` 实际导入 `TankHull > TankTurret > TankGun` 共 3 骨骼/20 网格，低于七骨骼预算；64×8 PNG 含 8 种不透明颜色。Android 世界渲染仍待验证 |
| T-34 v0.2 地面驾驶 | 已确认（代码/测试） | 驾驶时关闭原生地面阻力以避免双重阻力；统一处理推进、倒车、制动、滑行、侧滑和转向。仅地形接触允许推进，离地保留惯性，浸水/实体支撑不施加推进。Android 手感和碰撞仍待验证 |
| T-34 v0.2 碰撞范围 | 已确认（参数/模型范围） | `BoxSize=2.6,1.55,4.4` 覆盖当前车身和炮管坐标范围，`MaxSmoothRiseHeight=0.3`；仍是单一原生长方体 Body，不宣称复合履带/炮管碰撞，狭窄地形待实机验证 |
| T-34 v0.12.2 主炮 | 已确认（源码/固定内容包/编译），Android 待验证 | 炮弹继续从 AABB 外生成、创建后恢复 owner，并保留 1.5 格引信；发射载具安全包络由 8 扩至 12 格。爆炸压力 2160 来自固定 `Content.scpak` 中 ID 107 `LargeGunpowderKegBlock.DefaultExplosionPressure`，调用原生 `AddExplosion`，可进入原生方块破坏、粒子和声音链。实际破坏范围、驾驶员安全和性能须 Android 可丢弃世界复测。 |
| T-34 v0.12.2 炮塔骨骼 | 已确认（源码/规则/编译），Android 待验证 | 活动摄像机每帧给出目标角，`TankTurret` 以 1.2 rad/s、`TankGun` 以 0.65 rad/s 逐步追随；点击射线只作为开火触发，不再覆盖瞄准方向。视觉骨骼和炮弹共同读取当前 `TurretYaw/GunPitch`，避免“点哪里瞬间打哪里”。 |
| T-34 v0.12.2 无限耐久 | 已确认（XDB/构建守卫），Android 待验证 | 坦克实体移除原生 `ComponentDamage`；狼攻击和原生爆炸后处理找不到可扣减的载具耐久，构建脚本会拒绝任何重新加入 Damage 的坦克模板。实体销毁、掉出世界及其它 Mod 强制删实体不属于耐久保证。 |
| T-34 v0.12.3 炮弹外观 | 已确认（源码/编译），Android 待验证 | 专用 `TankShellBlock` 复用目标运行时内置 `Models/Snowball` 网格，不打包游戏资源；以纯黑顶点色和 3.5 倍原生投射物绘制比例替代旧 `BulletBlock` 的 0.3 平面贴片。Android 实际尺寸、光照可见性待验证。 |
| T-34 v0.12.3 首次命中爆炸 | 已确认（ABI/XDB/构建守卫），Android 待验证 | `SCIEPortTankShellBehavior.OnHitAsProjectile` 在原生反弹计算前同步通知所属坦克，按命中方块中心立即调用既有安全引信/12 格包络及爆炸链，移除状态后标记弹丸销毁；CSV 同时固定 `DisintegratesOnHit=TRUE`、`IsStickable=FALSE`、`ProjectileDamping=1`，避免最终停止后再次爆炸。 |
| T-34 v0.3 尺寸与一格越障 | 已确认（静态/编译），Android 待验证 | 模型坐标整体放大 35%，Body 改为 `3.6,2.1,5.9`，座位与下车偏移同步调整；`MaxSmoothRiseHeight=1.05` 使用原生连续抬升与碰撞/净空检查，不写 Position、不改地形 |

## 当前包布局

```text
SCIEPort.dll
modinfo.json
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Blocks/KellyPlants.csv
Assets/Blocks/IndustrialFurniture.csv
Assets/Blocks/LandVehicles.csv
Assets/Blocks/OffroadVehicles.csv
Assets/Blocks/Tanks.csv
Assets/Database/Watercraft.xdb
Assets/Database/KellyPlants.xdb
Assets/Database/LandVehicles.xdb
Assets/Database/OffroadVehicles.xdb
Assets/Database/Tanks.xdb
Assets/Models/Airship.dae
Assets/Models/ATV.dae
Assets/Models/OffroadCar.dae
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
Assets/Models/T34Tank.dae
Assets/Textures/SteamBoat.png
Assets/Textures/Train.png
Assets/Textures/ATV.png
Assets/Textures/OffroadCar.png
Assets/Textures/KellyExpansion.png
Assets/Textures/T34Tank.png
THIRD-PARTY-NOTICES.md
```

## 0.13.0 狐狸生物验证

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| glTF/GLB 直接加载 | 不支持 | 固定目标 `SurvivalCraftModEntity` 仅注册 DAE、JSON、OBJ；参考项目的 `AnimationController` 也不在目标程序集。 |
| 狐狸 XDB/原版 AI 继承 | 已确认（静态） | `SCIEPortFox` 继承固定 `Content.scpak` 中 Wolf GUID `49a9845c-3810-4092-ba53-df213d10d563`，真实 Entities/BlockBehaviors GUID 与参数由构建检查固定。 |
| 狐狸 DAE/PNG | 已确认（离线导入） | 项目自制 Y_UP 资源；目标 `Engine.Media.ModelData.Load` 导入为 6 bones/13 meshes，骨骼名与 `ComponentFourLeggedModel.SetModel` 完全一致。 |
| 狐狸生成物品 ID 927 | 已确认 | 固定程序集 260 个原生 Block.Index 扫描未占用；其它 Mod 的运行时冲突仍需安装时检查。 |
| 自然生成 | 不支持 | 第一阶段没有注册 `SubsystemCreatureSpawn.CreatureType` 或地形生成 Hook。 |
| Windows 游戏运行 | 待验证 | 按要求未启动游戏；加载、生成、AI、动画、死亡和存档恢复未在 Windows 运行。 |
| Android 7723 | 待验证 | 图标、模型、UV、动画、碰撞、生成与退出重进均需真机新世界验证。 |
| 多人联机 | 不支持 | 普通 Client 被禁止本地创建狐狸；尚无客户端请求、服务器确认与实体复制专项协议。 |

兼容性结论发生变化时，必须同时更新 `TEST-RESULTS.md` 中的测试证据。


## 2026-10-29 阿比斯 V0.1 / port 0.16.0

指定port分支新增原创立体战镰952（自动扫描生成分配）、三连斩/蓄力重斩、四槽锁定UI。949/950及主线951保持不变，无地形/天气修改。真实手持动画API、编译与离线检查见SCIE-Port/docs/ABYSS-REAPER.md及测试记录；Android和联机实测待验证，普通客户端战斗不支持。
