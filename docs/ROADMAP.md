# 路线图

完成状态必须以编译和对应平台测试记录为准。

## Phase 1：最小 NET Mod 加载测试（进行中）

- [x] 核验 x25.01.17 Windows 程序集、目标框架和加载器 ABI。
- [x] 建立最小加载日志源码。
- [x] 建立环境检查、Release 构建和 `.netmod` 打包流程。
- [ ] 在公开 Android APK 上确认加载。
- [x] 在 7723 实际客户端上确认最小 `.netmod` 安装与加载（用户实机确认）。
- [ ] 提取 7723 客户端程序集指纹，确认更完整的 ABI 兼容范围。
- [ ] 保存首次单人世界测试记录。

## Phase 2：Android 触屏功能入口

- [ ] 验证安全的 Widget/Screen 生命周期 Hook。
- [ ] 添加可缩放、不遮挡原生 UI 的测试按钮。
- [ ] 支持按钮显示开关和位置配置。

## 实验功能：水上载具

- [x] 从 x25.01.17 程序集确认原生船、浮力、骑乘、触屏输入和骑乘网络包。
- [x] 编译单人快艇推进原型，复用原生船与触屏摇杆。
- [x] 建立独立编号 900、创造栏物品、`Speedboat` 实体模板、生成子系统、原创占位模型与贴图。
- [x] 用用户提供的正式贴图替换占位资源。
- [ ] Android/7723 实机测试 v0.4.0 的物品显示、生成、推进、转向、下船和重新进入世界。
- [ ] 主机与普通客户端分别测试速度保持、位置回弹、生成同步和乘坐同步。
- [ ] 根据实机画面调整模型、UV 和碰撞盒。
- [ ] 在服务端权威逻辑明确前，不启用联机推进。

## Phase 3：小范围批量建造和撤销

