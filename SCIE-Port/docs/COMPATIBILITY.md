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

## 方块图集 slot→像素映射取证（V1 作物/植物自绘图集前置）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 方块面贴图网格（block face texture slot） | 已确认（原生资源实测，非自证） | 从目标 `Content.scpak` 用 `Game.SurvivalCraftModEntity.GetDecipherStream`（static, NonPublic）解出 ZIP，取 **游戏自带** `Assets/Textures/Blocks.png`：反射 PNG 头实测 `512×512`、bitDepth=8、colorType=6（RGBA）。CSV 第 74 列 `DefaultTextureSlot` 是纯整数（原生实测：Snow=66、Ice=65、Sandstone=176、Water=189、Magma=141）。对同一张**原生**图集按不同“每行 tile 数”假设取样各格平均 RGB：仅 **每行 16 格 / 每格 32×32 像素**（即 `col=slot%16, row=⌊slot/16⌋, 像素矩形左上=(col*32,row*32)`，容量 256 槽）假设下，五个已知方块都落在语义正确颜色——Snow(64,128)=250,252,252 近白、Ice(32,128)=217,227,244 冷蓝、Sandstone(0,352)=196,182,138 黄褐、Magma(416,256)=174,57,5 橙红、Water(416,352)=185,185,185 中性偏蓝；每行 8 格（64px）全部读到 (0,0,0) 越界空白，每行 32 格（16px）读到灰阶错色。结论与自绘 PNG 无关，是对游戏资产的独立验证。 |
| 命名贴图（模型/部件/GUI 图集）机制 | 已确认（机理区分） | 同一 `Content.scpak` 内 `Assets/Atlases/Atlas.txt`（驱动 `Game.TextureAtlasManager.LoadTextureAtlas(texture, atlasDefinition, prefix)`）是**按名字显式列出每个子图矩形**（`Name x y w h ...`，尺寸 16/24/32/40/64/112/224 不等、位置任意）。因此 `Models/*`、实体 `TextureOverride` 与 GUI 走“具名矩形”通道，与方块面的“固定 16×16 网格整数槽”通道**不同**，两者不可混用；自绘作物方块必须按方块面网格约定排布，不能借用 Atlas.txt 具名机制。 |
| 自绘作物图集（Crops.png）实际渲染 | 待验证 | 上述网格与槽→像素公式已在原生资产上确认；但据此新建的 512×512 RGBA 自绘图集能否被加载器解析并在 Android 7723 正确采样（尤其 alpha 测试、放大过滤、创造栏图标）须真机新世界验证，不提前宣称已修复。 |

## 0.13.0 兔子生物基础验证（commit cb0f309，手动生成器阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 兔子 XDB/原生 AI 继承 | 已确认（静态） | `SCIEPortRabbit` 继承固定 `Content.scpak` 中原版 Alpaca 食草动物模板 GUID `8e62c6e4-...`（LandAnimal 后代），复用原生四足步态 + 吃拾取物/群居/逃跑 AI，思路与狐狸复用 Wolf 的追捕链一致。`Test-Database.ps1` 已把该 GUID 加入 `$KnownVanillaParents` 白名单；真实 Entities/BlockBehaviors GUID 与 7 个覆盖组件由 `Build-NetMod.ps1` 校验。 |
| 兔子 DAE/PNG | 已确认（离线导入） | 项目自制 Y_UP `Rabbit.dae`（6 骨骼 Body/Head/Leg1-4 + 12 立方网格，每网格 24 顶点/12 三角）与 32×8 四色不透明 `Rabbit.png`，由 `New-RabbitAssets.ps1` 生成；`Build-NetMod.ps1` 用目标 `Engine.Media.ModelData.Load` 实测导入 6 bones/12 meshes 并逐骨骼名校验，PNG 断言恰好 4 种 A=255 颜色。 |
| 兔子生成物品 ID 928 | 已确认 | 固定 x25.01.17 的 260 个原生 `Block.Index` 反射扫描未占用 928；`Test-Environment.ps1` 保留区间扩至 903–928 且无原生冲突；其它 Mod 运行时冲突仍需安装时检查。 |
| 兔子权限闸（联机） | 已确认（代码/规则） | `SubsystemRabbitSpawnBlockBehavior.OnUse` 在 `CommonLib.WorkType == WorkType.Client` 时直接返回不生成，避免普通客户端本地制造未同步实体。真实主机/客户端同步效果待联机实测。 |
| 自然生成 | 不支持 | 本阶段仅创造栏手动生成器（`Spawner Eggs` 类），未注册 `SubsystemCreatureSpawn.CreatureType` 或地形生成 Hook。 |
| 生态互动（被捕食/喂食） | 不支持 | 本阶段不接线狐狸追捕兔子、兔子吃作物等互动，留待基础加载存读档实机验证后再单独成阶段。 |
| Windows 游戏运行 | 待验证 | 按要求未启动游戏；加载、生成、AI、动画、死亡与存读档未在 Windows 运行。 |
| Android 7723 | 待验证 | 图标、模型、UV、动画、碰撞、生成与退出重进均需真机新世界验证。 |
| 离线逻辑测试 | 已确认（净8 无游戏依赖） | `Features/Ecology/EcologyRules.cs`（种群上限、每块生成预算、确定性 chunk 种子、生物群系适宜度、权限闸、捕食资格、存读档计数钳位）配套 `tests/SCIEPort.Ecology.Tests` 25 条断言通过，不引用游戏程序集。 |
兼容性结论发生变化时，必须同时更新 `TEST-RESULTS.md` 中的测试证据。

