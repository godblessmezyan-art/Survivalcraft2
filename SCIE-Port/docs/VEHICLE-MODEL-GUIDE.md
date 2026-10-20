# SCNET x25.01.17 载具模型从零制作指南

本文用于在 `SCIE-Port` 中从零制作新的静态载具模型和贴图。目标环境固定为：

- Survivalcraft NET 2.4 `Net_x25.01.17`
- Android 7723 客户端
- `.NET Framework 4.7.2`
- `.netmod` 包

ATV 0.8.3 是当前已通过 Android 世界显示验证的最小参考实现：

- 生成脚本：`scripts/New-AtvAssets.ps1`
- 模型：`src/SCIEPort/Assets/Models/ATV.dae`
- 贴图：`src/SCIEPort/Assets/Textures/ATV.png`
- 物品绘制：`Features/LandVehicles/AtvBlock.cs`
- 世界实体：`Assets/Database/LandVehicles.xdb`
- 自动校验：`scripts/Build-NetMod.ps1`

## 1. 开始前必须确认

1. 阅读仓库根 `AGENTS.md`、`SCIE-Port/AGENTS.md` 和兼容性文档。
2. 从 `main` 创建独立 `codex/` 分支，不在 Builder Assistant 中放置载具资源。
3. 确认模型和贴图为项目原创、允许再分发或已有明确授权，并在
   `THIRD-PARTY-NOTICES.md` 记录来源、范围和 SHA-256。
4. 新增物品 ID 前扫描原版及 SCIE-Port 已占用 ID；外部 Mod 冲突仍需提示。
5. 只引用 `SCNetGameDir`/`SCNET_GAME_DIR` 中的目标程序集，不复制或提交游戏 DLL。

## 2. 最重要的渲染限制

目标 x25.01.17 的 `Game.ModLoader.GetMaxInstancesCount()` 默认返回 `7`。
`SubsystemModelsRenderer` 会把 `ComponentModel.AbsoluteBoneTransformsForCamera.Length`
作为实例变换数量提交给模型着色器。因此，一个世界实体模型导入后的骨骼数不得超过该预算。

对不需要骨骼动画的载具，项目规范比引擎上限更严格：

> 所有静态部件使用一个根骨骼；可以有多个 geometry/mesh，但只能有一个场景 node。

ATV 0.8.2 曾把九个部件写成九个并列场景 node。目标 Collada 导入器会额外创建一个空根骨骼，
最终得到 `10 bones / 9 meshes`。物品栏仍能显示，因为物品代码把网格烘焙进 `BlockMesh`；
世界实体使用实例模型着色器，Android 中表现为完全看不到模型。0.8.3 改为一个根 node 后，
目标 Engine 的真实导入结果为 `1 bone / 9 meshes`，Android 世界显示验证通过。

不要用“DAE 文件能解析”“物品栏有图标”代替世界模型验证。

## 3. 坐标系与尺寸

- DAE 必须声明 `<up_axis>Y_UP</up_axis>`。
- X 为左右，Y 为高度，Z 为前后。
- 建议载具前方使用负 Z；驱动代码应再以目标 `ComponentBody.Matrix.Forward` 实测确认。
- `<unit name="meter" meter="1"/>`，顶点直接使用游戏格单位。
- 根节点矩阵必须为 identity：

```xml
<matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
```

- 缩放、旋转和平移全部烘焙进顶点，避免桌面与 Android 导入器行为差异。
- 模型最低点通常放在 `Y=0` 附近；`ComponentBody.Position` 是碰撞盒底部中心。
- 可见模型必须完整落在 `Body/BoxSize` 内。碰撞盒过小会穿模，过大会造成无法靠近或骑乘。

从 Z_UP 资源转换时不能只改 `<up_axis>` 文本，必须同时变换 positions 和 normals。
现有项目使用的绕 X 轴转换是 `(x,y,z) -> (x,z,-y)`；转换后要重新计算包围盒并检查正反面。

## 4. 推荐 DAE 结构

每个视觉部件保留独立 geometry，便于 UV、审计和以后重建，但在 `visual_scene` 中全部挂到同一个根 node：

