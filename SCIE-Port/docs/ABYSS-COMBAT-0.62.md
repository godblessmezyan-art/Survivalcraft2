# 阿比斯：冷却与虚空普攻/重击 · 0.62.0

基准main `226fd0f` / 0.61.1。开发前工作区干净，fetch+ff-only确认无远程更新，建立 `backup/abyss-0.61.1-before-heavy-rebalance`；旧0.61.1包保留。按本轮用户指令仅改阿比斯。

| 操作 | 当前规则 |
|---|---|
| 大招虚夜湮灭 | 冷却 **69秒**，原4.2秒演出、免伤、128/256湮灭和结算保持 |
| 二技能无界折跃 | 冷却 **3秒**，18格全方向与双裂隙保持 |
| 三技能万象破碎 | 冷却 **36秒**，0.61.1宽裂界/1.8秒/160/半径10/1.35秒一次保持 |
| 一技能按钮 | 原10秒冷却、25格射程、6格宽、100初击＋60残隙保持 |
| 平A | 原三连段节奏、22/22/34、4.5格判定保持；斩痕改为宽大厚刃 |
| 蓄力重击 | 0.65战斗时间蓄力后释放原断界斩：25格/宽6/100＋60/残隙4秒；**独立于按钮10秒CD，不消耗或重置按钮CD** |

攻击力均为原生AttackBody名义输入，实际生命扣除仍受游戏与其他Hook影响。重击不再附加旧52近战伤害或旧40收束伤害；每次初击/残隙各自去重，排除所有玩家和直接乘坐的承载体，沿用领地/已加载/墙体及容量检查。每次必须重新蓄力，活动斩击、其他技能和动作锁仍不能重入。FourSaints和武器攻速乘数作用于蓄力速度，主机按自己核实的频率计时。

## 普攻和输入接线

保留冻结的AbyssCombo、A3模型、持握、三连段、+20%移速/+10%攻速与专属槽位。AbyssMeleeScene由14段细线改为24段实体刃面：半径4.8、最宽径向约1.56格，尖端和尾部收尖，黑紫内核、上下厚面、不规则紫色刃缘、有限晕光。每次288三角形、无粒子/每帧大型数组，原生DepthRead和NonPremultiplied。移除旧5段小裂线；视觉放大不扩大平A伤害判定。随阶段在0.9归一化动画相位前消失。

SubsystemAbyssReaper新增UpdateMeleeInput适配器，在原FourSaints动作锁检查之后接入原Combo：短按继续原三连击；完整蓄力松手把释放交给WorldCutRuntime，同时清掉旧重击的近战Impact和动作，防止52＋100重复命中。远程刀刃、贴墙截停、恢复动作、残留4秒裂隙、取消与生命周期全部复用原一技能。

**现有支持范围需区分：**单机/房主平A的伤害保持原路径；普通客户端平A伤害仍是旧版未支持状态，本次只扩大它的本地斩痕。普通客户端重击现已通过下面的主机权威路径请求，不在客户端算伤害。真实房间仍待验证。

## 247协议扩展与主机蓄力

已在实施前说明需要扩展请求类型，以防普通客户端任意伪造瞬发重击。保留247 ID及所有字段/顺序，不追加数据：Kind新增6开始/续租、7释放、8取消；客户端仍仅接受主机1/2/3/5事件，原0一技能和4取消保持。新的消息走现有身份、实体和连接Token检查。服务器和客户端须同为0.62.0；旧版不会识别新的重击请求，旧客户端还可能拒绝36秒三技能回执。

每次按住开始建立独立序号的蓄力记录，持续按住每秒最多一次续租；租期6秒，不重置已累计的蓄力。松手后房主核实同一记录，未满足0.65/实际攻速的主机时间则排队到合法时刻再开始原0.38秒斩击前摇。没有开始记录、重复/过期/取消/旧连接释放都不能产生额外刀刃。攻击输入在等待回执和斩击期间被占用；沿用2秒回执超时取消。普通技能CD保留在同一玩家记录中，重击绕开的是按钮CD校验，不绕过权限或动作锁。

247原有字段压缩读写继续测试，249/250/251字段和存档v1保持。251冷却上界随三技能改至36.1秒，避免普通客户端丢掉36秒事件。249保留旧180.1秒解码上限兼容旧字节范围，**新施法实际冷却为69秒**。没有增加网络包ID、技能图标、地形/建筑/生物/存档写入。

## 验证与交付

完整Release通过，详见 [TEST-RESULTS.md](TEST-RESULTS.md)。26,032项原生夹具包含真正的重击输入接线、主机计时、25格刀刃与100＋60去重、按钮CD独立、连续再次蓄力、取消与重复/旧Token释放、普通客户端只发请求及36秒回执。记录AttackBody Hook名义参数并停止原生副作用，不是实战HP/护甲或真实UDP证据。

138项目标Engine/OpenGL合成GPU检查及2次CPU HUD导出通过，增加三段普攻0.35/0.48/0.65相位的大面积实体刃面检查与0.9相位恢复。截图使用原创合成地形，**非真实游戏/非Android实拍**；[普攻与重击残隙预览](../design/abyss-combat-0.62.0/combat-stages.png)中的重击卡片展示复用的残隙入口，飞行/伤害由原生运行夹具验证。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-WithPinnedSdk.ps1 -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-AbyssSpaceGpu.ps1 -GameDir D:/Games/SCNET-x25.01.17 -AssemblyPath SCIE-Port/artifacts/build/SCIEPort.dll -OutputDir SCIE-Port/artifacts/heavy-rebalance-gpu
python SCIE-Port/scripts/Render-WideShatterPreview.py SCIE-Port/artifacts/heavy-rebalance-gpu --assembly SCIE-Port/artifacts/build/SCIEPort.dll --combat
python SCIE-Port/scripts/Compare-AbyssSpacePackage.py SCIE-Port/artifacts/荒野探险家-0.61.1.netmod SCIE-Port/artifacts/荒野探险家-0.62.0.netmod --metadata-only
```

Android7723的触屏长按/拖动、第一/第三人称实际斩痕、手机帧率，真实房主/普通客户端、延迟/丢包/乱序、多人同时重击、换武器/死亡/掉线/取消与回执恢复均**待验证**。首次手机测试使用新建可丢弃世界和备份。旧0.61.1包和回退分支保留；只本地main提交，不自动推送或发布。