## 0.14.0 玉米作物基础验证（自绘图集 + 多阶段生长，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 玉米三块注册（Stalk 929 / Seed 930 / Ear 931） | 已确认（静态 ABI） | 三个公开 `Block` 子类各带 `public const int Index`（`CornStalkBlock:CrossBlock`、`CornSeedBlock:FlatBlock`、`CornEarBlock:FlatBlock`），遵循 SCNET“类名即注册、CSV `Class Name` 列关联元数据”约定。`Test-Environment.ps1` 保留区间扩至 903–931，反射扫描 x25.01.17 全部原生 `Block.Index` 无占用冲突。 |
| Crops.csv 元数据 | 已确认（打包校验） | 表头 + 3 行，UTF-8 CRLF、每行 79 列（由脚本读取原生 Kelly CSV 模板生成，非手抄）。`Stalk` 复用 SoybeanBlock 模板、`Seed/Ear` 复用 SoybeanFruitBlock 模板；`Class Name`/`DefaultDisplayName`（玉米/玉米种子/玉米棒）/`DefaultCategory=Crops`/`DisplayOrder=330/331/332`/`DefaultTextureSlot=0/8/9`/`IsPlaceable=TRUE` 均由 `Build-NetMod.ps1` 逐列断言。 |
| Crops.xdb 轮询行为挂载 | 已确认（合并/结构） | 根 `DatabaseObjects`；`Folder BlockBehaviors` 用真实原版 GUID `00c97f0f-731e-481c-9909-eae9cc5ee940`；`SubsystemTemplate SCIEPortCornBehavior` 继承固定父 `fefb9590-4972-4893-b02a-76063611b745`（`SubsystemBlockBehavior` 基），`Parameter Class=SCIEPort.Features.Crops.SubsystemCornBlockBehavior`；`ProjectTemplate`（真实 GUID `85023bf8-1c90-4dd1-9442-e6c13691d078`）挂 `MemberSubsystemTemplate`。轮询生成走世界子系统 `HandledBlocks={929}`，非 CSV `Behaviors` 列。所有叶子 GUID 由 `randomUUID()` 生成，`Build-NetMod.ps1` 校验继承父与成员 GUID 一致。 |
| 生长规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | `Features/Crops/CropRules.cs`：尺寸=data&7（0..7 成熟阈 7）、`SetSize` 保留掩码外位（测 0x40）、最低光照 9、生长间隔 8 基值（水利 -2、氮肥 -2、低温≤4 +8、高温≥12 -1）、放置需顶面土壤、收获 1 棒 + 2 种、权限闸 `!isClient`。配套 `tests/SCIEPort.Crops.Tests` 26 条断言全部通过，不引用游戏程序集。 |
| 轮询行为接线 | 已确认（代码） | `SubsystemCornBlockBehavior.OnPoll` 在 `CommonLib.WorkType==WorkType.Client` 直接返回（联机仅权威端推进），校验 skylight≥9、size<7、下方为土壤，读 `SoilBlock.GetHydration`（反射实测返回 `Boolean`，直取）、`GetNitrogen`、季节温度与 `SubsystemWeather.GetTemperatureAdjustmentAtHeight`，`ChangeCell` 推进 size+1，成熟时消耗土壤氮 1；`OnNeighborBlockChanged` 土壤被移除则摧毁作物。真实主机/客户端推进与联机同步待实测。 |
| 自绘图集 Corn.png | 待验证 | 512×512 `Format32bppArgb`，按已确认的方块面网格公式排布 slot 0..9 于首行（`x=slot*32,y=0`），slot 0..7 茎秆逐阶段、8 种子、9 玉米棒，10/11 留空。`Build-NetMod.ps1` 断言尺寸/像素格式且 slot 0..9 存在不透明像素、10 与 15 全透明；独立 PNG 解码确认阶段 0..7 不透明像素单调增长（4→10→32→38→60→66→96→123）、种子 78、棒 246。但据网格公式自绘的图集能否被加载器在 Android 7723 正确采样（alpha 测试、放大过滤、创造栏图标、跨阶段贴图切换）须真机新世界验证。 |
| Windows 游戏运行 | 待验证 | 未启动游戏；种植、逐阶段生长、成熟收获、掉落到背包与存读档未在 Windows 运行。 |
| Android 7723 | 待验证 | 方块渲染、贴图、种植交互、生长轮询、收获掉落、退出重进均需真机新世界验证，不提前宣称已修复。 |