```xml
<library_geometries>
  <geometry id="VehicleBody-mesh" name="VehicleBody">...</geometry>
  <geometry id="VehicleWheelFL-mesh" name="VehicleWheelFL">...</geometry>
</library_geometries>

<library_visual_scenes>
  <visual_scene id="Scene" name="Scene">
    <node id="Vehicle" name="Vehicle" type="NODE">
      <matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
      <instance_geometry url="#VehicleBody-mesh" name="VehicleBody"/>
      <instance_geometry url="#VehicleWheelFL-mesh" name="VehicleWheelFL"/>
    </node>
  </visual_scene>
</library_visual_scenes>
```

每个 geometry 至少包含：

- POSITION，三维 float；
- NORMAL，三维 float；
- TEXCOORD，二维 float；
- `<triangles>`；
- 正确的交错索引，且索引不越界。

禁止事项：

- 不要为每个静态车轮、座椅或车把建立独立场景 node。
- 不要保留 Blender 的非 identity 节点缩放或旋转。
- 不要只验证 XML 节点数量，必须用目标 `Engine.Media.ModelData.Load` 检查实际导入结果。
- 不要为了“双面可见”复制一套完全重合的反向三角形；闭合网格应使用一致绕序和正确法线。
- 不要假定 mesh 名在单根节点多 geometry 后仍保持 geometry 名。目标导入器以父骨骼名命名 mesh，
  因此多个 mesh 可能全部名为根节点名。

## 5. 贴图与 UV

- 运行包内使用 PNG；不要使用尚未被目标 Android `Texture2D` 验证的 WebP。
- 优先使用尺寸小、完全不透明的 RGBA/RGB 调色板贴图。
- UV 必须落在有效 `[0,1]` 范围内。
- 使用色带时，UV 取色带中心，不取边缘，避免 PointClamp 或缩放时串色。
- 透明像素必须是设计要求；不需要透明时，构建脚本应强制 Alpha=255。
- 世界实体必须在 XDB 的 `SimpleModel` 中明确写 `TextureOverride`，不要只在自定义组件
  `Load()` 中临时赋值。组件加载顺序可能使 `ComponentSimpleModel.Load` 再次覆盖它。
- 低地形光下纯黑并不一定是贴图丢失。确认贴图绑定后，可使用很低的 `EmissionColor`
  作为可见性保底，但不能用高自发光掩盖法线、光照或材质错误。

## 6. 物品栏与世界实体是两条独立路径

### 物品栏

`Block.DrawBlock` 通常把模型网格追加到 `BlockMesh`。单根骨骼模型的多个 mesh 可能同名，
因此应枚举 `model.Meshes`，不要按部件名调用 `FindMesh`：

```csharp
Model model = ContentManager.Get<Model>("Models/Vehicle");
foreach (ModelMesh mesh in model.Meshes)
{
    Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone)
        * Matrix.CreateScale(0.45f);
    foreach (ModelMeshPart part in mesh.MeshParts)
    {
        itemMesh.AppendModelMeshPart(
            part, transform, false, false, false, false, Color.White);
    }
}
```

### 世界实体

世界模型由实体 XDB 的 `ComponentSimpleModel` 加载：

```xml
<MemberComponentTemplate Name="SimpleModel" InheritanceParent="cc5a4e72-0df9-4acf-a42e-ecb5a2a7baf4">
  <Parameter Name="ModelName" Value="Models/Vehicle" Type="string" />
  <Parameter Name="CastsShadow" Value="True" Type="bool" />
  <Parameter Name="TextureOverride" Value="Textures/Vehicle" Type="string" />
</MemberComponentTemplate>
```

诊断时必须分别记录：物品栏是否可见、生成后世界模型是否可见、是否有阴影、碰撞体是否存在。
“图标正常但世界空白”首先检查实际导入骨骼数；“世界纯黑”首先检查 XDB 贴图绑定和光照；
“模型竖直”首先检查是否真正转换了顶点和法线到 Y_UP。

## 7. 碰撞、座位和保存

- 模型网格不提供实体碰撞；碰撞来自 `ComponentBody.BoxSize`。
- `MountOffset` 应位于座椅位置，并确保玩家能进入原生搜索距离。
- `DismountOffset` 必须位于碰撞盒外且有落脚空间。
- 模型根点、碰撞盒底部和生成位置必须一起检查，避免模型埋地或悬空。
- 保留实体模板名、方块 ID 和既有 GUID，才能降低旧世界升级风险。
- 静态外观修复不应顺手改动物理参数、实体名或保存结构。
- `AutoDespawn=False` 只是保存链的一部分，仍需实测退出世界、重进和区块重载。

