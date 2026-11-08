# 荒野探险家（含云天书典）统一项目与版本规范

**生效：2026-11-08。唯一代码仓库：`godblessmezyan-art/Survivalcraft2`；唯一默认开发分支：`main`。**

## 一、产品边界

- 「荒野探险家 / Wilderness Explorer」是对玩家发布的唯一主产品；「云天书典 / Celestial Codex」是其阅读功能模块，不再拥有独立的后续发行版本。
- 所有涉及两者的源码改动直接合入 `main`，用功能目录管理内部边界，不创建平行产品分支。
- 最终发行目标：一个 `PackageName=SCIEPort` 的正式 .netmod、一个产品版本、一个对应的 GitHub Release，用户只需要安装一份文件。
- 现有 `src/CelestialCodex/` 和 `SCIE-Port/` 尚为不同 csproj / DLL / modinfo / 构建流程，**现阶段只是规范统一，实际合包尚未完成**。不得把直接放两个 .netmod 到同一 ZIP 叫作合并。

## 二、兼容性优先级

1. 荒野探险家保留 `SCIEPort` 的 PackageName / AssemblyName / 旧存档归属；游戏版本 `SCNET Net_x25.01.17`，目标 Android 7723 / net472 不变。
2. 云天书典沿用已有的物品编号 956/DATA0、阅读 UI、本地 TXT/EPUB、Digital-Study API、用户目录和续读数据，避免修改旧版协议与数据模型。
3. 集成时检查所有 CSV 方块条目、XDB 数据库模板及 GUID、ModLoader 初始化入口、类名/资源路径、装载顺序、程序集引用、第三方授权声明。
4. 验证独立旧版卸载和统一版安装的迁移步骤、旧世界存档、旧书签和缓存；升级不能损坏玩家存档。冲突时停止发布并记录失败原因。
5. Android 必须检查界面与文本阅读、联网/离线、资源大小、内存与系统 .NET BCL 差异；联机必须分别检查单机/房主/普通客户端，不把个人书籍数据上传到房间。

## 三、版本与 Release

- 集成完成后，使用 `SCIE-Port/src/SCIEPort/modinfo.json` 作为统一**发行版本**真值，`SCIEPort.csproj` 同步；云天书典不再以自己的版本号单独触发发布。合包迁移时应处理遗留的独立版本元数据，但在验证完成前不得随意改动现有包的内部身份。
- 后续每发布一个更新版本都要在 GitHub Releases 提供一个 `wilderness-explorer-vX.Y.Z` Release。Release 发布必须对应 `main` 的明确提交与版本号；旧 `v0.x.y` 和 `celestial-codex-v0.2.0` 均保留历史。
- **今后不创建只有源码 ZIP 的新 Release。** 只要构建成功并生成 `.netmod`，无需运行测试、安装包内容校验和 SHA-256 比对，即可直接上传 GitHub Release。未做 Android/联机实测的版本必须标记为未经测试的 prerelease。
- `.github/workflows/release-wilderness-explorer.yml` 使用 Windows 自托管 Runner 调用快速发布脚本，只执行编译、资源打包、上传，**不检查 `docs/UNIFIED-RELEASE-READY`、不运行测试、不审计 .netmod 结构**。没有产出文件则不创建 Release。
- 禁止只因为源码已合并到 `main` 或文档改了就声称已经产出可安装的统一模组。

## 四、迁移实施步骤（待完成）

1. 核对独立项目的加载器入口、包资源和游戏程序集 ABI，输出冲突清单。
2. 在 SCIEPort 主项目中集成书典 Core / UI / Networking / Mod，以及必要 XML/CSV/图片资源与第三方依赖；一个包只保留一份 modinfo 和正式运行入口。禁止无目标 ABI 依据地靠重命名 DLL 合并。
3. 为单包补全测试：书典网络/本地阅读、旧读书缓存、全部 SCIE 既有模块、Block/GUID/网络包唯一性与隔离。
4. 真实 Windows Release 构建、包展开逐项核对、Android 7723 全流程及联机验证，记录包 SHA-256、安装升级和回滚。
5. 为自动发布配置 `scnet-x25` Windows 自托管 Runner 与 `SCNET_GAME_DIR` 仓库变量；更新版本并推送 main 后，直接上传 `荒野探险家-X.Y.Z.netmod`。当前快速路径仅打包现有 SCIEPort 代码，尚不包含未合入该工程的书典；后续完成真实合包后自动一同打包。

## 五、不在此次范围

Builder Assistant 和 Ruins 暂不并入此统一产品；它们的内部版本与发布流程维持现状，除非用户另行要求。

## 2026-11-09 统一源码接入进度（未实机验证）

从荒野探险家 0.79.0 开始，`SCIEPort.csproj` 直接编译书典目录的 C# 源码，快速和全量打包脚本把书典 Assets 合并到单份 SCIEPort .netmod；仅使用主产品的 modinfo.json，不嵌入第二份 .netmod。云天书典 ID956 和本地图书导入窗口均按此路径接入。**这是代码与打包方案的整合，缺少目标 x25.01.17 实际编译/加载及 Android/联机验证**。早期描述“尚无整合”指 0.79.0 之前的历史状态，不能再用于当前源码状态。
