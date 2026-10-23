# 荒野探索者 X1（Wilderness Explorer X1）摩托外观 V2 模型审计

版本：SCIE-Port 0.23.0　|　目标客户端：Survivalcraft NET x25.01.17（Android 7723）　|　日期：2026-10-24

本文是 v0.23.0 摩托视觉 V2 重构的交付审计：只改外观资产与生成/校验脚本，不新增载具功能，不触碰
Block 923、`SCIEPortATV` 实体 GUID、双座、驾驶、联机权威与存档逻辑。

## 1. 交付物与路径

| 交付物 | 路径 | 说明 |
| --- | --- | --- |
| 生成脚本 | `scripts/New-AtvAssets.ps1` | 参数化几何核 + X1 装配，确定性产出模型与贴图 |
| 预览脚本 | `scripts/Render-AtvPreviews.ps1` | 离线正交软件光栅器，7 视角 ×（白模/彩色） |
| 上游恢复脚本 | `scripts/Restore-UpstreamFromPackage.ps1` | 从自有旧包还原 gitignore 的授权第三方资产（环境修复） |
| 模型 | `src/SCIEPort/Assets/Models/ATV.dae` | Y_UP、单 identity 场景节点、13 个独立 geometry、变换烘焙 |
| 贴图 | `src/SCIEPort/Assets/Textures/ATV.png` | 64×64 不透明材质图集 |
| 打包断言 | `scripts/Build-NetMod.ps1`（ATV 段） | 泛化几何/索引/UV/面数预算/贴图规格 + 目标 Engine 导入 + 包围盒 |
| 验收预览 | `artifacts/previews/atv-x1/`（gitignore） | 14 张 PNG，供人工目检 |
| 旧资产备份 | `artifacts/backups/atv-pre-x1/ATV.v1.dae`、`ATV.v1.png`（gitignore） | 重构前 12 部件版本 |
| 产物 | `artifacts/SCIEPort.netmod` | 496,289 字节，SHA-256 见 §6 |

## 2. 部件清单与面数

几何全部由可复用生成器构造（`Frustum` 圆柱/锥、`Tube` 管状折线、`Loft` 等截面放样、`Torus` 环面、
`ArcPlate` 弧板、`BoxA` 轴对齐盒），不以堆叠小立方体模拟圆；所有面法线由内部参考点自动校正朝向。

| # | geometry 名 | 三角面 | 主要构造 |
| --- | --- | --- | --- |
| 1 | BikeFrontWheel | 600 | 16 边闭合格放样轮胎 + 16 交错胎块 + 轮辋 + 6 辐条 + 12 边轮毂 + 8 边轴 |
| 2 | BikeRearWheel | 600 | 同上，半径/宽度略大，挂独立摇臂 |
| 3 | BikeFrontFork | 172 | 双倾斜前叉管 + 下滑筒 + 前轴 + 上三角夹 |
| 4 | BikeHandlebar | 220 | 5 点管状车把 + 立管 + 握把 + 仪表盒 |
| 5 | BikeHeadlight | 60 | 方形灯壳 + 支架 + 镜片 + 双琥珀转向豆 |
| 6 | BikeFrame | 348 | 真三角管状车架（脊管/下管/摇篮/斜撑/侧管/副车架/货架横管） |
| 7 | BikeEngine | 252 | 曲轴箱 + 12 边气缸 + 3 散热片 + 缸头 + 下护板 + 侧盖 |
| 8 | BikeFuelTank | 60 | 5 截面 6 边放样，前宽后窄多面楔形 |
| 9 | BikeSeat | 60 | 5 截面放样连续双座，尾部微翘 |
| 10 | BikeSidePanels | 24 | 左右侧护板 |
| 11 | BikeExhaust | 136 | 右侧头段管 + 铬消声器锥 + 上翘尾嘴 |
| 12 | BikeSwingarm | 660 | 双摇臂管 + 横梁 + 枢轴 + 双减震（各 3 圈橙色环面弹簧） |
| 13 | BikeFenders | 160 | 前/后弧板挡泥 + 尾板 + 红色尾灯 |
| 合计 | 13 | **3352** | 移动端预算 6000，构建断言强制上限 |

## 3. 渲染限制符合性（SCNET）

- 骨骼：目标 `Engine.Media.ModelData.Load(Stream)` 反射实测 **1 bone / 13 meshes**，低于七骨骼实例
  着色器上限；车轮不引入额外场景骨骼。
