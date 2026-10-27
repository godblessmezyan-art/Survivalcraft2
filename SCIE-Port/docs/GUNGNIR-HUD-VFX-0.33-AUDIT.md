# 昆古尼尔 HUD / 美术特效 0.33.0 审查与真机验收

日期：2026-10-27。目标：SCNET `Net_x25.01.17` / Android 7723；本轮只重构表现层，不改变 ID 948、包 244、协议字段、伤害、范围、冷却、长按投枪、穿透返回或神佑属性。

## 原生 HUD 取证与定位

固定 Windows 程序集 `Survivalcraft.exe` SHA-256 `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`。使用 `ilspycmd 9.1.0.7988` 与游戏自身 `SurvivalCraftModEntity.GetDecipherStream` 只读取证：

- `ComponentGui.Load` 从真实 `Widgets/GameWidget.xml` 取得 `ControlsContainer`、`Look`、`Move`、`MoveRose` 和 `ShortInventory`；目标 XML 的 `LookPadContainer` / `MovePadContainer` 是 160×160 逻辑单位，左右由 `SettingsManager.LeftHandedLayout` 动态镜像。
- `Widget` 公开 `GlobalBounds`、`ActualSize`、`ScreenToWidget`、`WidgetToScreen` 和 `GlobalTransform`。HUD 因此读取实际布局后的全局矩形，再转换到 `ControlsContainer` 本地逻辑坐标，不把像素、DPI 或固定分辨率当作 Widget 坐标。
- `ComponentInput.UpdateInputFromWidgets` 证明 `LookWidget` 同时承担观察与点按跳跃；底层 `ViewWidget` 承担点按攻击/交互、长按挖掘/瞄准。技能控件只有图标本体可命中，字幕、冷却文字、边框、能量条与全屏根容器均不命中，图标外触摸继续交给原生层；未改写 `PlayerInput.Look/Hit/Aim/Jump`。
- 默认右手布局：永恒裁决右缘与 Look 左缘保留 10 单位，卢恩神佑位于其左下；左手布局镜像到 Look 右侧，避免屏幕外定位。快捷栏或其他可见原生命中控件发生碰撞时，两个按钮作为整体以 16 单位步长向上/向外退让，保持相对位置。
- 运行日志只在 Look、快捷栏、视口或左右手布局矩形变化时输出：`viewport`、`look`、`blessing`、`judgment`、`mirrored`、`fallback`、`overlapLook`、`overlapHotbar`、`overlapNative` 与 `hitTarget=iconOnly`，不会逐帧刷日志。

离线布局输出（逻辑坐标；不等同真机截图）：

| 物理/模拟视口 | UI 缩放后的逻辑视口 | 卢恩神佑 | 永恒裁决 | Look/快捷栏重叠 |
|---|---:|---:|---:|---:|
| 1280×720 / 1.0 | 1280×720 | `(962,609.3,72,72)` | `(1022,513.3,88,88)` | false / false |
| 1920×1080 / 1.0 | 1920×1080 | `(1602,969.3,72,72)` | `(1662,873.3,88,88)` | false / false |
| 1280×720 / 1.25 | 1024×576 | `(706,422,72,72)` | `(766,326,88,88)` | false / false |
| 1920×1080 / 1.5 | 1280×720 | `(962,609.3,72,72)` | `(1022,513.3,88,88)` | false / false |

上述检查还覆盖左手 Look 位于 `(0,560,160,160)` 的镜像退让。实际设备安全区、厂商 DPI、分屏和 7723 重打包后的 XML 仍须以运行日志与截图复核。

## 资源与绘制

- `New-GungnirAssets.py` clean-room 生成透明 `GungnirSkills.png`（512×256）和透明 `GungnirRunes.png`（1024×256，8 槽）。八种字形只参考 Fehu、Uruz、Thurisaz、Ansuz、Raidho、Kaunan、Gebo、Algiz 的几何语汇，未导入第三方图片或字体。
- 技能徽记使用透明外部背景、暗色内嵌底、银金多圈拉丝边、刻线与高亮；72/88 逻辑单位仍保留主轮廓。资产守卫检查透明角、尺寸及 8 个字形至少 7 个独立采样签名。
- `GungnirScene` 使用目标已编译通过的 `PrimitivesRenderer3D.TexturedBatch` / `TexturedBatch3D.QueueQuad`、真实 UV、`DepthRead`、`CullNone`；AlphaBlend 刻印层与 Additive 1.42× 柔光层分别批处理。圆环仍用有界 `FlatBatch3D`，不生成发光球。
- 枪身 24 个 `LightRune_*` 网格按 12 段循环计算窄波头与单向衰减尾迹；常态低亮，神佑期逐段传播，最后 2 秒乘剩余时间渐弱。低画质隔段绘制，不再让整组网格使用一个正弦亮度。

