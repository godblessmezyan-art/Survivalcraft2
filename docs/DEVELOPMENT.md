# 开发环境与流程

## 必要工具

- Windows 10/11。
- PowerShell 5.1 或 PowerShell 7。
- .NET SDK（当前开发机为 9.0.314；SDK 版本不是目标运行时版本）。
- .NET Framework 4.7.2 Targeting Pack。
- Git。
- SCNET x25.01.17 Windows 发布目录，仅作为本地编译引用。
- Android 文件管理/ADB（二选一）用于设备安装与日志回收。

公开发布来源：<https://github.com/survivalcraft-net/survivalcraft-net/releases/tag/x25.01.17>。

禁止把 `Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll`、APK、PDB、`Content.scpak` 或存档复制到仓库。

## 本地配置

构建脚本按以下顺序寻找游戏目录：

1. `-GameDir` 参数；
2. 环境变量 `SCNET_GAME_DIR`；
3. 本机默认值 `E:\SCNET\SCNETx25.01.17`。

可选设置：

```powershell
$env:SCNET_GAME_DIR = 'E:\SCNET\SCNETx25.01.17'
```

## 验证与构建

```powershell
./scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
./scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
```

构建脚本实际执行 SDK 风格的 `net472` Release 构建，并把 `BuilderAssistant.dll` 与 `modinfo.json` 放在 ZIP 根目录，最终扩展名为 `.netmod`。

手动编译等价命令：

```powershell
dotnet build ./src/BuilderAssistant/BuilderAssistant.csproj -c Release -p:SCNetGameDir='E:\SCNET\SCNETx25.01.17'
```

`artifacts/`、`bin/` 和 `obj/` 都是生成目录，不提交。

## 元数据与包格式

当前 x25.01.17 程序集中的 `Game.ModInfo` 声明以下字段：`Name`、`Version`、`ApiVersion`、`Description`、`ScVersion`、`Link`、`Author`、`PackageName`、`Dependencies`。其中 `Dependencies` 是字符串数组/列表，不是 SCAPI 常见的对象映射。

当前包根目录：

```text
BuilderAssistant.netmod
├── BuilderAssistant.dll
└── modinfo.json
```

不要向包内加入游戏 DLL。

## Android 安装与单人测试

1. 确认安装的是目标 `Net_x25.01.17` 客户端，并记录来源（公开 APK 或 7723）。
2. 启动一次游戏，让客户端创建数据目录；在设置中开启 Mod 加载（若存在该开关）。
3. 完整备份 `Worlds` 和客户端配置。
4. 把 `BuilderAssistant.netmod` 放到该客户端实际使用的 `NetMods` 目录。
5. 新建一个可丢弃的单人世界，不进入重要存档或联机服务器。
6. 启动后检查 Mod 列表以及日志中的 `[BuilderAssistant] Loaded`。
7. 退出并再次进入测试世界，确认存档仍可加载。
8. 保存客户端日志、Mod 列表截图、Android 版本、设备架构以及客户端来源。

若 Android 无法直接访问应用数据目录，使用客户端内置导入功能或 ADB；不要通过重新签名 APK 来“顺便”塞入 Mod，因为这会改变测试变量。

## 7723 包验证所需文件

为确认渠道兼容性，请从你实际安装的 7723 包提供以下之一：

- 原始 APK（仅在你有权用于本地分析时）；或
- 从 APK 提取的托管程序集及其 SHA-256，至少包含 Survivalcraft 主程序集、`Engine.dll`、`EntitySystem.dll`；或
- 客户端日志中完整的版本、API 版本和 Mod 加载异常。

这些文件只用于本地校验，不提交 Git。

## 常见错误

- `MSB3644`：缺少 .NET Framework 4.7.2 Targeting Pack。
- 找不到 `Survivalcraft.exe`：`-GameDir` 指向了 APK、压缩包或错误版本目录。
- `TypeLoadException`：很可能混用了 SCAPI DLL、其他 SCNET 版本或错误命名空间。
- Mod 不出现在列表：检查扩展名、`NetMods` 目录、ZIP 根目录以及 `Dependencies` 是否为数组。
- 能在 Windows 加载但 Android 失败：优先比对 Android 包内程序集，不要先修改接口签名。
- 联机中修改不生效：先确认主机/服务端权威逻辑，不假定客户端地形写入会同步。

