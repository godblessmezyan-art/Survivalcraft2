# SCNET x25.01.17 大型载具武器开发规范

本文是 `SCIE-Port` 中坦克、装甲车、舰炮、列车炮、飞艇武器等大型载具攻击功能的强制开发规范。目标运行时固定为 Survivalcraft NET `Net_x25.01.17`、Android 7723、net472。

新增或修改大型载具武器前，必须同时阅读：

- 仓库根 `AGENTS.md`；
- `SCIE-Port/AGENTS.md`；
- `docs/COMPATIBILITY.md`；
- `docs/VEHICLE-MODEL-GUIDE.md`；
- 本文。

## 1. 已确认的事故与根因

SCIEPort 0.9.1 坦克 v0.3 在 Android 实机出现两个缺陷：炮塔不随第三人称视角转动，第一炮在车体内停止并炸伤坦克。

目标 x25.01.17 程序集反编译确认，原版火枪的发射链为：

`ComponentInput` → `ComponentMiner.Aim` → `SubsystemMusketBlockBehavior.OnAim` → `SubsystemProjectiles.FireProjectile` → `SubsystemBulletBlockBehavior.OnHitAsProjectile`。

原版火枪从玩家眼睛附近生成弹丸，并把玩家 `ComponentCreature` 作为 owner。`FireProjectileNet` 会从预定炮口前方反向检测 owner 的 `ComponentBody.BoundingBox`，随后把弹丸起点校正到玩家包围盒外。该机制能防止手持武器击中射手自己。

大型载具不能原样套用这个步骤。骑手通常位于载具碰撞箱内部，而载具炮口已经位于骑手包围盒和载具外部。若仍把骑手作为 `FireProjectile` 的 owner 参数，原生手持武器校正可能把安全炮口拉回骑手附近，即大型载具碰撞箱内部。下一帧的 `SubsystemBodies.Raycast` 会命中载具，爆炸监听器继而在车体附近引爆。

结论：

> 可以复用原版火枪的瞄准射线、`BulletBlock`、弹丸运动、命中处理、声音、烟雾思路、后坐和伤害归属，但不得默认复用它针对玩家身体的炮口起点校正。

## 2. 必须遵守的炮口安全规则

### 2.1 炮口以载具碰撞体为基准

不得使用 `body.Position + direction * 固定常数` 作为大型载具的最终弹丸起点。载具尺寸、炮塔偏航和炮管俯仰变化后，固定距离可能仍在碰撞箱内。

最低要求：

1. 取得载具真实 `ComponentBody.BoundingBox`。
2. 从炮管枢轴或车体内已知安全点沿最终炮管方向计算射线离开 AABB 的第一个正距离。
3. 在出口外增加安全净距；当前坦克使用 `0.75` 格。
4. 安全净距必须大于原生弹丸 body raycast 的膨胀量并保留浮点误差余量。
5. 炮口计算必须使用最终受限后的炮塔偏航/炮管俯仰，不能一边显示限位后的炮管、一边按未限位触屏射线发射。

若载具以后改为多个碰撞体，必须对全部自有碰撞体求最远出口，或在弹丸碰撞过滤层明确排除发射载具；不能继续只检查主 Body。

### 2.2 不得让原生 owner 校正覆盖安全炮口

对当前单人坦克，已验证的实现顺序为：

```csharp
Vector3 muzzle = CalculateSafeMuzzlePosition(direction);
ComponentCreature owner = player.Entity.FindComponent<ComponentCreature>(true);
Projectile shell = subsystemProjectiles.FireProjectile(
    shellValue,
    muzzle,
    direction * shellSpeed,
    Vector3.Zero,
    null);

if (shell != null)
{
    shell.Owner = owner;
}
```

这里的 `null` 只用于避免 `FireProjectileNet` 按骑手包围盒改写起点；创建成功后立即恢复 `Projectile.Owner`，让原生弹体伤害、统计和命中归属仍指向驾驶员。

使用这一模式前必须重新反编译目标版本，确认：

- `Projectile.Owner` 仍可写；
- `FireProjectile` 返回前没有必须依赖非空 owner 的目标弹体 `OnFiredAsProjectile` 行为；
- owner 在第一次弹丸更新和碰撞前已经恢复；
- 当前功能明确为单人模式。

多人模式不得直接沿用“创建后补 owner”。网络包可能在 owner 恢复前已入队，造成远端无 owner、伤害归属不同或客户端/服务端弹丸不一致。多人阶段必须设计服务器权威的发射请求、炮口复算、owner 实体 ID、弹丸生成与爆炸同步。

### 2.3 必须设置引信保险

爆炸弹不能在创建后立即具备完整爆炸能力。至少记录真实弹丸起点，并在移除/命中时检查飞行距离或飞行时间。

