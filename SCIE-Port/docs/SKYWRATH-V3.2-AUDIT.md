# 苍穹之怒 V3.2 源码与 ABI 审计

审计日期：2026-10-26

目标程序集：`E:\SCNET\SCNETx25.01.17\Survivalcraft.exe`，AssemblyVersion `2.4.0.0`，SCNET API `1.42`，SC `2.4.10.6`，目标框架 `net472`。

## 原生伤害链证据

- `Game.ComponentMiner.Hit` 在命中概率通过后调用静态 `ComponentMiner.AttackBody(...)`，一次原生挥击只进入一次该入口。
- `AttackBody` 先执行真实 Hook `ModLoader.AttackBody(..., ref float attackPower, bool isMeleeAttack)`，随后依次执行 `ComponentClothing.ApplyArmorProtection`、`ComponentLevel.ResilienceFactor`、`attackPower / ComponentHealth.AttackResilience` 和 `ComponentHealth.Injure`。
- `ComponentHealth.Injure(float, ComponentCreature, bool, string)` 只在非 Client 权威端写生命，并广播 `ComponentHealthPackage`。`NetInjure` 在有效伤害后触发 `Attacked(ComponentCreature)`，因此反伤可以获得真实攻击者。
- 目标程序集没有元素伤害枚举或独立雷电伤害 API。V3.2 的物理 40 继续走完整 `AttackBody` 链；雷电追加 25 和反伤 5 使用 `Injure(power / AttackResilience, ...)`，尊重无敌、死亡状态、权威端和目标内在 `AttackResilience`，但作为元素段明确绕过衣甲与 `ComponentLevel.ResilienceFactor`。它是“攻击点数等价值”，不是未经折算直接扣 5% 生命。
- 雷电追加延迟到下一权威更新，因此原生物理段先完成；诊断开关开启时分别记录 physical/lightning/retaliation 的攻击者、目标、标称点数和生命前后值。反伤期间有递归闸，同一次有效受击只触发一次。

## 角色、武器与天空渲染证据

- `ComponentHumanModel` 暴露 `m_bodyBone/m_headBone/m_hand1Bone/m_hand2Bone/m_leg1Bone/m_leg2Bone` 与 `AbsoluteBoneTransformsForCamera`；`SubsystemModelsRenderer` 在模型绘制后调用真实 `OnModelRendererDrawExtra` Hook。V3.2 在该 Hook 中将骨骼位置由 camera space 还原到 world space，绑定多个 `GlowPoint` 和结构化电弧，并使用低强度 `ComponentModel.EmissionColor` 保留皮肤/衣服/护甲可辨识度。
- 本地第一人称不会绘制第三人称人体模型，故第一人称保留武器核心发光而不在镜头内生成身体光球；第三人称与其他已加载玩家使用骨骼光效。卸装、死亡或停止绘制超过三帧会移除 GlowPoint 并恢复原始 emission。
- 武器仍使用已验证的 1604 三角 COLLADA、1024 diffuse/glow、256 effects 和 2048×512 技能图集。HUD 复用正式图集中的两枚既有高质量符文图标，没有用临时纯色资源替换。
- 真实 Hook `ChangeSkyColor` 与 `ViewFogColor` 分别将天空渐变到深靛蓝/冷紫、雾色渐变到冷暗蓝。每个大招以 CastId 独立记录并合成 blend；结束只移除自己的状态，不修改天气、时间、地形或全屏遮罩。
- `SubsystemGlow`、模型 emission、附加混合粒子和模型渲染 Hook 均已在目标程序集确认。未发现可安全复用的 Bloom、动态点光源、云层材质扰动、后处理或专用镜头震动接口；V3.2 不臆造这些能力。`CameraShake` 配置被保存但当前不执行镜头偏移，真机前不宣称镜头震动已完成。

## 技能与联机

- HUD 恰好两个按钮：雷霆万钧（12 秒）与万象雷劫（55 秒）；原生攻击键不变，被动不占按钮。
- 雷霆万钧由权威端枚举半径 9 内目标并用 HashSet 去重：0–4 格 110，4–9 格线性降到 70。视觉使用 8/10/12 条确定性放射电弧、分叉、中心爆发和两层雷波，不写地形。
- 万象雷劫以施法中心 24 格为搜索半径，在权威端快照最多 24 个已加载、存活的生物；玩家目标服从原生 FriendlyFire 开关。洞穴/地形遮挡不改变这个超自然锁敌规则，客户端也不做二次筛选。
- 每个锁定目标获得独立警告与错峰 130 点雷击；5.7 秒触发中心 220/外围 100、半径 7 的主雷，随后四次每秒 18 点余震。事件携带 `CastId`、`ServerTime`、`TargetId`、位置和阶段序号，客户端只演出。
- 客户端按 `CastId + Event + TargetId + Step` 去重权威演出事件；0.28.1 使用专属包 243（242 保留给载具激活），与旧错误载荷不兼容，所有联机参与端必须统一使用 0.28.1。
- 当前服务端仍限制同一 Project 同时只有一个万象雷劫；客户端天空实现仍支持多个 CastId 合成和独立回滚，以防重复/迟到事件污染天空。

## 画质与生命周期

- `SCIEPortStormhammerVfxQuality=High|Medium|Low` 控制人物挂点数、电弧频率、爆发粒子和远景闪电；超预算只削减视觉，不影响伤害。
- `SCIEPortReducedLightningFlashes` 降低人物轮廓亮度；`SCIEPortStormhammerDiagnostics` 控制伤害日志。配置通过真实 `SaveSettings/LoadSettings` Hook 保存。
- 所有粒子有固定数量与寿命；人物/武器 GlowPoint 有上限并按帧过期；电弧随机种子由权威世界坐标导出，不依赖各客户端帧号。

## 尚需真机验证

- Android 7723 高/中/低档帧时间、发热、频闪舒适度和两个按钮的多点触控。
- 第一/第三人称手持姿态、其他玩家全身雷光、卸装/死亡/断线/切世界清理。
- 物理与雷电两段、固定 5 点反伤、技能所有数值的实际生命条和日志结果。
- 主机、普通客户端、观察者的 CastId 去重、目标位置、迟到事件和天空恢复。
- 当前没有 Android 设备或双端会话证据，以上项目均为“待验证”，不得因编译通过标记为运行通过。
