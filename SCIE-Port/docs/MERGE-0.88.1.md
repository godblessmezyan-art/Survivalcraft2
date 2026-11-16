# 0.88.1主线合并

2026-11-16，用户明确要求拉取、解决冲突并推送。接手时仓库已处于合并中：本地 `80b9f1b`，MERGE_HEAD/远端 `6ba0cc7`，共同起点 `55e77f0`；重新fetch确认远端包含三次新提交，没有另行覆盖或重置合并。

保留远端 `87e36fa` 的水面流体等级/鱼竿前向修复、`20c9879` 的五类创造栏、`6ba0cc7` 的装饰书架973–977、963 bit7与相关测试文档；保留本地睡眠射线、恒温床、壁炉红橙自发光/热量修复。统一为0.88.1，避免与双方既有0.86.1及远端0.88.0构建混淆。

7个冲突文件：HomeBlocks、csproj、modinfo与README/兼容性/测试结果/蓝图清单。文档同时保留双方记录；HomeBlocks保留CreateTexture/CreateMesh供书架重写，并将原PrepareSection分段材质管线合入默认CreateMesh，防止取一侧导致书架丢失或壁炉颜色修复失效。自动合并的HomeRules/占位/交互保留两边新增能力。

单SCIEPort.dll/net472/API1.42/SC2.4.10.6、内部身份、原GUID/协议/存档键、书典956/缓存保持。没有新模型、美术、协议或支持的蓝图材料；此前外部设备书架拆除问题不在本次新增实现范围内。

本轮完整验证命令（仓库根）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-NetMod.ps1 -GameDir E:/SCNET/SCNETx25.01.17 -Configuration Release
```

完整入口退出0，环境/ABI、Release 0警告/0错误、家居911/陈列器672、书架40网格/5方块/4配方、五类Tab及全部旧回归通过。最终213条目包SHA256 `A3579B77E8C9246F51D44D132941C14CF8432FE9BE228384C3814E83CF2F40F0`；详细字节及DLL指纹见 `TEST-RESULTS.md`。

本轮用户已授权正常push main；保留双亲合并提交，源码到达GitHub后再发布同提交预发布包。完整游戏、Android7723、触屏/分辨率、真实Local/Server/Client仍 **待验证**；不以合并、编译或隔离夹具代替设备验收。发布不移动旧标签或覆盖旧版本资产。