当前坦克规则：

- 最小解锁距离：`1.5` 格；
- 爆炸格中心到发射坦克当前 AABB 的最小距离：`8` 格；
- 未达到解锁距离：允许弹丸消失，但禁止产生爆炸；
- 仅当两项距离都达到：按既定压力和燃烧标记处理爆炸。

第二项是 Android 二次反馈后增加的爆炸伤害包络。高速弹丸可能在一个物理步内越过 1.5 格，
所以“从炮口飞得够远”不能单独证明爆点不会覆盖大型车体。距离必须针对 `AddExplosion` 实际采用的
整数爆炸格中心计算，避免浮点弹丸位置在包络外、取整后的爆点却回到包络内。

引信保险是第二道防线，不能代替正确炮口计算。若日志出现“未解锁炮弹爆炸被抑制”，应视为需要调查的安全事件，而不是正常发射结果。

## 3. 炮塔、摄像机和触屏规则

骑乘状态下，玩家 `ComponentCreatureModel.EyeRotation` 不一定跟随第三人称活动摄像机旋转。大型载具炮塔不得只依赖骑手模型眼睛方向。

单人触屏推荐规则：

1. 每帧用 `player.GameWidget.ActiveCamera.ViewDirection` 更新炮塔目标，使玩家可以看到炮塔持续转动。
2. `PlayerInput.Hit` 出现的开火帧，优先使用实际触屏射线，兼容非分屏触控模式下点击屏幕不同位置。
3. 将世界瞄准向量转换为车体局部偏航与俯仰。
4. 对炮塔转角和炮管俯仰应用机械限位。
5. 从限位后的角度重新构造最终世界发射方向。
6. 用同一组 `TurretYaw`/`GunPitch` 同时驱动骨骼显示、保存字段和弹丸方向。

模型前方约定必须和代码一致。本项目载具通常以模型 `-Z` 为前方，对应目标 Engine 的 `Matrix.Forward`；不得仅根据 DAE 观感猜测正负号。必须用目标 `Engine.Media.ModelData.Load` 验证骨骼层级，再在 Android 实机分别测试正前、左、右、向上和向下瞄准。

## 4. 原版火枪逻辑的复用边界

可以优先复用：

- `PlayerInput.Hit`/`Aim` 提供的触屏射线；
- `BulletBlock` 及原生弹丸渲染；
- `SubsystemProjectiles` 的弹道、地形/body raycast 和命中回调；
- `SubsystemBulletBlockBehavior` 的弹丸命中效果；
- `Audio/MusketFire`，前提是声音风格适合当前原型；
- 水下禁射、冷却、后坐、烟雾和噪声的设计模式。

必须按载具重写或重新验证：

- 炮口位置；
- owner 起点校正；
- 自有载具碰撞过滤；
- 炮塔/炮管骨骼方向；
- 爆炸触发条件与引信；
- 后坐施加对象和力度；
- 多人权限与同步；
- 弹药库存、装填和射速。

不得因为调用了原版 `FireProjectile` 就宣称武器安全或 Android 兼容。

## 5. 爆炸生命周期规则

监听 `ProjectileRemoved` 并无条件爆炸是不安全的，因为弹丸可能由于近车体碰撞、区块状态、生命周期结束或其他移除路径消失。

实现爆炸弹时至少保存：

- 发射者/伤害归属；
- 初始位置；
- 是否已经解锁引信；
- 如 API 可得，实际命中原因或目标。

只有满足明确爆炸条件时才能调用 `SubsystemExplosions.AddExplosion`。实体移除、退出世界和组件卸载时必须解除事件订阅并清理弹丸状态表，避免跨世界回调和内存泄漏。

若原生投射物会反弹，不要把 `ProjectileStoppedAction.Disappear` 当成“命中即消失”。目标
`SubsystemProjectiles` 会先运行 `OnHitAsProjectile`，随后按命中面反转并衰减速度，只有速度足够低后
才执行 stopped action。需要触碰即爆炸的炮弹应使用独立方块行为，在首次碰撞回调中记录准确的命中格、
立即排队爆炸并把弹丸标记移除；同时从所有权状态表删除该弹丸，让稍后的 `ProjectileRemoved` 不能重复爆炸。
`DisintegratesOnHit=TRUE` 应作为第二道数据层保险，但不能代替爆点、安全包络和重复触发管理。
目标实现即使在同一原生更新尾部计算了一次不再使用的反射速度，也必须保证弹丸在下一帧绘制/模拟前移除；
验收标准是没有可见二次轨迹、没有第二次碰撞、没有重复爆炸。

爆炸测试必须使用可丢弃的新建单人世界，禁止直接在玩家主存档测试。

## 6. 自动化与静态测试最低要求

