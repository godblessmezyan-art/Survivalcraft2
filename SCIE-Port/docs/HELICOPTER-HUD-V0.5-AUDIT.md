# 双人直升机移动端飞行控制 HUD（v0.27.0）审计与开发记录

日期：2026-10-25　分支：`feature/helicopter-hud`　目标引擎：SCNET x25.01.17 / SC 2.4（Android 7723）

本文件遵循 `docs/VEHICLE-QUALITY-STANDARD.md`：编译/离线测试与真机验证分别记录，绝未验证项一律标注"待验证"，不以"能运行/编译通过"充当完成。

## 一、任务目标

为双人直升机开发移动端专属飞行控制 HUD，重点提供独立的上升、下降按键，并与既有飞行/座次/网络路径融合，不替换任何已验证逻辑。

## 二、来源审查（反射实证，x25.01.17 真实程序集）

所有控件类型位于 `Game.*`（`Survivalcraft.exe`），而非猜测的 `Engine.Widgets.*`。关键结论（均经反射逐一确认公共可见性）：

- `Game.Widget`：`DesiredSize{get;set}`、`Margin`、`HorizontalAlignment`/`VerticalAlignment`（`WidgetAlignment` 枚举值仅 `Near/Center/Far/Stretch`）、`ActualSize{get}`、`IsVisible`、`IsHitTestVisible`、`IsEnabled`、`ParentWidget{get;set}`。
- `Game.ContainerWidget`：`AddChildren(Widget)`/`RemoveChildren(Widget)`/`ClearChildren()`/`AllChildren`，无公开 `Children` 集合属性。
- `Game.CanvasWidget : ContainerWidget`：`SetPosition(Widget, Vector2)` 是**静态**方法（易踩坑）。
- `Game.ClickableWidget : Widget`：`IsPressed{get;set}` 公开，引擎按 touchId 路由，天然支持持续按压与多点触摸——这是"按住即持续输入"的正规通道，非模拟单击。
- `Game.BevelledRectangleWidget : Widget`：`RoundingRadius/RoundingCount/CenterColor/BevelColor/ShadowSize/Size`，程序化绘制圆角半透明面板，无需纹理。
- `Game.ImageWidget : CanvasWidget`：`Texture/SubTexture/RotateAngle/ColorTransForm`；配合 `Game.Subtexture(Texture2D,Vector2,Vector2)` 复用同一张箭镞贴图（`RotateAngle` 0=▲、π=▼）。
- `Game.ButtonWidget`：**无公开构造函数**，故不继承，改为组合 `BevelledRectangleWidget + ImageWidget + ClickableWidget`。
- `Game.ComponentGui`：`ControlsContainerWidget{get}` 即本地玩家控件层根容器（与原生移动摇杆 `MoveWidget`、视角区 `LookWidget`、快捷栏 `ShortInventoryWidget` 同层）。
- `Game.ComponentPlayer`：`GameWidget`、`GuiWidget`（此 `GuiWidget` 已是 `ContainerWidget`，无 `.ControlsContainerWidget` 子成员——首版接错处，已修正）。
- `Game.SettingsManager.UIScale`：**公开静态** `Single`，作为 UI 缩放来源，`Project` 上无 SettingsManager 实例属性。
- 内容加载沿用 `HelicopterBlock` 既有范式：`ContentManager.Get<Texture2D>("Textures/HeliArrow")`。

## 三、实现分层

1. 纯逻辑层 `Features/AirVehicles/Hud/HelicopterControlSurface.cs`（零引擎依赖，可离线单测）：
   - `HelicopterVerticalIntent{Hover,Climb,Dive}`、`HelicopterButtonRect`、`HelicopterButtonLayout`。
   - `ResolveIntent(buttonClimb,buttonDive,viewClimb,viewDive,jumpClimb,sneakDive)`：专用按键优先于视角通道，按键/键盘双按对冲归 Hover，无按键时回退视角俯仰，全释放稳定 Hover。
   - `ShouldShowControls`：仅本地主玩家 + 驾驶位 + 载具存活时可见，乘客/远端副本/销毁态隐藏。
   - `ComputeLayout/ButtonSize/ClampUiScale`：右下角堆叠（上升在上、下降在下），短边驱动基准尺寸，`MinButtonSize=96/MaxButtonSize=190` 夹取，`VerticalGapFactor/RightMarginFactor/BottomMarginFactor` 分数边距，避开左侧摇杆与底部快捷栏，随 `UIScale` 缩放并 `MinimumUiScale=0.7` 兜底。
   - `ToVerticalFlags`：把意图映射回既有 `HelicopterFlightRules.GetVerticalTarget` 的 ascend/descend 布尔，物理内核零改动复用。
