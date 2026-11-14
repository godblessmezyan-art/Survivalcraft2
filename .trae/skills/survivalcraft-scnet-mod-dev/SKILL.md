---
name: survivalcraft-scnet-mod-dev
description: 开发、构建与验证生存战争 SCNET Net_x25.01.17 的 .netmod 模组。当用户要求修改荒野探险家、天工绘卷、蓝图、神器、书典、建筑助手，或排查 SCNET 构建、打包、联机、模型导入问题时使用。不用于纯玩法问答。
---

# 生存战争 SCNET 模组开发

## 开工顺序

1. 读仓库根 `AGENTS.md` 和 `SCIE-Port/AGENTS.md`，再读 `SCIE-Port/docs/COMPATIBILITY.md` 顶部的当前结论。
2. 不要继承旧版本、旧工作副本或别处项目的兼容结论；以当前目标程序集和最新测试记录为准。
3. 按任务读专项：
   - 蓝图/绘卷：`SCIE-Port/docs/TIANGONG-BLUEPRINT.md`、`TIANGONG-DIRECT-0.83.0.md`
   - 物品/神器：`INVENTORY-STANDARD.md`、`PIXELFX-DEVELOPMENT-STANDARD.md`
   - 打包/品牌：`PACKAGING-RULES.md`
4. 只改任务范围内的文件。BuilderAssistant 与 SCIE-Port 是独立工程，不跨工程引用源码或资产。

## 固定目标

- SCNET `Net_x25.01.17`，ApiVersion 1.42，ScVersion 2.4.10.6，net472，产物 `.netmod`。
- 游戏程序集只从 `SCNetGameDir` / `SCNET_GAME_DIR` 引用；绝不提交 APK、游戏 DLL、PDB 或内容包。
- 接口必须回到目标程序集取证。SMT3、SCAPI 模板、旧 DLL 和第三方 Mod 只作线索。
- 未编译不算完成；未在 Android/7723 实测的构建不得标记“兼容”，只能写“待验证”。
- 玩家界面少提示：按钮直接写操作名，不常驻机制、数值或教程。

## Windows 构建

当前开发机的固定环境（路径按机器调整）：

```powershell
$env:DOTNET_ROOT='C:/Users/85196/AppData/Local/Microsoft/dotnet'
$env:FrameworkPathOverride='D:/Dev/Tools/net472/build/.NETFramework/v4.7.2'
$env:DOTNET_ROLL_FORWARD='Major'
$env:PATH="$env:DOTNET_ROOT;D:/Dev/Tools/node-portable/node-v20.18.0-win-x64;D:/Dev/Tools/MinGit/cmd;$env:PATH"
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 `
  -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release
```

- `dotnet` 可能不在 PATH，用 `DOTNET_ROOT` 下的完整路径。
- `node` 缺失会让 PixelFX 资源校验直接失败；先确认临时 node 目录在 PATH。
- `git` 不在 PATH 时用 `D:/Dev/Tools/MinGit/cmd/git.exe`。
- 单独跑蓝图测试：

```powershell
dotnet build SCIE-Port/tests/SCIEPort.Blueprints.Tests/SCIEPort.Blueprints.Tests.csproj `
  -c Release -p:SCNetGameDir=D:/Games/SCNET-x25.01.17
SCIE-Port/tests/SCIEPort.Blueprints.Tests/bin/Release/net472/SCIEPort.Blueprints.Tests.exe `
  D:/Games/SCNET-x25.01.17 SCIE-Port/artifacts/build/SCIEPort.dll
```

## 已知坑

- **PS 5.1 编码**：含中文的 PowerShell 脚本必须保存为 UTF-8 with BOM，否则按 ANSI 解码报 ParserError。
- **构建假成功**：脚本守卫可能中途抛错但退出码仍为 0。必须检查日志中的 `checks passed`、SHA-256、条目数，不要只看 `$LASTEXITCODE`。
- **构建路径**：不同 csproj 的 `SCNetGameDir` 默认值可能过期，显式传 `-GameDir` / `-p:SCNetGameDir`。
- **XDB**：目录容器必须复用原版 GUID；模板父级 GUID 必须逐字符核对，`Test-Database` 会抓错。新增内容必须全局唯一，避开现有 ID/DATA/GUID。
- **碰撞**：`ComponentBody` 只把地形碰撞用于平滑抬升，实体碰撞盒踩不上去。地板/甲板/楼梯用分段 body + 隐藏地形，单代理半对角线不超过 4，否则边缘会穿。
- **模型**：DAE `<triangles>` 的 `format` 参数要与实际属性对应；UV 必须按面分区并内缩，不能所有面都写 `uv(0,0)..(1,1)`，否则整模型采样同一像素、发黑。
- **伤害数字**：`ComponentHealth.Injure` 不产生飘字；飘字要从 `ComponentMiner` 的近战路径或复刻 `ComponentHealthPackage` + `HitValueParticleSystem` 生成。
- **推送凭据**：无 TTY 时 git 弹不出凭据窗口；源码 `git push` 由用户本人执行，助手只做本地提交。
- **副本差异**：Codex/WorkBuddy 下的 `Surviveft2 2` 是另一个工作副本，可能领先或落后。只借鉴经验，不直接照搬其版本结论或哈希。

## 天工绘卷与蓝图

- 0.83 起为直接施工：房主本机执行，普通客户端用原生地形 ID7（`ChangeCellNet` / `SubsystemTerrainPackage`）同步。
- 不要恢复 237 上传、主机票据、真名门槛或“主机响应超时”路径。
- 清障默认开启：确认后先清空蓝图包围盒内（含门上半格、家具占地）所有非空气方块，再写入蓝图。
- 施工硬约束：32 格/帧、单轴 ≤128、每事务 ≤32768 格、清障包围盒 ≤262144 格；每格写前捕获原值，取消/失败/撤销可恢复。
- 门只能落在空气目标；覆盖已有门仍不支持。清障时旧门只允许清为空气。
- 未开启清障时仍拒绝覆盖非空气目标；任何放宽必须写进专项文档和测试。

## 物品、神器与特效

- 新神器默认必须带：持有移速 +20%、近战攻速 +10%、专属物品栏背景；缺一不得标记完成。
- 移速走 `ComponentLocomotion.WalkSpeed` 自有增量，先归还再按基数重算；攻速走 `m_lastHitTime` 缩减并与外部攻速相乘。
- 特效选型前先查 PixelFX，记录候选、适用阶段和采用/不采用原因；PixelFX 只做表现，不改伤害、冷却、权限、存档或协议。
- 所有新增物品进独立 `SCIEPort.*` Tab，并注册主题背景；新分类必须同时补自动检查。

## 安全与交付

- 地形操作必须有坐标边界、数量上限、每帧预算、写前撤销和取消入口；首测用可丢弃的新世界。
- 联机功能区分 Local / Server / Client，没有证据不得假定客户端写入会同步。
- 完成并通过构建后默认提交本地 `main`，只提交本次范围；不 force push。
- 交付包固定 `荒野探险家.netmod` 和字节一致的 `荒野探险家-<版本>.netmod`；内部 DLL/命名空间仍为 `SCIEPort`。
- 记录构建命令、测试项数、包 SHA-256、DLL MVID、版本号和 Android/联机待验证项。
- GitHub Release 必须对应实际可访问的提交；提交未推送时先保留产物并说明。
