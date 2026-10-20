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
- 新建或重做载具模型前必须完整阅读 `docs/VEHICLE-MODEL-GUIDE.md`；静态载具使用单根骨骼，且必须用目标 `Engine.Media.ModelData.Load` 校验实际导入骨骼/网格数，不能只验证物品栏图标或 DAE XML。

