# 阿比斯 V1 开发记录（进行中）

## 2026-10-29 0.18.0 本地技能预览（未完成）

- 环境：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1` 通过；完整阶段构建：`powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -OutputDir E:\Survivalcraft2\SCIE-Port\artifacts\abyss-v1-staging`，net472 Release 和包校验通过。
- 离线断言：普通攻击 38、断界斩状态/输入/范围/去重 441、星门注册与序列化 21、模拟传送状态和故障 23、虚空冷却/阶段/几何规则 16。目标程序集原生夹具共 868 项，含武器包、触屏坐标、射线/领地、星门地面/空位及 `ValuesDictionary` 原生序列化回环；不是游戏进程或 Android 测试。`Test-VoidPrototype.ps1` 检查新代码使用的目标程序集 API、构建类型及未调用地形修改接口。
- 独立开发包：`artifacts/abyss-v1-staging/荒野探险家-0.18.0.netmod`，SHA-256 `3AE81E2510BC8BADD0CB713C6101DC3FE0A6C1CDA331F5114B21A97658E145A7`；这是未完成预览，不是 V1 成品。
- 星门记录、碰撞筛选、已加载落点传送、瞬狩与领域是本地/本地主机原型；未经真实世界或安卓运行测试。远距离主动加载、普通客户端新技能/状态同步不支持。已验证 SCNET 的远程玩家 BodyUpdate 会覆盖服务器端的位置，不能安全地以一次 PositionSet 冒充完整权威传送。
- 原始模型、纹理、CSV 的物品/持握字段、AbyssCombo 和二技能核心未修改，冻结守卫通过。XDB 在原 GUID 上新增上限和画质参数，当前 SHA-256 `6895E106D214DB3C465FEC288BB9451F2B138D64E93D8B0E9D8803DD6ED47FC1`；后续阶段必须继续核对。
- 四槽当前为文字按钮；星门、折跃、天空、黑洞、领域均为尚待审美和设备验收的简易效果，缺用户要求的完整演出及部分音效。无真实地形写入；第三方领地保护未适配。Android 7723、Windows 游戏内、正常存档重进、单人/主机/客户端真实联机全部待验证。
- 用户本轮明确取消自动关机，不执行关机。


## 2026-10-29 阶段 A 部分实现

- 指定目标 `port/scnet-x25.01.17` 基线 `d81e09e`；开发分支 `codex/abyss-v1`。目前尚未交付 V1，不能将本阶段包视为四技能成品。
- 环境：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1` 通过，Windows x25.01.17 / net472。
- 新增星门世界记录容器，含世界、唯一 ID、名称、坐标、方向、创建者、目标、有效状态及版本。记录操作和 XML 往返/异常输入离线测试；坏数据或未知版本拒绝载入并原样保留，避免保存时覆盖。`SubsystemAbyssReaper.Save/Load` 已接入记录，但目前没有星门创建入口或交互、授权、安全传送、UI 和图形实现；持久化在真实世界保存重开仍待验证。
- 新增三技能距离和大招阶段/去重纯规则，不代表技能已接入游戏。引擎调用、粒子、敌人牵引、权限与网络均未完成。
- `dotnet run --project SCIE-Port/tests/SCIEPort.Abyss.Tests/SCIEPort.Abyss.Tests.csproj -c Release`：星门 19 项、技能规则 11 项、二技能 441 项及普通攻击 38 项断言通过。
- `powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1`：阶段性 Release 编译及包检查通过；此包仅用于开发验证，仍使用基线 0.17.0 标识，不是 V1.0 安装交付；本次构建覆盖了 artifacts 中的同名 0.17.0 测试包，SHA-256 `12CF8CC46CE24CD3A4F9AA88803E79B2AD2FAFD258DCE815C51148224C48B653`。
- `SubsystemTerrain.TerrainUpdater.SetUpdateLocation/RemoveUpdateLocation` 在固定程序集可见，但临时更新位置的 ID 分配、并发释放、远端区块和超时安全尚未验证；`ComponentFrame.Position` 会设置 `SendPosition`，不能由此推断普通客户端权威同步。故未实现强行移动。
- 模型、贴图、CSV、XDB、普通攻击和断界斩核心文件基线 SHA-256 依次为 `48BF03C9667DC181BD31387ED3F9F6A40007997E05A4E097CA0EBA3FF17EC3A8`、`8F294F389E15D44FFD0102B9A01925BF435B58DDAA633B266DC565579374587F`、`C1508F0871BD9C584BD8389679B3F56608269A57E2CDB2995B1991AF16B8C6C4`、`8BCB82A9575258DEAE70E37C04CCA08AC77CEB655B99FC1AB0648EDAA2828661`、`4833B31F8630EF44D3E2764EA5EEF3E8DCC064B16A75DA86933C44DF9B4FF256`、`B547CED2160C3EEBB8C7778649DF98E5D113065AA10C6B2E6A28D395086AA928`；构建前后未修改。
- Android 7723、Windows 游戏内、单人、主机、普通客户端均待验证。HUD、视觉、权限、跨会话存档、区块就绪、瞬移与终结伤害尚未完成。因存在关键功能缺失，不执行关机。
