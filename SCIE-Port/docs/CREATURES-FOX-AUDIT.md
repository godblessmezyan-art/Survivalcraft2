# SCIE-Port 狐狸移植审计（第一阶段）

审计日期：2026-10-21。目标仅为 Survivalcraft NET 2.4
`Net_x25.01.17`、Android 7723、`.NET Framework 4.7.2`、`.netmod`。
本阶段只提供手动物品生成，不添加自然生成，也不宣称多人联机兼容。

## 参考基线与许可

| 仓库 | 固定提交 | 许可与使用范围 |
|---|---|---|
| `XiaofengdiZhu/SurvivalcraftTemplateModForAPI` | `a8a9f09aea2ede595b2d677f10907772a2a716c3` | 仓库 `LICENSE` 为 MIT；本轮仅研究 XDB、模型路径和动画配置，没有复制 SCAPI DLL 或源码。 |
| `XiaofengdiZhu/AdvancedGltfFoxModForSC` | `82c1ea541751045804968a71216f9153e2dd400e` | 仓库 `LICENSE` 为 MIT；本轮仅研究自定义模型组件和动画控制器依赖，没有复制源码、DLL 或 GLB。 |

模板仓库的 `Fox.gltf` 内嵌版权字段明确写明 PixelMannen、tomkranis、
AsoboStudio/scurest，并标注 **CC BY 4.0**。因此它不能只按仓库 MIT
处理。高级示例的 `Fox2.glb` 没有内嵌版权或原始素材链接，仓库也没有 README
说明第三方来源；在来源链未补齐前不进入发行包。

关键审计文件：

- `SurvivalcraftTemplateModForAPI/Assets/BaseGltfFoxDatabase.xdb`
- `SurvivalcraftTemplateModForAPI/Assets/Animations/BaseGltfFox.json`
- `SurvivalcraftTemplateModForAPI/Assets/Models/BaseGltfFox/Fox.gltf`
- `AdvancedGltfFoxMod/Assets/AdvancedFoxDatabase.xdb`
- `AdvancedGltfFoxMod/Assets/Animations/AdvancedFox.json`
- `AdvancedGltfFoxMod/Components/ComponentAdvancedFoxModel.cs`
- `AdvancedGltfFoxMod/Assets/Models/Fox2.glb`

参考副本只保存在被 Git 忽略的 `artifacts/reference-creatures/`。固定文件哈希见
`THIRD-PARTY-NOTICES.md`。

## 目标程序集取证

固定目标程序集继续由 `scripts/Test-Environment.ps1` 校验：

- `Survivalcraft.exe` SHA-256
  `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`
- `Engine.dll` SHA-256
  `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`
- `EntitySystem.dll` SHA-256
  `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`
- `Content.scpak` SHA-256
  `13A86223C791D23093D9DF15D9E424F8ADF5BEB0FE1BE8DFB7AC62ECC07E6C34`

反编译与反射确认：

- `SurvivalCraftModEntity` 只注册 DAE、JSON、OBJ 模型读取器，没有 glTF/GLB
  读取器。
- `ComponentCreature.Load` 强制取得 `ComponentBody`、`ComponentHealth`、
  `ComponentSpawn`、`ComponentCreatureSounds`、`ComponentCreatureModel` 和
  `ComponentLocomotion`。
- `ComponentFourLeggedModel.SetModel` 强制查找 `Body`、`Head`、`Leg1`、
  `Leg2`、`Leg3`、`Leg4`，`Neck` 是唯一可选骨骼。
- 原生四足步态不是导入动画片段，而是按速度、接地、水浸、攻击、进食和死亡
  状态逐帧设置上述骨骼变换。
- `SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)` 使用
  `DatabaseManager.CreateEntity` 创建模板实体，设置位置、朝向和
  `ComponentCreature.ConstantSpawn`，再加入 Project。
- `ComponentCreature.Save` 会保存 `ConstantSpawn`；本实体另将
  `Spawn.AutoDespawn=False`，用于手动生成后的存档恢复验证。

## 为什么不能直接移植参考 XDB/glTF

`BaseGltfFoxDatabase.xdb` 面向 SCAPI 1.9，包含目标
`ComponentFourLeggedModel.Load` 不读取的 `AnimationConfigPath`，并使用大量
并非 x25.01.17 数据库对象的示例 GUID。高级示例则依赖 SCAPI 提供的
`AnimationController`、自定义 glTF 模型组件和行为组件；这些类型不在固定目标
程序集内。直接复制会同时产生加载器 ABI、数据库继承和模型读取失败风险。