2. 控件层 `Features/AirVehicles/Hud/HelicopterFlightHud.cs`：
   - 每按钮组合 `CanvasWidget 根 + BevelledRectangleWidget 面板 + ImageWidget 箭镞 + ClickableWidget 命中层`；一次性构建，之后仅切 `IsVisible` 与定位，尺寸/坐标仅在解析几何变化时才写入 `DesiredSize/SetPosition`（避免逐帧重建打断引擎活动触摸）。
   - 按压反馈：`IsPressed` 翻转时改 `CenterColor`（常态半透明深色、按下高亮蓝）。
   - 生命周期：`Attach(container)` 换容器先干净 `Detach`；`Refresh` 读 `container.ActualSize` 重排；隐藏/`Detach` 清 `IsPressed`，杜绝幽灵按压跨会话。
   - 7723 安全：所有控件/内容调用包 `try/catch`，失败仅记 `Log.Error` 并保持 HUD 隐藏、按钮不可读——回退到已验证的视角升降通道，绝不让弹窗/控件异常打断驾驶循环（与 `VehicleActivationService` 同风格）。
3. 接入层 `ComponentHelicopterDrive.cs`：
   - 新增 `m_hud`、`m_hudDriver` 字段与 `using ...Hud`。
   - `Update` 每帧据 `localPilot/authorizedPilot/driver` 调 `UpdateFlightHud`：授权本地驾驶位 attach+refresh，其余 hide+detach 并清 `m_hudDriver`。
   - 读取 `hudClimb/hudDive` 传入 `SimulateFlight`；`SimulateFlight` 内以 `ResolveIntent` 统一按键/视角/跳跃/潜行六路输入，再 `ToVerticalTarget`；顶点钳制、水平/偏航/入水损伤等逻辑原样保留。
   - `Dispose` 先 `m_hud.Detach()` 再释放旋翼音效，保证下机/销毁/断线时还原原生 UI。

## 四、素材

`Assets/Textures/HeliArrow.png`：透明底 64×64 白色上指箭镞（PowerShell System.Drawing 绘制，9px 圆头笔画，顶点 (32,16) 两侧 (15,40)/(49,40)）；下降按钮用 `RotateAngle=π` 复用同图。已在 `Build-NetMod.ps1` 必需清单加入该 `.png`，缺失即打包失败。

## 五、编译与离线测试结果（已确认）

- 主工程 Release 编译：`dotnet msbuild SCIEPort.csproj /p:Configuration=Release /p:SCNetGameDir=D:\SC` → **成功，0 warning / 0 error**（首轮 3 处编译错误已修：GuiWidget 容器类型、静态 SetPosition、缺 XML 注释与 cref）。
- 直升机规则离线单测：`SCIEPort.AirVehicles.Tests` → **73/73 断言通过**（新增意图仲裁、可见性、响应式布局共 29 条 HUD 断言，含双按对冲、按键优先、下机隐藏、尺寸夹取、间隙/边距、UIScale 夹取）。
- 打包守卫：`Build-NetMod.ps1` 新增三文件存在性 + 源码字符串断言（意图枚举/ResolveIntent/ComputeLayout；BevelledRectangleWidget/ImageWidget/ClickableWidget/m_hit.IsPressed/Textures/HeliArrow/异常回退；驱动 Attach/Refresh/IsClimb/IsDive/ResolveIntent/Detach），回归即失败。

> 上述"已确认"仅代表编译与纯逻辑离线测试通过，不代表真机表现。

## 六、真机待验证项（未验证，绝不标为通过）

1. Android 7723 上 HUD 是否真正渲染在驾驶位控件层、圆角半透明与箭镞朝向是否正确。
2. 单指按住上升/下降是否产生持续升降；左摇杆 + 右双键双手并用是否互不干扰（多点触摸）。
3. 松开是否平滑悬停（阻尼、非锁定世界 Y）；两键同按是否归零悬停。
4. 上机自动显示、下机/死亡/断线/载具销毁是否干净还原原生 UI，无残影、无幽灵按压。
5. 不同分辨率/横屏/`UIScale` 下边距与安全区是否合理，是否与快捷栏/菜单重叠。
6. 双人联机：仅授权驾驶员出按键并可操控；乘客无按键不可控；垂直输入经主机校验、随既有一元位置包同步，切换驾驶员清除旧输入、无抖动/重复。
7. 存档重载后 HUD 与飞行状态一致。
8. 保存/加载/断开连接无泄漏（控件与命中层释放）。

## 七、取舍与说明

- 未新增子系统/工程模板：控件与输入完全由本地驾驶位 `ComponentHelicopterDrive` 拥有并驱动，复用既有已验证网络路径与授权门（`VehicleActivationService`、`ComponentTwoSeatManager`），把 7723 ABI 与 GUID 风险降到最低。
- 载具"存活"不查 `ComponentDamage`（其无公开血量/IsDead）：销毁即卸载组件走 `Dispose→Detach`；进水损伤触发驾驶员离机即隐藏 HUD，语义等价且不依赖未证实符号。
- 若真机证实 `ImageWidget.RotateAngle` 非绕中心旋转：仅需调 `m_image.Margin` 偏移量或改用两张预旋贴图，纯逻辑层与飞行内核均不受影响。