## 0.15.0 竹子作物基础验证（纵向逐段生长 + 建筑方块，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 竹子两块注册（Plant 932 / Block 933） | 已确认（静态 ABI） | `BambooPlantBlock:CrossBlock`（Index 932，逐段堆叠的植株）与 `BambooBlock:Block`（Index 933，实心建筑立方体），各带 `public const int Index`。`Test-Environment.ps1` 保留区间扩至 903–933，反射扫描 x25.01.17 全部原生 `Block.Index` 无占用冲突。`SoilBlock/SandBlock/DirtBlock` 均有公开静态 `Index`（反射实测）作基座判定。 |
| 实心立方体自绘图集渲染 | 已确认（API 取证）+ 待验证（真机） | 目标程序集实测：`BlockGeometryGenerator.GenerateCubeVertices(block,value,x,y,z,Color,TerrainGeometrySubset[])` 走方块自身 `GetFaceTextureSlot` 并允许按面传入自建图集的 `SubsetOpaque`；图标用 `BlocksManager.DrawCubeBlock(...,Texture2D)` 重载传入 `Textures/Bamboo`。据此 `BambooBlock` 六面采样自绘图集 slot 2。能否在 Android 7723 正确实心渲染待真机验证。 |
| 逐段纵向生长机制 | 已确认（代码/离线规则） | DATA bit0（`BaseFlagMask=1`）标记“栽下的基段”，仅基段驱动向上堆叠（一段一生长者，防失控）。`OnPoll`：基段脚下需土壤/沙/泥土，向上数连续竹段得高度，`CanExtend(height<8)` 且顶上一格为空气、其上光照≥8，按 `GetGrowthInterval`（基 3、水利 -1、低温≤4 +3、下限 1）用 `pollPass % interval` 节流后在顶端 `ChangeCell` 加一段（bit0 清）。`OnNeighborBlockChanged`：基段失基座销毁，非基段下方非竹销毁（支持逐段塌落）。 |
| 生长规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | `Features/Bamboo/BambooRules.cs`：`MaxHeight=8`、基段位往返/幂等、掩码外位保留（测 0x40 与基位共存）、上段判定、`CanExtend` 上下界（0 不可、cap 停、超 cap 停）、间隔（干暖 3/水利 2/低温 6/水寒 5）、放置需顶面有效基座、砍伐每段掉 1、权限闸 `!isClient`。配套 `tests/SCIEPort.Bamboo.Tests` 25 条断言全部通过，不引用游戏程序集。 |
| Bamboo.csv 元数据 | 已确认（打包校验） | 表头 + 2 行（植株/建材），UTF-8 CRLF、每行 79 列（由 node 读 Crops.csv 表头按列名改写生成，非手抄）。`Build-NetMod.ps1` 断言：`BambooPlantBlock` 属 Crops 类可放置且 `DefaultTextureSlot=0`；`BambooBlock` `IsPlaceable=TRUE`、`IsCollidable=TRUE`、`IsTransparent=FALSE`、`DefaultTextureSlot=2`。 |
| Bamboo.xdb 轮询行为挂载 | 已确认（合并/结构） | 根 `DatabaseObjects`；`Folder BlockBehaviors` 用真实原版 GUID `00c97f0f-...`；`SubsystemTemplate SCIEPortBambooBehavior` 继承固定父 `fefb9590-...`（`SubsystemBlockBehavior` 基），`Parameter Class=SCIEPort.Features.Bamboo.SubsystemBambooBlockBehavior`；`ProjectTemplate`（真实 GUID `85023bf8-...`）挂 `MemberSubsystemTemplate`。轮询走世界子系统 `HandledBlocks={932}`。叶子 GUID 由 `randomUUID()` 生成，`Build-NetMod.ps1` 校验继承父与成员 GUID 一致。 |
| 自绘图集 Bamboo.png | 待验证 | 512×512 `Format32bppArgb`，slot 0 基段十字面、slot 1 上段十字面、slot 2 建材立方面。`Build-NetMod.ps1` 断言尺寸/格式、slot 0..2 有不透明像素、slot 2 全 1024 像素不透明（实心面）、slot 5 全透明。实际加载/alpha 测试/放大过滤/创造栏图标待真机验证。 |
| Windows 游戏运行 | 待验证 | 本机未启动游戏；种植、逐段堆叠、砍伐塌落、建材放置与存读档均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、生长动画、塌落、建材实心碰撞与退出重进恢复均需真机新世界验证。 |
| 多人联机 | 待验证 | `OnPoll`/`OnNeighborBlockChanged` 已在 `WorkType.Client` 直接返回，仅权威端堆叠，但真实主机/客户端同步与一致性未实测。 |