- 场景：`visual_scene` 仅一个 `name="ATV"` 节点，`matrix` 为单位阵；全部变换烘焙进顶点。
- 朝向：`up_axis=Y_UP`，前进 = -Z，X 左/右，Y 上；`unit meter=1`。
- 索引：每 geometry 单一 `<triangles>`，VERTEX/NORMAL/TEXCOORD 三输入、offset 0/1/2、set 0，
  交错索引长度 = 面数 × 9，索引范围逐值校验。
- UV/法线：逐 geometry 校验 accessor 步长 3/3/2、浮点个数 = 顶点数 × 步长、UV ∈ [0,1]。

## 4. 包围盒与碰撞兼容

模型包围盒（构建断言实测）：X [-0.371265, 0.371265]、Y [0.003218, 1.187]、Z [-1.286782, 1.286397]。
完全落入既有 `ComponentBody BoxSize = 1.1,1.25,2.6`（允许 X±0.55 / Y0–1.25 / Z±1.3），因此**不需要**
任何碰撞尺寸改动；骑乘锚点（0,0.9,-0.3 与 1.1,0.4,-0.3 的 Mount 配置）保持不变。

## 5. 贴图与材质分区

64×64、Format32bppArgb、全不透明（构建断言逐像素 A=255）、33 种颜色（断言上限 96）。
4×4 网格共 16 个区域，每区域 1-texel 内缩防渗色：橙漆/橙贴花（银条纹）/碳黑/橡胶胎纹/拉丝银/钢/
座椅粒面/铬/灯镜/红/琥珀/前叉银/握把环/轮辋辐条/深橙护板/备用底。v=0 对应 PNG 顶行（XNA 约定）。

## 6. 编译与打包结果

- `scripts/Test-Environment.ps1 -GameDir D:\SC`：通过（260 原版方块常量、ABI、保留 ID 检查）。
- `scripts/Build-NetMod.ps1 -GameDir D:\SC -Configuration Release`：全程 exit 0；
  `TreatWarningsAsErrors` + `GenerateDocumentationFile` 下 0 warning / 0 error；DATABASE VALIDATION PASSED；
  ATV 段全部新断言通过（含目标 Engine 1 bone/13 meshes 与包围盒）。
- 产物：`artifacts/SCIEPort.netmod`，**496,289 字节**，
  SHA-256 `0934145603B1FADE38F94D493D49455DF50D3193AA42FB577A015727E7C78F35`；包内 `modinfo.json`
  Version=0.23.0，`Assets/Models/ATV.dae` 与 `Assets/Textures/ATV.png` 均为本版资产。
- 环境备注：打包需 SDK 9.0.300（仓库 `global.json` 钉版），经 `DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet`
  + `DOTNET_ROLL_FORWARD=Major` 提供；本工作区 gitignore 的 `upstream/SCIE/` 授权资产曾被清空，已用
  `Restore-UpstreamFromPackage.ps1` 自用户自有旧包还原（见 TEST-RESULTS 0.23.0）。

## 7. 验收预览结论（离线）

`Render-AtvPreviews.ps1` 输出 front/rear/left/right/front45/rear45/top 各白模+彩色共 14 张。目检结论：
左视可识别完整侧轮廓（辐条轮、前叉、车把、方灯、楔形油箱、三角车架、发动机+护板、连续双座、后减震
橙弹簧、右侧排气、前后挡泥）；front45/rear45 体积感与材质分区清晰；top 视左右对称、排气仅右侧、
油箱前宽后窄、双座连续；无缺面、无错位、无翻转导致的黑面。预览仅为离线光栅近似，**不替代真机验证**。

## 8. 剩余待验证（7723 真机）

编译与离线断言通过**不等于** Android 兼容。以下必须在 7723 客户端新世界实测后才能改判「已确认」：

1. 物品栏图标：13 部件烘焙进 `BlockMesh` 后的缩略图是否清晰、无黑块/缺面。
2. 世界实体渲染：单骨骼实例路径在真机着色器下是否正常显示、有无骨骼数告警。
3. 多角度辨识度：近/中/远距离、昼夜光照下橙/碳黑/银分区是否可读。
4. 双人乘坐：两名玩家同乘时模型与骑姿锚点视觉是否合理、无穿插异常。
5. 运动态：行驶、转向、上下坡时车轮/摇臂/排气管无脱节或抖动。
6. 存档往返：放置→退出→重进后模型与贴图一致。
7. 联机：主机/普通客户端两侧显示一致（本次未改同步代码，仅视觉回归）。
