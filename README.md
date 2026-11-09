<div align="center">

# 🌌 Survivalcraft 2.4 · 生存战争模组开发计划

### 从自由建造，到工业探索，再到神话与未知世界。

**Survivalcraft NET / SCNET x25.01.17 · Android · C# · .NET Framework 4.7.2**

[![Game](https://img.shields.io/badge/Survivalcraft-2.4-2D70B7?style=flat-square)](https://github.com/godblessmezyan-art/Survivalcraft2)
[![Platform](https://img.shields.io/badge/Platform-SCNET%20%7C%20Android-4A946D?style=flat-square)](#-运行环境与兼容性)
[![Development](https://img.shields.io/badge/Development-Active-7450A4?style=flat-square)](#-项目矩阵)

[项目总览](#-项目矩阵) · [特色内容](#-特色内容) · [安装与构建](#-安装与构建) · [开发文档](#-开发文档)

</div>

---

## ✨ 项目简介

这是一个围绕 **《生存战争 2.4》（Survivalcraft 2.4）联机版** 持续开发的模组工程集合。

最初，它只是一个让玩家能够通过 JSON 蓝图自动建造房屋的工具。现在，它逐步延伸出 **自动建筑、工业与载具、神话神器、召唤生物、世界遗迹** 等独立方向。

我们的目标并非简单增加物品数量，而是让每一种工具、每一把武器、每一处遗迹，都能为生存战争带来新的游玩体验。

> **项目边界：**建筑助手、荒野探险家、遗迹探索保持独立工程及产物。云天书典属于荒野探险家的内置模块，两者共用版本与安装包。各工程的构建和实机验证状态见对应测试记录。

## 🧭 项目矩阵

| 项目 | 简介 | 当前开发基线 | 入口 |
|:--|:--|:--|:--|
| 🏡 **Builder Assistant · 建筑助手** | 蓝图导入、自动建筑、区域编辑、家具与生成工作台 | **v0.24.0** | [详细文档](docs/BUILDER-ASSISTANT.md) · [源码](src/BuilderAssistant/) |
| 📚 **云天书典 · Celestial Codex** | 云库与本地TXT/EPUB、触屏阅读、离线缓存与本机续读 | **荒野探险家0.81.0内置；实机待验证** | [整合与使用](SCIE-Port/docs/CELESTIAL-CODEX.md) · [Android HTTP修复](docs/CELESTIAL-CODEX-HTTP-0.1.4.md) |
| ⚔️ **荒野探险家 · Wilderness Explorer** | 工业移植、载具、神话神器、四圣兽、召唤物与云天书典 | **v0.81.0** | [项目介绍](SCIE-Port/README.md) · [源码](SCIE-Port/src/SCIEPort/) |
| 🏛️ **Survivalcraft Ruins · 遗迹探索** | 在新生成的地形中加入神殿、废墟、险境、守卫与战利品 | **SCNET 移植工程** | [移植说明](SurvivalcraftRuins-Port/README.md) · [独立仓库](https://github.com/godblessmezyan-art/SurvivalcraftRuins) |
| 🌋 **World Expansion · 世界扩展** | 新地貌与生态、探索区域及模组共存策略 | **规划中，非已发布模组** | [开发规范](AGENTS.md) |

<sub>上述版本是仓库文档记录的开发基线，不等同于已公开发布的稳定安装版本；最新情况以各模块 README、提交与测试记录为准。</sub>

## 🎮 特色内容

### 🏡 01 / Builder Assistant — 让想象中的建筑落入世界

**从手动摆放数千方块，走向蓝图驱动的自动建造。**

- **蓝图系统：**支持 JSON 蓝图、剪贴板导入、保存与载入本地蓝图库、旋转和落点预览。
- **云端蓝图库 V1.0（Builder Assistant 0.25.0）：**网站管理员上传/发布，游戏切换本地或云端列表，刷新、下载、SHA-256/revision 校验与离线保存；沿用既有材料、预览、施工、撤销和权限流程。Windows 编译/离线检查与 Android、桌面游戏及联机实测分开记录，详见[专项说明](docs/BUILDER-ASSISTANT-CLOUD-V1.md)。
- **参数化生成：**使用建筑工作台生成小屋、两层住宅和自动布置家具的一室一厅住宅。
- **建筑主题：**大型奇幻树屋「森之王庭」、猫头鹰树屋及其他蓝图示例。
- **施工能力：**单事务最多 **10,000 方块**，分批施工、清障模式、撤销与回滚。
- **材料与家具：**v0.24.0 文档记录 **81 种原版材料**及 **10 种结构块家具模板**，支持细分的方块状态约束。
- **移动端优先：**为触屏使用设计工作流程，减少对 PC 专用操作的依赖。

**阅读更多：**[建筑助手完整历史与使用说明](docs/BUILDER-ASSISTANT.md) · [蓝图格式规范](docs/BLUEPRINT-SPEC.md) · [AI 生成蓝图提示词](docs/AI-BLUEPRINT-PROMPT.md) · [方块能力清单](docs/blueprint-block-reference.md)

### ⚔️ 02 / 荒野探险家 — 将工业、神话与奇幻带入生存战争

**Wilderness Explorer** 是位于 `SCIE-Port/` 的独立移植及扩展工程，内部仍使用 `SCIEPort` 标识，正式构建包使用 **`荒野探险家.netmod`**。

云天书典永久作为其中的内置模块，安装这一份包即可。两部分今后直接在本地 `main` 统一开发、版本和交付，不再生成独立书典包；完整规则见 [开发规范](AGENTS.md)。

**神器与权柄**

| 神器 / 系统 | 体验方向 |
|:--|:--|
| ⚡ **雷神之锤 · Mjölnir** | 雷霆爆发、领域演出与神威降临 |
| 🔱 **昆古尼尔 · Gungnir** | 神枪投掷、卢恩加护与命运裁决 |
| 🌊 **海神三叉戟 · Poseidon** | 海洋权柄、潮汐领域与水元素演出 |
| 🌑 **阿比斯 · Abyss** | 虚空与死亡权柄、空间位移、终焉领域 |
| 🗡️ **圣剑 · Excalibur** | 黄金圣光解放与阿瓦隆「遥远的理想乡」 |
| 📖 **神器图谱** | 神器收藏、真名解放及按玩家保存的解锁进度 |

- **四圣兽装备：**青龙、白虎、朱雀、玄武各有独立的造型、能力设计与物品栏主题。
- **冥界军势：**冥界猎犬、夜魇战马、夜魇骑士、骨龙、刻尔柏洛斯等召唤候选；包含契约、收纳、行动模式与骑乘相关开发。
- **载具系统：**越野车、双人载具、坦克、直升机、快艇、移动堡垒等方向；各载具的模型完整度和实机验证状态并不相同。
- **更多内容：**植物与生态资源、创造栏独立分类、「全知之眼」「吞噬之眼」、悬浮物品陈列装置与永久星门等功能。

**近期主线：**`v0.81.0` 合入云天书典本地TXT/EPUB书库及PixelFX效果，保留现代家居和普通玩家同步修复。构建与离线检查有记录；**Android 真机和真实多人联机效果仍需验证**。

**阅读更多：**[荒野探险家完整更新记录](SCIE-Port/README.md) · [神器图谱](SCIE-Port/docs/ARTIFACT-CODEX.md) · [阿比斯](SCIE-Port/docs/ABYSS-REAPER.md) · [测试结果](SCIE-Port/docs/TEST-RESULTS.md)

### 🏛️ 03 / Survivalcraft Ruins — 让探索重新充满未知

不再只是翻山越岭，而是在旅途中发现真正值得进入的地点。

- **23 种探索结构 / 14 个生成槽位**（含同槽随机变体），涵盖沙漠神殿、丛林神庙、海底神殿、天空堡垒、斗兽场、废弃矿井、沉船与更多小型遗迹。
- **确定性世界生成：**根据世界种子、地形温湿度、海水深度与结构间距选择位置。
- **机关与奖励：**陷阱、电路、守卫、宝箱、战利品池与不同程度的遗迹风化。
- **兼顾旧存档：**预期仅对新生成的区块放置遗迹，不主动重写已探索区域。
- **独立构建：**SCNET 移植目标为 `SurvivalcraftRuins.netmod`；不能将 SCAPI 的 `.scmod` 直接改名使用。

本仓库中的 [`SurvivalcraftRuins-Port/`](SurvivalcraftRuins-Port/) 负责 SCNET 适配；[独立的 SurvivalcraftRuins 仓库](https://github.com/godblessmezyan-art/SurvivalcraftRuins) 保留原项目开发内容。**Windows 编译、ABI 与离线测试不等同于手机端的实际生成与联机测试。**

### 🌋 04 / World Expansion — 下一片未知大陆

**规划方向，尚未作为可运行模组交付。**

探索更丰富的地貌与生态，包括火山等特色区域，同时尽量保持与遗迹生成、工业内容、建筑助手和其他 Mod 的兼容。此部分目前属于未来设计目标，**请勿将其理解为已经实现或可以下载的功能**。

## 📱 运行环境与兼容性

| 项目 | 说明 |
|:--|:--|
| 主要游戏版本 | **Survivalcraft 2.4 · SCNET Net_x25.01.17** |
| 目标设备 | **Android**，重点关注 7723 渠道客户端 |
| 开发与构建环境 | Windows / C# / `.NET Framework 4.7.2` 目标程序集 |
| SCNET 安装包 | **`.netmod`**，由各模块独立编译 |
| SCAPI 插件 | `.scmod` 为另一套运行时产物，**不可混装或直接改后缀** |
| 多人联机 | 房主与客户端需满足游戏对 Mod 列表、版本与资源一致性的校验 |

> ⚠️ **验收口径：**本项目严格区分「已确认」「待验证」「不支持」。Windows 编译成功、静态模型预览、离线测试通过，并不代表 Android 7723 客户端已成功加载、能够正确渲染或完成多人同步。安装新构建前请备份世界，优先在测试存档中验证。

## 🛠️ 安装与构建

### 玩家安装

1. 确认游戏版本与目标 Mod 的说明一致。
2. 获取**对应模块实际构建完成**的 `.netmod` 包，放入客户端使用的 `NetMods` 目录；具体位置以游戏/安装渠道为准。
3. 启动游戏检查模组列表；多人联机时，房主和其他玩家需使用一致的模组与资源版本。
4. 首次测试请使用新建世界或已备份的测试存档，不要直接拿唯一的主存档试验开发包。

**注意：**本仓库不是统一安装包；不能将不同目录中的源码或未完成的构建产物直接混合打包。是否提供可直接下载的成品，请以仓库实际 Release/交付记录为准。

### 开发者构建（PowerShell）

在仓库根目录执行，先准备好合法取得的 **SCNET x25.01.17 游戏程序集**，将示例路径替换为本机路径：

```powershell
$gameDir = "C:\path\to\SCNET-x25.01.17"

# 建筑助手
.\scripts\Build-NetMod.ps1 -GameDir $gameDir -Configuration Release

# 荒野探险家
.\SCIE-Port\scripts\Build-NetMod.ps1 -GameDir $gameDir -Configuration Release

# 遗迹 SCNET 移植
.\SurvivalcraftRuins-Port\scripts\Build-NetMod.ps1 -GameDir $gameDir -Configuration Release
```

各模块自行执行必要的依赖检查、构建与打包校验。**请分别阅读其 README 与测试记录，不要假定三份脚本的依赖和产物完全相同。**构建过程不应提交或再分发游戏 DLL、APK、专有资源和未经许可的第三方素材。

## 📂 仓库结构

```text
Survivalcraft2/
├── src/BuilderAssistant/       # 建筑助手源码
├── SCIE-Port/                  # 荒野探险家：工业、载具、神器与扩展
│   ├── src/SCIEPort/
│   ├── docs/
│   └── scripts/
├── SurvivalcraftRuins-Port/    # 遗迹模组 SCNET 专项移植
├── blueprints/                 # 建筑蓝图资源
├── docs/                       # 建筑、兼容性及工程文档
├── scripts/                    # 根工程测试、构建与打包脚本
├── tests/                      # 建筑助手相关离线测试
├── AGENTS.md                   # 开发约束与安全原则
└── README.md                   # 你正在阅读的项目总览
```

## 📚 开发文档

| 主题 | 文档 |
|:--|:--|
| 建筑助手完整功能说明与历史记录 | [Builder Assistant](docs/BUILDER-ASSISTANT.md) |
| 构建、开发与测试 | [开发指南](docs/DEVELOPMENT.md) · [测试说明](docs/TESTING.md) |
| 原版方块与蓝图 | [蓝图规范](docs/BLUEPRINT-SPEC.md) · [方块支持表](docs/blueprint-block-reference.md) |
| SCNET ABI 与运行时 | [兼容性矩阵](docs/COMPATIBILITY.md) · [SCNET 适配记录](docs/SCNET-X25-PORT.md) |
| 荒野探险家更新历史 | [SCIE-Port README](SCIE-Port/README.md) |
| 神器、军势、装备、载具设计 | [荒野探险家文档目录](SCIE-Port/docs/) |
| 荒野探险家构建与验收 | [测试记录](SCIE-Port/docs/TEST-RESULTS.md) |
| 遗迹结构、安装和已知问题 | [遗迹移植说明](SurvivalcraftRuins-Port/README.md) |
| 开发规范与第三方来源说明 | [AGENTS.md](AGENTS.md) · [Third-party notices](SCIE-Port/THIRD-PARTY-NOTICES.md) |

## 🗺️ 接下来要做什么

- **体验闭环：**把各模块的离线测试结论逐步转换为 Android 真机、存读档和多人联机的实际验收记录。
- **内容打磨：**继续完善神器特效、载具交互、骑乘与召唤体验，优先解决玩家可见的功能和视觉问题。
- **建造体验：**改善蓝图库、参数化生成、落点预览、复杂方块与安全撤销。
- **探索系统：**验证遗迹的生成密度、箱子、陷阱及房主/客户端同步，再推进世界扩展设计。

## 🤝 关于项目

这是一个面向 **Survivalcraft / SCNET** 的独立爱好者开发项目，并非游戏官方作品。项目由 **zyan** 持续维护；部分内容涉及第三方模组的授权移植和再创作，具体来源、许可与可分发范围请阅读 [第三方声明](SCIE-Port/THIRD-PARTY-NOTICES.md) 及各子项目文档。

**欢迎交流创意、报告问题、分享测试结果。**实际功能状态以源码、文档和对应版本的验证记录为准。

---

<div align="center">

**Build something extraordinary. Explore beyond the familiar.**

*让生存战争的下一段冒险，由我们自己创造。*

</div>