## 0.16.0 亚麻作物基础验证（多阶段生长 + 纤维收获，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 亚麻两块注册（Plant 934 / Fiber 935） | 已确认（静态 ABI） | `FlaxPlantBlock:CrossBlock`（Index 934，0..7 阶段）与 `FlaxFiberBlock:FlatBlock`（Index 935，纤维成品），各带 `public const int Index`。`Test-Environment.ps1` 保留区间扩至 903–935，反射扫描 x25.01.17 全部原生 `Block.Index` 无占用冲突。 |
| 生长规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | `Features/Flax/FlaxRules.cs`：`MaxFlaxSize=7`、尺寸=data&7、`SetSize` 保掩码外位（测 0x40）、最低光照 8、生长间隔 7 基值（水利 -2、氮肥 -2、低温≤4 +6、高温≥12 -1）、放置需顶面土壤、收获 2 纤维 + 1 可重种种子、权限闸 `!isClient`。配套 `tests/SCIEPort.Flax.Tests` 29 条断言全部通过，不引用游戏程序集。 |
| 轮询行为接线 | 已确认（代码） | `SubsystemFlaxBlockBehavior:SubsystemPollableBlockBehavior`（`HandledBlocks={934}`）在 `WorkType.Client` 直接返回（联机仅权威端推进），校验 skylight≥8、size<7、下方土壤，读 `SoilBlock.GetHydration`（`Boolean` 直取）、`GetNitrogen`、季节温度与高度调整，`ChangeCell` 推进 size+1，成熟时消耗土壤氮 1；`OnNeighborBlockChanged` 土壤被移除则摧毁。真实主机/客户端推进与联机同步待实测。 |
| Flax.csv 元数据 | 已确认（打包校验） | 表头 + 2 行（植株/纤维），UTF-8 CRLF、每行 79 列（node 读 Crops.csv 表头按列名改写生成，植株复用玉米茎 CrossBlock 模板、纤维复用玉米种 FlatBlock 模板，非手抄）。`Build-NetMod.ps1` 断言两者属 Crops 类可放置且 `DefaultTextureSlot` 分别 0 / 8。 |
| Flax.xdb 轮询行为挂载 | 已确认（合并/结构） | 根 `DatabaseObjects`；`Folder BlockBehaviors` 用真实原版 GUID `00c97f0f-...`；`SubsystemTemplate SCIEPortFlaxBehavior` 继承固定父 `fefb9590-...`，`Parameter Class=SCIEPort.Features.Flax.SubsystemFlaxBlockBehavior`；`ProjectTemplate`（真实 GUID `85023bf8-...`）挂 `MemberSubsystemTemplate`。轮询走世界子系统非 CSV Behaviors 列。叶子 GUID 由 `randomUUID()` 生成，`Build-NetMod.ps1` 校验继承父与成员 GUID 一致。 |
| 自绘图集 Flax.png | 待验证 | 512×512 `Format32bppArgb`，slot 0..7 亚麻植株逐阶段（细茎随阶段增高、成熟期顶端蓝色亚麻花）、slot 8 纤维束，slot 12 留空。`Build-NetMod.ps1` 断言尺寸/格式、slot 0..8 有不透明像素、slot 12 全透明；生成器自检阶段 0..7 不透明像素单调增长（18→36→54→72→90→114→132→154）、纤维 271。据网格公式自绘的图集真机采样/alpha/图标待验证。 |
| Windows 游戏运行 | 待验证 | 本机未启动游戏；种植、逐阶段生长、成熟收获（纤维 + 可重种种子）、掉落与存读档均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、蓝花阶段、种植交互、收获与退出重进均需真机新世界验证，不提前宣称已修复。 |

