# 测试结果

日期：2026-10-17  
目标：`Survivalcraft NET 2.4 Net_x25.01.17`  
本机目录：`E:\SCNET\SCNETx25.01.17`

## 结果矩阵

| 项目 | 状态 | 证据/说明 |
|---|---|---|
| 上游 SHA 固定 | 已确认 | `git rev-parse HEAD` = `4864d956793b6e670413019f1f6b1cc5c8bb0970`；工作树干净 |
| SCIE 许可文件检查 | 已确认 | 固定树没有 LICENSE/COPYING；因此当前不分发上游内容 |
| x25 Windows 程序集指纹 | 已确认 | 三项 SHA-256 与 `Test-Environment.ps1` 固定值一致 |
| 目标 ABI 检查 | 已确认 | 版本 2.4.0.0、API 1.42、SC 2.4.10.6、net472；ModLoader/CSV/ID 及 Boat/Mount/Rider/SpawnCreature 签名检查通过 |
| 原 SCIE 工程原样构建 | 不支持 | 旧 csproj 硬编码 `D:\Survivalcraft`；缺引用时产生 1378 错误，不能作为目标构建方式 |
| 旧源码直接引用 x25 编译 | 不支持 | target probe 暴露 56 个第一层兼容错误：PluginLoader、几何、UpdateOrder、交互、配方、类型冲突等 |
| SCIE-Port Release 编译 | 已确认 | `dotnet build`：0 warning，0 error |
| `.netmod` 内容校验 | 已确认 | 0.3.0 包含 DLL、modinfo、CSV、Watercraft XDB、授权蒸汽船模型/纹理及第三方声明 |
| `.netmod` 大小 | 已确认 | 21,099 字节；未压缩条目合计约 47 KB，文本和 DAE 经 ZIP 压缩 |
| `.netmod` SHA-256 | 已确认 | 0.3.1：`78E44A1CFF0FC37BF8192BAF42E55046E14252BB426E6A598D17C34153A62407` |
| 蒸汽船物品/组件编译 | 已确认 | ID 999；独立实体、原生 Boat/Mount/Body/Damage 和本地推进组件均以 Release 0 warning/0 error 编译 |
| SCIE 蒸汽船模型 | 已确认 | 固定提交文件 25,178 字节；SHA-256 `E3C2F54513D10486FA14927418E6ABB76CB7AC270B947A57C338A2423911C4F8`；`Cylinder` 节点静态检查通过 |
| SCIE 蒸汽船纹理 | 已确认 | 64×64 PNG，6,036 字节；SHA-256 `BB0CC5F9886E30BA9DE12D79F1C7724CBF9C6341DCE7606C65906441ABE8F6A4` |
| Windows 游戏加载 | 待验证 | 本次没有启动游戏进程 |
| Windows 创造栏显示材料 | 待验证 | 必须在新建可丢弃单人世界确认 |
| Windows 蒸汽船生成/骑乘/驾驶 | 待验证 | XDB、内置模型路径、触屏输入和实体运行行为必须在游戏中确认 |
| 0.3.0 世界加载 | 不支持 | 用户实测进入世界显示 `Database not loaded`；反编译确认 XDB 根节点错误地造成嵌套 `DatabaseObjects` |
| Windows 保存/重载 | 待验证 | 仅创造物品也需验证存档不报错 |
| Android 7723 实测 | 待验证 | 尚未部署到手机，Windows 指纹不能替代 Android 证据 |
| 多人主机/客户端 | 不支持 | 0.3.0 的额外推进明确仅启用 `WorkType.Local`；尚无服务器权威输入同步 |

## 执行命令

```powershell
cd E:\Survivalcraft2\SCIE-Port
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
dotnet build .\audit\SCIE.Legacy.TargetProbe.csproj --configuration Release -p:SCNetGameDir='E:\SCNET\SCNETx25.01.17'
```

最后一条命令预期失败；它是兼容性审计，不是交付构建。

## 包清单

```text
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Database/Watercraft.xdb
Assets/Models/SteamBoat.dae
Assets/Textures/SteamBoat.png
modinfo.json
SCIEPort.dll
THIRD-PARTY-NOTICES.md
```

包内未发现：`Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll`、`Jint.dll`、`System.Reflection.TypeExtensions.dll`、SCIE 音频/旧 XDB/配方/翻译或其他载具资源。

## 下一轮运行测试步骤

1. 备份并使用新建单人世界。
2. 把 `artifacts/SCIEPort.netmod` 放入该精确客户端的 `NetMods`。
3. 启动后保存完整加载日志，确认 `SCIEPort` 被发现且无 CSV/Block 初始化警告。
4. 进入创造模式，搜索并拿取 `SCIE Port Test Alloy`；检查图标、名称、描述、堆叠、丢弃、拾取。
5. 搜索 `SCIE 蒸汽船（原型）`，对水面使用；确认只生成一艘实体且物品减少。
6. 验证上船、触屏前进/后退/转向、松手减速、下船、碰撞损坏及附近最多 4 艘限制。
7. 保存退出并重进，确认玩家库存、船实体和位置可恢复且日志无实体模板错误。
8. Android 上重复 2–7，并记录 APK/程序集指纹、设备、Android 版本和日志。

只有以上证据完成后，才能把相应运行项目从“待验证”改为“已确认”。