本轮重写为：

- XDB 根容器为独立 `<SCIEPortCreatures>`；原版 `Entities`、
  `BlockBehaviors` 文件夹使用固定目标的真实 GUID。
- `SCIEPortFox` 继承固定目标原版 `Wolf` 实体 GUID
  `49a9845c-3810-4092-ba53-df213d10d563`。
- 复用 Wolf/LandAnimal/AICreature 的真实原版 AI 组件链，不复制参考项目的
  自定义行为类。
- 覆盖狐狸的显示名、碰撞体、质量、速度、生命值、群组名和模型参数。
- 不声明 `CreatureEggData`，避免伪造或占用原版蛋索引；使用独立 ID 927
  `FoxSpawnBlock` 手动生成。
- 普通 `WorkType.Client` 不直接创建不同步实体；本阶段没有实现客户端请求/
  服务器确认协议。

## 模型与动画方案

`Assets/Models/Fox.dae` 和 `Assets/Textures/Fox.png` 是本项目从零创建的资源，
由 `scripts/New-FoxAssets.ps1` 确定性生成，不是参考 glTF/GLB 的转换副本。

- Y_UP，6 bones / 13 cuboid meshes。
- 骨骼严格为 `Body`、`Head`、`Leg1`、`Leg2`、`Leg3`、`Leg4`。
- 目标 `Engine.Media.ModelData.Load` 已实际导入为 6 bones / 13 meshes。
- 32×8、4 色、全不透明 PNG；UV 分别落在橙、浅色、黑、深棕色条带。
- 行走、快跑、观察、攻击、进食和死亡使用目标原生程序化骨骼动画。
- glTF 中的动画 clip 不会被保留；若以后必须保留关键帧，需要为 SCNET 新增
  经过 Android 验证的动画读取与混合系统，而不是打包 SCAPI DLL。

## 当前验证状态

| 项目 | 状态 | 证据 |
|---|---|---|
| 上游 SHA、仓库 LICENSE | 已确认 | 本地 `git rev-parse` 与仓库 LICENSE |
| 第三方狐狸素材来源 | 已确认/部分待验证 | 模板 glTF 明确 CC BY 4.0；高级 GLB 原始来源待验证，均未打包 |
| 目标动物/四足/生成 ABI | 已确认 | 固定程序集反编译 + `Test-Environment.ps1` 反射 |
| XDB 文件夹与继承 GUID | 已确认（静态） | 固定 `Content.scpak` 原版 Database.xml + `Test-Database.ps1` |
| DAE 骨骼和网格导入 | 已确认（离线） | 目标 `Engine.Media.ModelData.Load` 得到 6/13 |
| net472 Release 编译 | 已确认 | `Build-NetMod.ps1 -Configuration Release`，0 error |
| `.netmod` 内容与禁带 DLL | 已确认 | 构建脚本包清单、PNG、XDB、模型和禁带依赖检查 |
| Windows 游戏加载/生成/AI/存档 | 待验证 | 按要求未在本机启动游戏 |
| Android 7723 | 待验证 | 需要真机新世界验收 |
| 多人联机 | 不支持（第一阶段） | 没有请求、权限、复制或纠正协议 |

## Android 真机验收清单

1. 备份后新建可丢弃的单人创造世界，安装唯一一份最新
   `SCIEPort.netmod`。
2. 在“生成蛋”分类找到“SCIE 狐狸生成器”；验证图标不是空白或黑块。
3. 对平地使用一次，确认只生成一只狐狸，模型朝向合理且没有竖直、缩放或
   贴图错误。
4. 观察静止、走动、转向、自动跳跃、逃跑/追逐、进食、嚎叫、受伤和死亡；
   重点检查四条腿是否围绕正确关节摆动。
5. 在水边、半格地形、一格台阶、狭窄通道和实体附近观察碰撞，确认不穿地、
   不长期悬空、不发生模型与碰撞体明显分离。
6. 退出世界并重新进入，确认狐狸仍存在且没有复制、幽灵实体或
   `Database not loaded`。
7. 检查日志中是否出现缺失 `Models/Fox`、`Textures/Fox`、骨骼名、实体模板或
   ValuesDictionary 参数的异常。
8. 不把联机主机/客户端表现记为通过；联机阶段另行设计并测试。