## 演出与性能边界

- 神佑：0.9 秒中心扩展地阵；字形按 0.075 秒错峰出现；少量竖直符文上升并以细束汇向第一人称武器附近；持续期保留 5/8 个以内的环绕符文、水平奔跑 1/2/3 条短光带及跳跃脚环；剩余 2 秒自然衰减。
- 裁决：地面三层阵、天空三层差速环与逐字点亮；0.42–1.40 秒真实 `GungnirBlock.Mesh` 从 25 格高度以三次缓入下降，低画质仍保留巨枪；1.40 秒后只有一次 0.18 秒主十字爆闪，三圈错峰冲击波，随后 5/8/12 个字形向外上升并淡出。伤害仍由 `SubsystemGungnir.Judgment` 原时点单次结算，Scene 不查询目标、不写生命、不改地形。
- 高/中/低：圆环段数 64/40/24，动态字形 12/8/5，光带 3/2/1。120 格玩家效果和 150 格裁决中心外不绘制；Slash 容量 24，纹理批次与环境对象在 Load 时复用；退出世界 `Dispose` 清状态/HUD，死亡、卸装和快照超时沿既有状态闸隐藏神佑/飞枪。

## 本轮离线验证

- `Test-Environment.ps1`：固定三程序集、API 1.42、SC 2.4.10.6、net472 与保留 ID/包检查通过。
- Gungnir 105 项（原 88 项 + 四组布局/缩放、逻辑尺寸、右上关系、左手镜像）通过。
- 真实 `Engine.Media.ModelData.Load`：1 bone / 41 meshes / 1176 triangles；透明徽记与八字形图集守卫通过。
- 完整 `Build-NetMod.ps1`：全部既有离线测试通过；net472 Release 0 warning / 0 error；16 份 XDB `DATABASE VALIDATION PASSED`。
- 包：`artifacts/SCIEPort.netmod`，3,091,265 bytes，99 entries，SHA-256 `B451A23802425F83B68B53C7B1E0DE09C6A69FE965B55A8874FB9C7F86295830`；modinfo `0.33.0`，AssemblyVersion `0.33.0.0`。

这些结果是 Windows 静态/离线验证，不是 Windows 游戏内、Android 7723 或联机实测。

## Android 截图与联机验收清单（待执行）

两台设备安装同一包哈希，备份存档后使用可丢弃新单人世界；记录设备型号、Android、7723 安装包程序集哈希、分辨率、系统显示缩放、游戏 UI 缩放、左右手布局和画质。

1. 1280×720、1920×1080（或等比例录屏）及至少两档 UI 缩放，各截一张：HUD 全景、Look/技能局部、快捷栏局部；日志必须 `overlapLook=false overlapHotbar=false overlapNative=false`。
2. 同时按住移动、拖动 Look、点按/长按原生攻击并用另一指点击两技能；确认观察、攻击、跳跃、快捷栏切换均不丢触点，字幕区域点按不施法，非持枪/死亡/切菜单隐藏。
3. 冷却、禁用灰态、按压白金反馈、神佑 15 秒数字/能量条、裁决起手反馈各截图；检查 72/88 尺寸在小屏可读且不形成不透明大面板。
4. 昼/夜各录神佑起势、持续奔跑、连续跳跃、最后 2 秒和结束后 5 秒；检查符文种类可辨、流向是逐段而非整体呼吸、无残留。
5. 昼/夜各录完整裁决：三层地阵、三层天空环、巨枪立体侧视、唯一主爆闪、三层冲击波、解构清理；逐格比对地形未变化，伤害仍只结算一次。
6. 高/中/低各连续施放 10 分钟，记录平均/最差帧时、峰值内存、温度；低档必须仍有关键地阵和巨枪，退出世界重进后无旧符文/HUD。
7. 单人、房主、普通客户端轮换施法并由另一端旁观；核对包 244 状态驱动同一时序、重复/延迟包不重复伤害，换装、死亡、断线、重连、取消/拒绝施法不残留。严格远程移动物理权威仍为不支持，不因本轮视觉改造升级结论。
