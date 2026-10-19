# 依赖与分发权限

## 上游内容

| 项目/文件 | 发现版本 | 许可证据 | 当前处理 |
|---|---|---|---|
| SCIE 源码与资源 | commit `4864d956793b6e670413019f1f6b1cc5c8bb0970` | 固定树无公开许可证文件；用户声明已取得明确许可 | 0.7.0 在既有交通资源外新增 Gear、Wheel、GearMould、WheelMould、Piston、Cylinder 六个模型；五个 Z_UP 模型使用项目内 Y_UP 派生副本，Cylinder 原样为 Y_UP；公开发布前归档权利人许可文本和范围 |
| Kelly Expansion 植物资源 | 发布器 `1.1.0` / 包内 `0.2.1` | 用户声明已取得移植许可；公开分发前仍须归档权利人许可文本与范围 | 0.6.0 仅打包 14 个植物/果实定义、植物图集和果实模型；不复用原 DLL/PDB，不纳入其他系统；哈希见 `THIRD-PARTY-NOTICES.md` |
| SCPluginAPI | SCIE README 仅给仓库链接，未固定 commit | 上游仓库标示 MIT | 可研究其 API 思路；不能据此推定 SCIE 获得 MIT |
| 游戏程序集/内容包 | 本机 x25.01.17 | 专有游戏内容 | 仅外部编译引用；不提交、不打包 |

## SCIE `Common/` 二进制

| 文件 | 本地元数据 | SHA-256 | 许可/来源判断 | 决策 |
|---|---|---|---|---|
| `Jint.dll` | Assembly `0.0.0.0`; Product `2.11.23`; 246272 bytes | `67626877A3A33733FC1400F8039CB7A0825D2080E0D291EECA78F94E019B5037` | Jint 2.11.23 的官方 NuGet 页面匹配产品版本；项目采用 BSD-2-Clause。但 SCIE 没附 NOTICE/来源锁定，仍需从官方包重新获取并校验，而不是复制仓库 DLL | 当前最小包不使用；电路脚本阶段再做安全与 Android AOT/反射验证 |
| `System.Reflection.TypeExtensions.dll` | Assembly `4.1.2.0`; File/Product `4.0.0.0`; Xamarin 标记；6144 bytes | `6295D14E71BB7D8CEE76CCC570F2FF58F748517764F6E4E806642BCD891C2F86` | 仅凭文件名无法精确映射 NuGet 版本；旧 4.1.0 许可/发行元数据也与后续 MIT 包不同 | 不复制、不引用；net472 当前无需它 |

参考：

- SCPluginAPI GitHub：<https://github.com/0-v-0/SCPluginAPI>（仓库标示 MIT）。
- Jint 2.11.23 NuGet：<https://www.nuget.org/packages/Jint/2.11.23>。
- Jint 项目许可证说明：<https://github.com/sebastienros/jint>（BSD-2-Clause）。
- System.Reflection.TypeExtensions NuGet：<https://www.nuget.org/packages/System.Reflection.TypeExtensions/>。必须按精确包版本读取其中许可证，不能把最新版许可证反推到未知 DLL。

## 当前工程依赖

当前 `SCIEPort.csproj` 只有三项外部引用，均来自用户通过 `SCNetGameDir` 或 `SCNET_GAME_DIR` 指定的游戏目录，`Private=false`：

- `Survivalcraft.exe`
- `Engine.dll`
- `EntitySystem.dll`

打包脚本明确拒绝游戏 DLL、Jint 和 TypeExtensions，并检查载具及 Kelly 植物所需模型与纹理。
当前 `.netmod` 没有第三方二进制依赖；包含获用户授权声明覆盖的 SCIE 与 Kelly 资源，详见 `THIRD-PARTY-NOTICES.md`。

## 需要用户补充或确认

1. 公开发布前补充 SCIE 作者/版权持有者的许可文本或可核验链接，并确认其覆盖修改、二进制打包、模型与贴图再分发；当前仅记录了用户对已获明确许可的声明。
2. 最终 Android 7723 安装包中 `Survivalcraft`、`Engine`、`EntitySystem` 的实际程序集副本或由设备侧脚本产生的名称、版本、SHA-256 与 API 表面报告。不能把 Windows 同版本哈希结论直接当成 Android 结论。
3. 若后续保留 JavaScript 电路编程：确定从官方来源取得的 Jint 版本、许可证文件、NOTICE、Android 运行验证和脚本权限模型。
4. 可丢弃的单人测试世界与备份；联机阶段还需要一台主机和至少一个普通客户端的独立测试环境。