每种大型载具武器至少增加以下离线测试或构建守卫：

- 正前方炮口位于车体 AABB 外；
- 左右方向炮口位于 AABB 外；
- 最大上仰和最大下俯时炮口位于 AABB 外；
- 安全净距大于 body raycast 膨胀量；
- 小于引信距离不爆炸，边界及以上允许爆炸；
- 爆点位于发射载具安全包络内时不爆炸，包络边界及以上才允许爆炸；
- 冷却期间不能重复发射；
- 深水/禁射状态不能发射；
- 视觉炮管方向与计算发射方向使用同一角度状态；
- 目标版本存在 `ActiveCamera.ViewDirection`、`Projectile.Owner`、`FireProjectile` 和所需爆炸 API；
- 组件移除时解除所有弹丸事件订阅。

编译和静态测试只能标记为“已确认（源码/编译）”，不能替代 Android 实机结论。

### 6.1 爆炸强度与装甲耐久必须分别设计

爆炸强度不能靠观感猜测。若需求要求对齐原版炸药，应从目标渠道包的 `BlocksData` 取证，
并记录具体方块、字段和内容包哈希。固定 x25.01.17 Windows 包
`Content.scpak`（SHA-256 `13A86223C791D23093D9DF15D9E424F8ADF5BEB0FE1BE8DFB7AC62ECC07E6C34`）
中的 `DefaultExplosionPressure` 为：小型桶 60、中型桶 360、大型桶 2160。当前坦克主炮 v0.12.2
明确采用大型桶的 2160，并继续调用原生 `SubsystemExplosions.AddExplosion`，让地形抗爆、方块破坏、
粒子、声音和归属规则仍由游戏实现。更换渠道包后必须重新取证，不能沿用该数值结论。

“载具无限耐久”和“乘员免伤”是两件事。若载具设计为不可损坏，优先让其实体模板不包含
`ComponentDamage`，并用构建守卫禁止回归；不要在每帧把生命值写满，因为更新顺序可能在回填前
已经移除实体。移除 `ComponentDamage` 不会保护乘员，也不会阻止爆炸冲量，因此强爆炸仍必须保留
炮口外置、引信距离和发射载具安全包络，并只在可丢弃世界测试。

## 7. Android 验收顺序

安装前删除旧的同 `PackageName` 包并冷启动游戏。只使用可丢弃的单人创造世界，按以下顺序测试：

1. 不开火，转动第三人称摄像机，确认炮塔持续跟随且车体不被带着旋转。
2. 正前方空旷区域发射第一炮，确认弹丸来自炮口且坦克不受伤、不爆炸。
3. 分别向左、右、上、下发射，确认炮口、视觉炮管和弹道一致。
4. 紧贴墙壁发射，确认近距离弹丸最多消失，不在车体处爆炸。
5. 在 2 格以上距离命中地形，确认弹丸正常爆炸。
6. 连续点击验证冷却，确认不会一帧多发。
7. 驾驶中、倒车中、坡面、车体旋转后重复方向测试。
8. 保存退出并重进，确认炮塔角度字段、车辆实体和事件订阅正常。
9. 检查日志中的未解锁引信抑制记录；若空旷发射也出现，必须回到炮口计算排查。

结果必须分别记录为：已通过编译、已在 Windows 运行、已在 Android 实测、已通过多人联机测试。未执行的层级必须标记“待验证”，多人未实现则标记“不支持”。

## 8. 提交前检查表

- [ ] 已按目标 x25.01.17 实际程序集核对完整原版武器调用链。
- [ ] 已明确列出复用的原版逻辑和重写的载具逻辑。
- [ ] 炮口由载具真实碰撞体计算，不是固定距离猜测。
- [ ] 原生 owner 起点校正不会把弹丸拉回载具内。
- [ ] 弹丸在首次物理更新前已恢复正确伤害 owner。
- [ ] 视觉炮管和实际弹道使用同一限位角度。
- [ ] 爆炸弹具有近车体引信保险。
- [ ] 事件订阅在实体移除时解除。
- [ ] 离线规则测试、ABI 检查、Release 构建和 `.netmod` 校验通过。
- [ ] Android 第一炮、五向瞄准、贴墙、远距离命中和保存重载均有独立记录。
- [ ] 未把单人结论外推为多人同步成功。

## 9. 当前参考实现

- `Features/Tanks/ComponentTankDrive.cs`
- `Features/Tanks/TankRules.cs`
- `tests/SCIEPort.Tanks.Tests/Program.cs`
- `docs/TANK-V0.2-AUDIT.md`
- `docs/API-DIFF.md`

参考实现是当前 x25.01.17 单人阶段的证据，不是未来版本的永久 ABI。升级游戏版本或 7723 渠道包后必须重新取证。