## 0.17.0 自然生成 spawner 验证（狐/兔按种子散布，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 生成器接线（复用已证 Hook） | 已确认（代码） | 自然生成走已被 Kelly 植物散布验证的 `OnTerrainContentsGenerated(TerrainChunk)` 块生成钩子，未新增地形写入路径。`CreatureWorldSpawn` 静态世界侧类不注册独立 SubsystemTemplate/XDB/CSV/GUID：它由 `SubsystemFoxSpawnBlockBehavior.Load` 与 `SubsystemRabbitSpawnBlockBehavior.Load` 各调 `SetProject(Project)` 缓存 `SubsystemCreatureSpawn/SubsystemGameInfo/SubsystemTerrain`（二者已持有经验证的 `Project`），故零新增数据库面。加载器把植物散布与生物生成放在各自 `try/catch` 内、失败 `Log.Error` 记录不吞异常。 |
| 静态 ABI 取证 | 已确认（反射） | 本阶段先反射目标 x25.01.17 再动手，确认 `TerrainChunk.GetTemperatureFast(x,z)→Int32`、`GetHumidityFast(x,z)→Int32`、`chunk.Coords.X/Y`、`chunk.Origin.X/Y`（世界偏移，与 Kelly 的 `Origin.X+localX/Origin.Y+localZ` 映射一致）、`chunk.GetTopHeightFast/GetCellValueFast`、`SubsystemGameInfo.WorldSeed→Int32`、`SubsystemCreatureSpawn.SpawnCreature(string,Vector3,bool)`（狐/兔手动生成器已用）均存在；`Game.Random(seed).Int(min,max)` 沿用 Kelly 已验证用法。实体类型按狐/兔生成器一致写全限定 `GameEntitySystem.Entity`，`Random` 全限定 `Game.Random` 避免 Engine/Game 二义。 |
| 决策规则（离线纯逻辑） | 已确认（净8 无游戏依赖） | 一切可纯化的判定委托 `Features/Ecology/EcologyRules.cs`：狐狸基础几率 8 < 兔子 15（捕食者比猎物更稀有，避免新图被过度捕食）；`ChanceMet(roll0To99,chancePercent)` 对越界 roll（<0 或 >99）一律不命中，且该 roll 守卫置于 `chance>=100` 短路之前（本阶段实测发现原实现 `ChanceMet(-1,100)` 误判命中，已改序并新增断言）；`CanSpawnWithinBudget` 每块至多 2 次尝试；`ClampTrackedCount` 钳到物种上限；`IsFoxHabitat/IsRabbitHabitat` 按温度/湿度/自然地面/树冠覆盖。`tests/SCIEPort.Ecology.Tests` 37 条断言全部通过（含新增几率/预算 12 条）。 |
| 确定性种子 | 已确认（离线）+ 待验证（真机） | 掷骰序列由 `GetChunkSeed(WorldSeed, chunk.Coords.X, chunk.Coords.Y)` 派生，同世界同 chunk 恒定、重进不重排（离线确定性断言通过）。真机跨存档/重进是否一致待验证。 |
| 权限闸（联机） | 已确认（代码/规则） | `CanAuthoritativeWorldMutation(WorkType==Client)` 令普通客户端直接返回、绝不在本地制造未同步实体；仅单机 Local 与主机/服务端尝试生成。真实主机/客户端同步效果待联机实测。 |
| 每块预算与种群上限 | 已确认（规则）+ 已知缺口 | 单块生成至多 2 只（不整图扫描、不高帧写入），狐全局上限 3、兔 6。存活计数 `s_trackedFoxes/s_trackedRabbits` 仅在成功生成时自增并钳到上限。生物死亡回收（递减计数）未接线——这是如实声明的已知缺口：计数只增不减，最坏是过早停止生成（保守安全）而非过度繁殖。死亡事件接线留待后续阶段。 |
| 实体生成激活（生成期 SpawnCreature） | 待验证 | 静态 ABI 与全部纯规则已确认，但“在块生成回调内调用 `SpawnCreature` 造实体”能否在 Windows 与 Android 7723 真机稳定成功、不崩、不阻塞生成、不产生未同步实体，尚未观测；按要求不提前宣称已修复。生成失败经 `Log.Error` 记录（不吞异常）以留下真机证据。 |
| 生态互动（狐追捕兔） | 待验证（未接线） | `IsEligiblePredation(狐活/兔活/距离²/追捕半径)` 纯谓词已测；实际追捕仍委托原生 Wolf 衍生 AI，本阶段不接线，留作独立阶段。 |
| Windows 游戏运行 | 待验证 | 本机未启动游戏；自然生成时机、生成实体存活、存读档与卸载均未运行。 |
| Android 7723 | 待验证 | 生成实体在低端机的性能、上限表现、与原生生物群系的共存、退出重进恢复均需真机新世界验证。 |
| 多人联机 | 待验证 | 已拒普通客户端生成，但主机权威生成后向客户端的实体同步、以及房主/加入者一致性未实测。 |

