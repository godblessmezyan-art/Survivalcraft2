# SCIE-Port 兼容性记录

目标：Survivalcraft NET 2.4 `Net_x25.01.17`，Android 为最终平台。

本表只使用“已确认 / 待验证 / 不支持”。编译和 Windows 程序集取证不能替代游戏运行、
Android 或多人测试。

| 项目 | 状态 | 证据与边界 |
|---|---|---|
| Windows 目标程序集 | 已确认 | `Survivalcraft.exe` 2.4.0.0；固定哈希见 `API-DIFF.md` |
| 目标框架 | 已确认 | `.NETFramework,Version=v4.7.2` |
| SCNET ModLoader 与 CSV | 已确认 | 对真实程序集编译通过；运行加载待验证 |
| 物品 ID 999、1000 | 已确认 | 反射扫描目标原生常量未占用；其他 Mod 冲突仍待运行检查 |
| XDB 合并格式 | 已确认 | 0.3.1 使用 Mod 容器根节点；0.3.0 的 `DatabaseObjects` 根会导致 `Database not loaded`，不支持 |
| Boat/Mount/Rider/SpawnCreature ABI | 已确认 | `Test-Environment.ps1` 逐项反射签名通过 |
| 蒸汽船本地推进代码 | 已确认 | 仅 `WorkType.Local`，Release 0 warning/0 error |
| Windows 游戏加载与驾驶 | 待验证 | 尚未启动目标游戏进行生成、骑乘与存档测试 |
| Android 7723 包 | 待验证 | 尚未取得该安装包的程序集哈希并实机运行 |
| 多人蒸汽船推进 | 不支持 | 尚未实现服务器权威驾驶输入和纠偏 |
| SCIE 燃料/蒸汽机库存 | 不支持 | 0.3.0 未实现，不能以占位代码宣称兼容 |
| SCIE 蒸汽船模型与纹理 | 待验证 | 0.3.0 按用户声明的明确授权打包；模型/纹理哈希已确认，游戏渲染待验证 |
| SCIE 音频及其他载具资源 | 不支持 | 不在本轮授权移植包范围内 |

## 当前包布局

```text
SCIEPort.dll
modinfo.json
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Database/Watercraft.xdb
Assets/Models/SteamBoat.dae
Assets/Textures/SteamBoat.png
THIRD-PARTY-NOTICES.md
```

兼容性结论发生变化时，必须同时更新 `TEST-RESULTS.md` 中的测试证据。
