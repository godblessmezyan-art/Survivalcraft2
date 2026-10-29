# 阿比斯 V1 开发记录（进行中）

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