## 8. 建议的资产生成流程

1. 用参数表描述各部件尺寸、位置和 UV，而不是手写大量重复 XML。
2. 生成独立 geometry，但只生成一个 `visual_scene/node`。
3. 同一脚本生成 PNG，保证模型 UV 与贴图色带同步。
4. 将生成脚本和生成物一起提交；生成物必须可复现。
5. 对 DAE 做 XML 静态检查。
6. 使用目标 `Engine.Media.ModelData.Load` 检查 bones、meshes、buffers 和包围盒。
7. 在打包脚本中加入模型专属断言，不能只靠人工查看。

ATV 参考命令：

```powershell
cd E:\Survivalcraft2\SCIE-Port
.\scripts\New-AtvAssets.ps1
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release
```

## 9. 每个新载具必须具备的自动校验

- DAE 为合法 XML，`up_axis=Y_UP`。
- 只有一个顶层场景 node，矩阵为 identity。
- 所有预期 geometry 均存在并被根节点引用一次。
- positions/normals/UV 数量及 accessor stride 正确。
- triangles 数量和交错索引长度一致，所有索引均在范围内。
- 目标 Engine 实际导入为 `1 bone / N meshes`；静态非动画载具不接受更多骨骼。
- 模型包围盒不超过 `ComponentBody.BoxSize`。
- PNG 签名、尺寸、颜色/Alpha 和格式符合约束。
- XDB 的 `ModelName`、`TextureOverride` 都能映射到包内资源。
- CSV 方块类名、Block ID、XDB GUID 和继承关系无内部冲突。
- 包内没有游戏 DLL、PDB、上游 DLL 或未经许可资源。
- `modinfo.json`、程序集版本和文档版本一致。

## 10. Android 验收顺序

使用新建可丢弃单人世界，并先删除旧包、冷启动游戏：

1. 确认加载版本正确且进入世界无异常。
2. 检查创造栏图标的比例、颜色、UV 和朝向。
3. 生成实体，确认世界模型可见；环绕观察所有方向，并分别测试日光、阴影和室内低光。
4. 对比物品和世界模型是否包含相同部件。
5. 检查模型是否埋地、悬空、竖直、镜像或超出碰撞盒。
6. 测试上车、下车和第三人称视角。
7. 测试碰墙、坡道、台阶、低顶棚、其它实体和落水。
8. 保存退出并重新进入；远离区块后返回；确认实体位置、朝向和外观恢复。
9. 单人通过后再分别测试联机房主和普通客户端。没有服务端权威设计时不得宣称联机兼容。

结果必须分开记录为：已通过编译、已在 Windows 运行、已在 Android 实测、已通过多人联机测试。

## 11. 常见现象快速定位

| 现象 | 优先检查 |
|---|---|
| 物品图标正常，世界模型完全不可见 | 目标 Engine 实际骨骼数、七骨骼预算、世界 `ComponentSimpleModel` 路径 |
| 物品和世界都缺模型 | 资源路径、DAE 解析、包内是否包含文件、模型节点/geometry |
| 世界模型纯黑 | XDB `TextureOverride`、加载顺序、法线、地形光、自发光保底 |
| 模型竖直或躺倒 | 是否只改了 `up_axis` 而未转换 positions/normals |
| 部件位置爆炸或比例异常 | 非 identity 节点矩阵、未应用缩放、单位或父子变换 |
| 只有部分部件显示 | 是否按重复 mesh 名调用 `FindMesh`、根节点是否引用所有 geometry |
| 可见但无法接近/上车 | `BoxSize`、`MountOffset`、原生骑乘搜索距离 |
| 模型埋地或悬空 | 顶点最低 Y、Body Position、生成高度和碰撞盒底部约定 |
| Android 闪退但 Windows 静态检查正常 | 首条设备日志、贴图格式、索引越界、着色器预算和渠道程序集差异 |

## 12. 完成定义

只有以下证据齐全，才能把“Android 载具模型显示”标为已确认：

- 目标程序集环境检查通过；
- Release 编译与 `.netmod` 校验通过；
- 目标 Engine 导入断言通过；
- Android 7723 冷启动后物品栏和世界模型均可见；
- 测试版本、日期、操作和结果已写入 `docs/TEST-RESULTS.md`。

编译成功不等于 Android 兼容；物品栏显示不等于世界实体显示；单人显示正常也不等于多人同步正常。