- [x] 默认仅填充空气方块。
- [x] 硬性限制方块数量和坐标范围。
- [x] 分批执行、取消和单级撤销。
- [x] 仅在单人新建测试世界验证。
- [x] v0.6.0：把「目标区块未加载或越界」逐格分类（越界/缺失/状态未就绪/未加载），离线证明（72/72）。
- [x] v0.6.0：主线程有限等待（预检）跨区块场地就绪，等待期间不写；越界/高度判硬性错误立即停。
- [x] v0.6.0：回滚/撤销对暂不可编辑格登记延迟条目并可重试，部分撤销保留待续做，绝不谎报干净完成。
- [~] Android 实机验证建造落点、分批可见性、取消与整体撤销（v0.5.0，待用户在单人创造测试世界实测）。
- [x] v0.6.0：用官方 Windows x25.01.17 客户端（`D:\SC`，哈希匹配公开发布）Release 编译 net472（0 错 0 警），Features 层升级为已确认（编译期 ABI），并打包出 `.netmod`（内容守卫全通过）。
- [~] v0.6.0：Windows 游戏进程内加载、Android/7723 实机验证有限等待与延迟回滚/撤销。
- [x] 与 SCIE-Port 共存时的方块编号（900/901/902）、资源路径与数据库 GUID 冲突检查（打包脚本强制校验）。
- [!] v0.6.0：XDB/GUID 合并冲突专项复核——**该结论不完整且误导，已由 v0.6.1 真机日志推翻**。当时只核对了文件夹/Project/自定义结构节点的 GUID，把 `<Parameter Name="Class">` 的 GUID 排除在唯一性检查之外，误判「无合并缺陷」。
- [x] v0.6.1：真机 7723 日志暴露真实缺陷——Building.xdb 里 Builder/Undo 两个方块行为节点复用了同一个 `<Parameter Name="Class">` GUID `ea25b78e-…ba34`（含 Speedboat 全局共 3 次）。x25.01.17 数据库加载器把每一个 `Guid=`（含 `<Parameter>`）都登记进字典，重复键抛 `An item with the same key has already been added. Key: ea25b78e…`，清空 `m_gameDatabase`，级联出 `Database not loaded.` 与 `EntityTemplate "MalePlayer" not found.`。取证：解密真实 `Database.xml` 得 191 个 `<Parameter Class>` GUID 全部逐实例唯一、`ea25b78e` 原版计数为 0。
- [x] v0.6.1：最小修复——仅把 Building.xdb 两个 Class 参数改成新的唯一 GUID（`ecf3ac44…`、`82d5d8fd…`，已验证不在原版库），Speedboat.xdb 逐字节保持原样使 `ea25b78e` 全包只出现 1 次；顺带修掉硬编码的 `v0.5.0` 启动横幅（改由程序集版本派生，设备此前实为旧包）。构建守卫 `scripts/XdbMergeSafety.ps1` 升级为全局 `Guid=` 唯一性校验（豁免原版文件夹/Project 复用），回归 `scripts/Test-XdbGuards.ps1` 增至 7 项全通过。Android 实机复测仍待验证。
- [x] v0.6.2：真机日志暴露第二个独立缺陷——装 v0.6.1 后数据库崩溃消失、建造逻辑已跑到"区块就绪检查"，但报 `470/470 格 ChunkNotLoaded … state=9 loaded=False`。经反射 x25.01.17 程序集取证：`TerrainChunkState.Valid=9`（即内容其实已生成就绪）、`chunk.Cells`/`Coords`/`State`/`IsLoaded` 均为 public、`GetChunkAtCell(x,z)` 与 `ChangeCell` 同用 cell 坐标无换算错配；且落点来自 `RaycastMode.Interaction` 近身射线命中（就是玩家眼前的地），`_terrain.IsCellValid` 也已通过。真因＝`GameTerrainGateway` 把可编辑性**硬性要求 `chunk.IsLoaded==true`**，而 `IsLoaded` 是磁盘/流送态标志，对玩家正站立的 Valid 区块仍为 `false`，导致眼前可建区被误判"未加载"而中止。修复：判据改为引擎自身的 `chunk.State==Valid AND chunk.Cells!=null`（`IsLoaded` 只记入日志不再把关），把误导性的 `ChunkNotLoaded` 状态/中文标签「区块未加载」更名/改为 `ChunkContentsNotReady`/「区块内容未就绪」，并回填真实区块坐标（不再打印 `int.MinValue` 的 `UnknownCoord` 哨兵）。离线逻辑 72/72、XDB 守卫 7/7 通过，net472 编译 0 错 0 警。Android 能否正常建造仍**待验证**。

## Phase 4：区域选取、复制、粘贴与旋转

- [x] 触屏选取两个角点。
- [x] 剪贴板数据与世界数据分离。
- [x] schema v2 楼梯/半砖/栅栏/门/竖向原木旋转与镜像离线测试。
- [x] v0.16.0：运行时注册表核对实际 Block 类型/基类族；非法 DATA 与族冲突明确拒绝。
- [ ] Android 7723 逐族复制→旋转→建造→存读档实测。

## Phase 5：JSON 建筑蓝图导入导出

- [x] schemaVersion 1/2 兼容的稳定格式。
- [x] 尺寸、方块数和输入路径校验。
- [x] 不导出玩家、实体或私密存档数据。
- [x] v0.16.0：`f`/材料实际族、`materials.key/block` 与每族 DATA 严格一致性校验。
- [ ] schema v3 显式 `state` 扩展（颜色/实体 payload）；未设计完成前不得复用 `props` 或静默降级。

## Phase 6：预览、渐进式建造和复杂方块

- [ ] 非破坏性预览。
- [ ] 每帧预算、进度、暂停和恢复。
- [ ] 家具、电气、容器等复杂方块的显式兼容表。
- [x] v0.16.0：生成 165 个原版可放置类型的完整阶段审计表，并列出实体、电路、流体、依附与世界更新风险。

## Phase 7：性能、存档兼容与多人行为

- [ ] 压力测试与帧时间预算。
- [ ] 跨版本存档回归。
- [ ] 主机/客户端/服务端权限矩阵。
- [ ] 联机失败时安全降级为只读或禁用。
