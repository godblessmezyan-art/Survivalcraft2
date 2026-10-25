# SCIE-Port 移植工作区规范

本目录是第三方 Mod → SCNET x25.01.17 的独立移植工作区。当前包含 SCIE Industrial Era 与 Kelly Expansion 植物资源移植；它不属于 `src/BuilderAssistant`，不得引用、修改或打包 Builder Assistant 源码与资源。

- 唯一目标运行时：Survivalcraft NET `Net_x25.01.17`，目标平台 Android，产物 `.netmod`。
- 每个上游来源必须记录名称、版本/提交、来源、许可或用户授权声明、纳入范围和资源哈希；缺少许可证据的内容不得进入可发布产物。
- `upstream/SCIE/` 是被 Git 忽略的只读上游快照。SCIE 上游未声明公开许可证，只能按已记录的用户授权范围使用；不能据此扩大到其他上游内容。
- `src/SCIEPort/` 只允许针对 SCNET 重写的代码，以及有明确分发许可的原创或授权资源。禁止打包上游 DLL、PDB、APK、游戏程序集和未经核验的依赖。
- 不同 Mod 的功能放入独立子目录（例如 `Features/Watercraft`、`Features/KellyPlants`），资源使用独立文件名，数据库模板名和 GUID 不得互相碰撞。
- 游戏程序集只从 `SCNetGameDir`/`SCNET_GAME_DIR` 引用，不得复制或提交。
- 不使用 SCAPI DLL，不复用旧版 DLL，不通过伪造类或空实现宣称完成。
- `.scmod` 与 `.netmod` 严格分离，不得重命名冒充；所有接口必须回到目标 SCNET 程序集验证。
- 创造栏/物品栏默认保留上游原名。除非上游名称本身包含括号，否则禁止追加“（移植）”“（授权移植预览）”等来源或状态后缀；移植状态写入说明文档，不污染物品名。
- 新增方块必须先检查原版与本工作区全部 `Block.Index`，并在兼容性文档记录区间；同时安装其他 Mod 时仍需提示外部冲突风险。
- 每项移植必须在构建脚本中校验必需资源、名称约束、CSV 类名唯一性、XDB 容器 GUID/继承关系和禁带二进制。
- 先完成单人材料注册和加载验证，再逐模块移植；联机功能必须区分 Local、Server、Client 权限。
- 兼容性状态只使用：`已确认`、`待验证`、`不支持`。
- 每次构建先运行 `scripts/Test-Environment.ps1`，再运行 Release 构建和包内容校验，并把结果写入 `docs/TEST-RESULTS.md`。
- Android 首测必须使用可丢弃的新建单人世界和存档备份；世界生成应有每区块/每帧预算，禁止一次更新无界扫描或写入。
- 所有载具（摩托车、汽车、坦克、载具舰船、直升机等）在首次正式交付前必须完整满足 `docs/VEHICLE-QUALITY-STANDARD.md` 的模型、贴图、人物融合、开发与验收全部条款；"能运行""能加载""编译成功"一律不得视为完成，未达标项必须显式列出差距、原因与下一步，禁止用最小可运行版本或临时测试模型冒充最终作品。
- 新建或重做载具模型前必须完整阅读 `docs/VEHICLE-MODEL-GUIDE.md`；静态载具使用单根骨骼，且必须用目标 `Engine.Media.ModelData.Load` 校验实际导入骨骼/网格数，不能只验证物品栏图标或 DAE XML。
- 新增或修改坦克炮、舰炮、装甲车武器、列车炮、飞艇武器等大型载具攻击功能前，必须完整阅读 `docs/LARGE-VEHICLE-WEAPONS.md`。不得直接照搬原版火枪的 owner 炮口校正；必须验证载具 AABB 外炮口、视觉/弹道一致、近车体引信保险、事件清理以及 Android 第一炮安全。