## 0.18.0 生态互动：狐追捕兔（数据式委托原生 Wolf AI，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 捕食机制取证 | 已确认（反射 + 资产实测） | 反射 x25.01.17 确认原生 `Game.ComponentChaseBehavior` 拥有 `FindTarget()`/`ScoreTarget(ComponentCreature)`/`IsTargetInAttackRange`，字段 `m_autoChaseMask : Game.CreatureCategory`；`Game.CreatureCategory` 为 `[Flags]`（LandPredator=1/LandOther=2/WaterPredator=4/WaterOther=8/Bird=16）；`Game.ComponentCreature.Category` 属性即生物声明的类别。再用 `Game.SurvivalCraftModEntity.GetDecipherStream(Stream)`（static, NonPublic）解出 `Content.scpak` 内的 `Assets/Database.xml`（987 条目、463610 字节）实测：基类 `ChaseBehavior`(GUID `31894441-…`) 默认 `AutoChaseMask="LandOther, Bird"`、`ChaseNonPlayerProbability=0`；`Wolf`(GUID `49a9845c-…`，狐继承父) 只覆写 `ChaseNonPlayerProbability=0.03`、`DayChaseRange=4`、`NightChaseRange=10`、`NightChaseTime=15`（掩码沿用基类）；`Alpaca`(GUID `8e62c6e4-…`，兔继承父) `Creature.Category="LandOther"`（参数 `1914b71d-…`）。据此狐（继承狼的 ChaseBehavior+Miner）本就把 LandOther 列为可追对象，兔（继承羊=LandOther）本就在其猎物集合内。 |
| 不臆造 API | 已确认（取证纠偏） | `Database.xml` 基类 `ChaseBehavior` 参数集不含 `IsAttack`——反射中 `m_isAttack`/`IsAttack` 是运行时字段而非可序列化模板参数，故本阶段未在模板里写 `IsAttack`（避免臆造参数）；实际“咬击伤害”来自狐继承的狼 `Miner.AttackPower=1` 与追捕进入攻击距离后的原生结算。 |
| 数据式接线（委托原生 AI） | 已确认（代码/数据） | 只在 `SCIEPortFox` `EntityTemplate` 增加一个 `ChaseBehavior` `MemberComponentTemplate` 覆盖：`AutoChaseMask="LandOther"`（把猎手范围显式锁定到陆生猎物，不再依赖基类默认顺带含 Bird）与 `ChaseNonPlayerProbability="0.35"`（狼基线 0.03 过低、几乎不主动猎非玩家动物，故提高使捕食可观察）。不写任何 C# 转向/寻路/攻击逻辑，全部交给原生 `ComponentChaseBehavior` 的 `FindTarget/ScoreTarget`。匹配方式与本工作区既有覆盖一致：按 `Name` 关联继承组件、`Parameter` 按 `Name` 覆写字段、各用唯一 Guid（成员 `b8e3cebb-…`、参数 `60d23cc8-…`/`7e78e276-…`）。 |
| 猎物资格（离线纯逻辑） | 已确认（净8 无游戏依赖） | `EcologyRules` 新增：`FoxAutoChaseMask="LandOther"`、`RabbitCreatureCategory="LandOther"`（与 XDB 单一事实源一致）、`VanillaWolfChaseNonPlayerProbability=0.03`、`FoxChaseNonPlayerProbability=0.35`，及镜像原生“类别是否在掩码内”判定的纯谓词 `IsCategoryChased(mask,category)`——逗号分隔解析、把序列化为 `"0"`（即 `CreatureCategory.None`）与 null/空一律判不追、大小写不敏感并逐段 trim。`tests/SCIEPort.Ecology.Tests` 由 37 增至 47（新增 10 断言，含核心不变量“狐掩码必须覆盖兔类别”“狐概率必须高于狼基线”）。 |
| 打包校验 | 已确认 | `Build-NetMod.ps1` 对 `SCIEPortFox` 断言存在 `ChaseBehavior` 覆盖、其 `AutoChaseMask` 逗号集合含 `LandOther`、`ChaseNonPlayerProbability` 可解析为 float 且 > 0.03；缺失或过小即抛错。 |
| 实机捕食激活 | 待验证 | 静态/数据 ABI 与纯规则已确认，但狐在真机是否确实发现、追捕并杀死兔、0.35 概率的手感与频率、与羊群 `AutoNearbyCreaturesHelp`/逃跑行为的交互、退出重进后一致性，均未在 Windows 客户端或 Android 7723 观测；按要求不提前宣称已修复。 |
| 多人联机同步 | 待验证 | 追捕/攻击结算由实体权威端（单机 Local 或主机/服务端）仿真，普通客户端不本地生成实体；但权威端捕食结果向客户端的实体状态同步、房主与加入者观感一致性未实测。 |
| Windows 游戏运行 | 待验证 | 本机存在可运行的 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测捕食行为。 |
| Android 7723 | 待验证 | 低端机上捕食 AI 的开销、可见性与稳定性需真机新世界验证；不得因编译或离线测试通过标记为兼容。 |
| 无越界改动 | 已确认 | 本阶段不新增地形/实体写入路径、不重写寻路，仅一处只读纯谓词 + 一处数据模板覆盖，符合“先独立基础功能、委托原生 Wolf AI”的方向。 |


