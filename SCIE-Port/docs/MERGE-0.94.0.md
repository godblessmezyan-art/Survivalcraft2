# 共同主线整合 0.94.0

2026-11-18，用户要求拉取代码、解决冲突并交付netmod。本地起点`0b9b7a0`，实际GitHub main为`ba27dd0`。本机origin的fetch配置只跟踪旧port分支，初次普通fetch未更新origin/main；使用显式`git pull --no-rebase origin main`和`git fetch origin main:refs/remotes/origin/main`取得真实主线，未修改remote配置，未执行push。

保留双方完整提交历史。远端新增12个提交，包含睡眠射线/恒温床/壁炉、植物0.93.0、系统选书/JNI/EPUB、纸页界面和书库返回/段落空行修复、主机钓鱼/蓄力弹道。本地独有11个提交，包含骨龙已提交模型、古建detail/home蓝图、云崖书屋、数字书房生成/发布技能、四种动物与原生生态、美术学习资料；书柜、五分类和统一书典共同保留。

16个冲突文件包括版本、加载器、钓鱼实现/测试/构建与双方文档。钓鱼采用远端完整0.92.0蓄力/收线流程，替代本地0.86.2直线抛竿与旧Request/Result轮次；保留原生流体等级解码、鱼竿朝向、主机复验/奖励与可靠20字节包236。移除已无调用者且依赖废弃规则常量的FishingCastResolver和测试链接，不把两套钓鱼协议拼接运行。其他家居、古建、动物及书典检查全部保留，加入植物/选书/纸页检查。

植物通过原生TerrainContentsGenerator包装在第四阶段后生成；动物继续由原生SubsystemCreatureSpawn在更新线程生成。移除无用途的OnTerrainContentsGenerated Hook与旧Kelly队列调用，不恢复远端旧动物队列。床保温Hook保持。没有新增ID/网络包/存档身份，统一net472/API1.42/SC2.4.10.6、SCIEPort.dll与版本0.94.0。

工作区三个未完成骨龙文件在`artifacts/merge-0.94.0/preexisting`备份并逐字节核对，原件保持。构建只从合并后的Git索引导出源码快照，不将这些未提交改动纳入包或提交。已知相邻Surviveft2目录为空，当前仓库worktree只有本地main。

实际命令（仓库根）：

```powershell
git pull --no-rebase origin main
git fetch origin main:refs/remotes/origin/main
$env:FrameworkPathOverride='D:/Dev/Tools/net472/build/.NETFramework/v4.7.2'
$env:DOTNET_ROOT='C:/Users/85196/AppData/Local/Microsoft/dotnet'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir D:/Games/SCNET-x25.01.17
powershell -NoProfile -ExecutionPolicy Bypass -File artifacts/merge-0.94.0/source/SCIE-Port/scripts/Build-NetMod.ps1 -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release -OutputDir SCIE-Port/artifacts/merge-0.94.0
```

Python子进程使用标准WindowsPowerShell模块路径；固定目标DLL仅从本机引用，不打包。首次完整构建发现旧FishingCastResolver引用已删除的MaxCatchDistance，移除无调用旧实现后重新构建；失败日志保留为`first-build-failed.log`，不把失败算通过。最终结果与指纹见本文件后续记录和TEST-RESULTS。

Android7723实机、触屏/分辨率、旧存档、真实Local/Server/Client仍待验证。源码只在本地main提交；该合并提交尚未由用户推送到GitHub前不创建指向旧提交的Release。

## 最终结果

固定Windows Net_x25.01.17环境/哈希/ABI、完整net472 Release和统一包校验**已确认**；三个编译阶段均0警告/0错误。蓝图379359、植物1052680、钓鱼2526规则/40原生、动物11123规则/96原生、书典627核心/34944目标、家居496928规则/911原生、书柜40网格/5方块/4配方、五分类807规则/259原生检查及全部既有回归通过。上述为离线/原生隔离检查，非游戏房间执行。

移除废弃OnTerrainContentsGenerated Hook：植物在原生生成器第四阶段后生成，动物由原生SubsystemCreatureSpawn在更新线程生成；其余Hook、net472/API1.42/SC2.4.10.6、SCIEPort身份/旧ID/GUID/存档保持。包236仍20字节，采用完整蓄力/收线协议；各端统一0.94.0，旧版混用不支持。

218条目、单SCIEPort.dll，旧0.91.0全部216条目保留；新增两张书典纹理，206旧条目逐字节相同。212个源资产与构建快照逐字节一致；其中93个文本相对Git索引只有Windows检出行尾变化，二进制/纹理未改变。主包及0.94.0版本副本字节一致。

| 产物 | 字节 | SHA-256 |
|---|---:|---|
| 荒野探险家.netmod / 荒野探险家-0.94.0.netmod | 12989976 | `099984BD122A370BD2E59557C7529E8E149A7432A97530015AB668F30B4DB1CA` |
| SCIEPort.dll | 1055744 | `EA536B4C387FA3605C7FF83DA228EE909D9E782B743035E5502FFF98EE0DD387` |

本机留存Android三份BCL快照解析绘卷496和钓鱼87个成员，0缺失，MVID `48c195d8-4eac-4b59-aba7-9c2fd7949e3c`。这些检查不覆盖新书典完整System.Xml/平台程序集，未将远端既有Android审计记录冒充本次全量复测。Android7723、触屏/分辨率、旧世界和真实Local/Server/Client仍**待验证**。

命令、首次失败及纠正、边界和输出位置见[整合记录](MERGE-0.94.0.md)。完整构建退出0，日志`artifacts/merge-0.94.0/full-build.log`；独立包比对`package-check.json`，BCL日志`android-bcl.log`/`android-fishing.log`。源码只本地main提交，未push；该合并提交在GitHub可访问前不发布指向旧提交的Release。


另核对D:/Dev/Survivalcraft2与D:/Dev/Survivalcraft2-abyss-domain：两者main均为250695e，是ba27dd0的祖先；未修改其工作区或未跟踪资料。