## 0.19.0 批次2首片：向日葵作物（复用已验证作物管线，静态/离线阶段）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 复用已验证作物切片 | 已确认（代码/数据） | 完全镜像本工作区已充分验证的亚麻（Flax）垂直切片结构：`SunflowerRules`（纯逻辑，无游戏依赖）+ `SunflowerPlantBlock : CrossBlock` / `SunflowerSeedBlock : FlatBlock`（自绘 atlas）+ `SubsystemSunflowerBlockBehavior : SubsystemPollableBlockBehavior`（生长轮询）+ CSV 两行 + XDB 行为模板绑 `Class`。不引入任何新的 ABI 面，只复用既有基类。 |
| ID 取证 | 已确认（枚举） | 编译单元内 `public const int Index = 9xx` 全量枚举：作物带 929..935（玉米 929-931、竹 932-933、亚麻 934-935）连续，936..994 空闲，995..999 为载具保留段。向日葵取 936（植株）/937（瓜子产物），无冲突。 |
| clean-room 取证 | 已确认（资产实测） | 反射前已从 `Content.scpak` 解出的真实原版 `Assets/Database.xml`（463,610 字节）中检索：无 sunflower、无 chicken/hen/rooster、无 bee/hive、无 apple 任何痕迹，确认批次2四项皆为 clean-room 新增，非移植。 |
| 食物语义取证 | 已确认（既有可食物对照） | 不臆造“可食”：`KellyFruitBlock` 实测仅为 `Block` 抽象子类，可食性纯由 CSV `DefaultNutritionalValue>0` 驱动（Kelly 野果/浆果 nutr 0.08..0.15、rotPeriod 120..170 为本工作区已证 Android 可跑数据）。据此向日葵种子按 cat=Food / nutr=0.12 / rotPeriod=160 / 无病害建模，与渲染路径（FlatBlock + `GetFaceTextureSlot` 覆盖）正交。 |
| XDB GUID 唯一性 | 已确认 | 新行为模板 Guid `eccfc7fa-…`、Class 参数 `6dd1c1e3-…`、Project 成员 `37706394-…` 均 `node randomUUID()` 生成、全局唯一；仅复用允许跨 xdb 重复的容器根（BlockBehaviors `00c97f0f-…`、Project `85023bf8-…`）。`Test-Database.ps1` 通过（DATABASE VALIDATION PASSED）。 |
| 纯决策（离线可测） | 已确认（净8 无游戏依赖） | `tests/SCIEPort.Sunflower.Tests` 33 断言：DATA 位 0..7 往返、越界钳制且保外位、成熟阈值、生长间隔（底 8、水/氮各 −2、冷 ≤4 +8、暖 ≥12 −2，并断言“冷减速 > 暖加速”这一温季作物签名、间隔下限 1）、光照阈值（向日葵全日照 `MinLightToGrow=10` 严格高于耐阴亚麻 8 且 ≤15）、放置需顶面土壤、收获 4 食籽 + 1 可重种籽、权限闸 `!isClient`。 |
| 自绘图集 | 结构已确认 | `New-SunflowerAssets.ps1` 生成 `Textures/Sunflower.png`：512×512、Format32bppArgb、16×16 格 32px；slot 0..7 生长阶段（绿芽→高茎→顶端黄花盘棕心）、slot 8 瓜子堆，其余全透明（cross-face alpha test）。打包断言：0..8 每格非空、slot 12 全透明。真机采样/开花渲染/图标 待验证。 |
| 打包校验 | 已确认 | `Build-NetMod.ps1` 新增：跑 Sunflower 测试；requiredEntry 加 `Sunflower.csv/xdb/png`；XDB 断言 `SCIEPortSunflowerBehavior` 继承原生 pollable 基 `fefb9590-…` 且 `Class` 绑定子系统类、Project 成员挂该行为 Guid；CSV 断言恰一行植株（Crops/可放/slot0）+ 一行瓜子（Food/可放/slot8/nutr>0）；atlas 逐格像素校验。 |
| 编译产物 | 已确认 | `Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0（`TreatWarningsAsErrors` → 0 warning / 0 error），54 条目，DLL `[AssemblyName]`/`FileVersion`=`0.19.0.0`、`ProductVersion`=`0.19.0+5b793cf`。二进制内确含 `SunflowerPlantBlock/SunflowerSeedBlock/SubsystemSunflowerBlockBehavior/SCIEPort.Features.Sunflower/SunflowerRules`。产物 `.netmod` 417,793 字节，SHA-256 `C08B52F2ECB9E8E31C6A5C1E1EA7A9B79ABDD6E4BAE69B4A36606763EB58D8C6`（磁盘复核一致）。 |
| Windows 游戏运行 | 待验证 | 本机有可运行 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测：种植、逐阶段生长、成熟收获（可食籽 + 可重种籽）、吃掉种子回饥饿、存读档、卸载均未运行。 |
| Android 7723 | 待验证 | 两方块渲染、贴图、种植交互、生长轮询、收获与退出重进恢复均需真机新世界验证，不得因编译或离线测试通过标记为兼容。 |
| 多人联机 | 待验证 | 轮询已在 `WorkType.Client` 直接返回，仅权威端推进；真实同步一致性未实测。 |
| 无越界改动 | 已确认 | 本阶段严格限定在 SCIE-Port 内新增向日葵切片，未改其他 mod、未吞异常、未动既有作物/生物行为，符合“按阶段推进、每片独立提交”。 |

## 0.20.0 批次2第二片：鸡生物基础验证（复用已验证生物切片管线，手动生成器阶段，静态/离线）

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| 复用已验证生物切片 | 已确认（代码/数据） | 完全镜像本工作区已充分验证的狐/兔垂直切片结构：`EntityTemplate` 覆写 7 组件（Creature/Body/Locomotion/Health/Spawn/HerdBehavior/FourLeggedModel）+ clean-room COLLADA 六骨骼模型 + 32×8 四色 PNG + `ChickenSpawnBlock : Block`（把模型网格 AppendModelMeshPart 进 BlockMesh 作创造图标方块）+ `SubsystemChickenSpawnBlockBehavior : SubsystemBlockBehavior`（`OnUse` 经 `SubsystemCreatureSpawn.SpawnCreature(string,Vector3,bool)` 生成）。不引入任何新的 ABI 面，只复用原生四足模型/食草 AI。 |
| 原生 AI 继承 | 已确认（静态） | `SCIEPortChicken` 继承原版 Alpaca 食草动物模板 GUID `8e62c6e4-a544-4c09-a3fb-59fd37d5b998`（LandAnimal 后代），复用原生四足步态 + 吃拾取物/群居/逃跑 AI，与兔的复用路径一致。鸡的 `Category` 因此为 `LandOther`，天然落入狐 `ChaseBehavior.AutoChaseMask=LandOther` 的捕食目标集，与生态阶段 synergy 一致（本阶段仅记录该数据层结论，实际追捕仍待真机验证）。 |
| 四足 rig 适配 | 已确认（离线导入） | 鸡仅两条真实腿：保留原生强制六骨骼（Body/Head/Leg1-4）完整性——前两条骨骼挂可见腿、后两条挂靠后收起的短腿，视觉读作鸟 stance。`Build-NetMod.ps1` 用目标 `Engine.Media.ModelData.Load` 实测导入 `Chicken.dae` 为 6 bones/10 meshes 并逐骨骼名（Body/Head/Leg1-4）校验；Y_UP 断言。 |
| clean-room 模型/贴图 | 已确认（资产实测） | 项目自制 `New-ChickenAssets.ps1` 生成 `Models/Chicken.dae`（10 立方网格，每网格 24 顶点/12 三角，无蒙皮/控制器/导入动画，纯程序化步态）与 `Textures/Chicken.png`（32×8、四色不透明：白身/红冠髯/橙喙腿/暗尾；U 值 0.125/0.375/0.625/0.875 选列）。`Build-NetMod.ps1` PNG 断言 32×8、每像素 A=255、恰 4 种 ARGB。 |
| ID 取证 | 已确认（枚举） | 编译单元内 `public const int Index` 全量枚举：作物带止于 937（向日葵瓜子），938..994 空闲，995..999 载具保留段。鸡生成器取 938，无冲突；`Creatures.csv` DisplayOrder 取 911（兔 910 之后）。 |
| CSV 行为绑定 | 已确认 | `ChickenSpawnBlock;SCIE 鸡生成器;Spawner Eggs;SCIEPortChickenSpawnBehavior;911;…;scieport-chicken-spawner;…`，由 Node 脚本按列名从已验证的兔行派生（禁手抄 CJK，`鸡` 字直接取自 `Creatures.xdb` 已提交 DisplayName），UTF-8 无 BOM、CRLF；`Build-NetMod.ps1` 断言恰一行 `ChickenSpawnBlock`、Behaviors/Category 正确。 |
| XDB GUID 唯一性 | 已确认 | 新实体 `SCIEPortChicken`、行为 `SCIEPortChickenSpawnBehavior`（继原生 pollable/behavior 基 `fefb9590-…`）与 Project 成员各用 `node randomUUID()` 生成的全局唯一 Guid；仅复用允许跨 xdb 重复的容器根（Entities `5fcdf35f-…`、BlockBehaviors `00c97f0f-…`、Project `85023bf8-…`）。文件内 Guid 无重复、CR=LF=126 一致。`Test-Database.ps1` 通过（DATABASE VALIDATION PASSED）。 |
| 权限闸（联机） | 已确认（代码/规则） | `SubsystemChickenSpawnBlockBehavior.OnUse` 在 `CommonLib.WorkType == WorkType.Client` 时直接返回不生成，避免普通客户端本地制造未同步实体；与兔一致。真实主机/客户端同步效果待联机实测。 |
| 自然生成 | 不支持（本阶段） | 仅创造栏手动生成器（`Spawner Eggs` 类）。`SubsystemChickenSpawnBlockBehavior.Load` 刻意**不**调用 `CreatureWorldSpawn.SetProject`，不注册 `CreatureType`、不接地形生成 Hook——鸡暂不进入既有狐/兔自然生成 spawner，留待基础加载/存读档实机验证后再单独成阶段。 |
| 跨切片互动（喂籽/下蛋） | 不支持（本阶段） | 不接线鸡吃向日葵/亚麻籽、下蛋等行为；本阶段只做「可手动生成的鸡实体」这一独立基础功能，食性/繁殖留作后续增强，避免臆造 ABI。 |
| 编译产物 | 已确认 | `Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0（`TreatWarningsAsErrors` + `GenerateDocumentationFile` → 0 warning / 0 error），54 条目（含 `Assets/Models/Chicken.dae`、`Assets/Textures/Chicken.png`），DLL `[AssemblyName]`/`FileVersion`=`0.20.0.0`、`ProductVersion`=`0.20.0+2151279`（SourceRevisionId 取构建时 HEAD）。二进制内确含 `ChickenSpawnBlock/SubsystemChickenSpawnBlockBehavior/SCIEPort.Features.Creatures`。产物 `.netmod` 421,326 字节，SHA-256 `246CFD35A066D90BAE2116E293FAD4A320D9540BD45E668C991A450F434EEDC9`（磁盘复核一致）。 |
| Windows 游戏运行 | 待验证 | 本机有可运行 `Survivalcraft.exe`（x25.01.17），但本阶段未启动游戏实测：图标方块渲染、使用生成、行走/群居/逃跑 AI、碰撞、死亡与存读档均未运行。 |
| Android 7723 | 待验证 | 图标、模型、UV、程序化步态、碰撞、生成与退出重进恢复均需真机新世界验证，不得因编译或离线导入校验通过标记为兼容。 |
| 多人联机 | 待验证 | 生成已在 `WorkType.Client` 直接返回，仅权威端生成实体；真实同步一致性未实测。 |
| 无越界改动 | 已确认 | 本阶段严格限定在 SCIE-Port 内新增鸡切片（块/行为/CSV 行/XDB 实体与行为/资产生成脚本/构建校验/版本号），未改其他 mod、未吞异常、未动既有作物/生物行为，符合「按阶段推进、每片独立提交」。 |
