# Builder Assistant v0.22.0 开发和验收记录

日期：2026-10-30。目标：Survivalcraft NET x25.01.17 / net472 / Android7723。只修改Builder Assistant、相关测试、文档和根构建环境检查，不引用SCIE-Port源码或资源。

## 改动

统一生成规范为 `BLUEPRINT-SPEC.md`，提示词、材料表和代码生成机器矩阵均引用它；新序列化输出schema3，旧1/2/3继续可读。新示例由真实解析器与规划器验证。导入预算由256Ki提高到2097152个UTF-16代码单元，使10000格合法蓝图能够序列化后再次导入；空间和事务预算不变。

903复用系统剪贴板API、单一导入解析器、CurrentBlueprint和本地目录。增加本地文件确认删除，保存成功刷新列表并选中实际文件。中文显示名保存在JSON，文件名为可移植标识，冲突编号且绝不覆盖。保存先写同目录临时文件再移动发布；失败删除临时文件。载入/删除使用原始已选择文件名，拒绝路径穿越，不把危险名字清洗为另一个文件。删除不会清空内存蓝图，也不会修改世界。

新增 `BlueprintPlacementPreview` 只读检查与 `BlueprintPreviewControls`。选择落点后显示世界范围框/地面矩形/原点/作者前向/高度/冲突格；支持旋转、升降、点地重选、取消和确认。箭头表示作者-Z，不一定是主门。最多绘制128个占用或不可读格；完整规划、总占用数及地形快照不截断。门的隐式上半格计入预览高度和快照。家具净空也纳入确认快照。

确认前核对权限和地形，变化时更新预览并要求再次确认；覆盖时另有清障确认。最终仍通过BuildPlanner/QuickStart/BuildTask及已有journal/撤销。取消、天空点击、换工具、死亡和所属世界Dispose时释放预览与窗口；绘制异常停止预览，避免无预览施工。

新增 `GuiDraw` Hook，使用目标原生 `PrimitivesRenderer3D.FlatBatch`、`QueueBoundingBox/QueueLine`、`DepthStencilState.None`、`Flush(camera.ViewProjectionMatrix)`，不引入Shader、纹理、数据库GUID或网络包。目标程序集反编译确认 `ComponentGui.Draw` 调用GuiDraw、DrawOrders为9，ComponentBlockHighlight使用同一套原生线框API。只绘制所属玩家相机；预检不在Draw中执行。区块不就绪仅每15个GUI tick重试一次，最多10次，时间到后保留失败预览供重选。

环境检查同时接受机器级net472 Targeting Pack和微软NuGet的net472引用程序集；原固定游戏哈希、版本和ABI校验保留。本机只有后者，原脚本将可编译环境误报为依赖缺失。

## 实际验证

命令（PowerShell会话先将已安装SDK的目录加入PATH；本机只有.NET9，因此运行net8测试设置 `DOTNET_ROLL_FORWARD=Major`）：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-NetMod.ps1 -GameDir <本机SCNET目录>
dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --capabilities docs/blueprint-block-capabilities.json
```

- 固定Windows程序集环境、哈希、Speedboat ABI检查通过。
- 2130项离线检查通过，包括10000格序列化导入、超大文本拒绝、中文文件往返、重名不覆盖、删除/坏文件/路径穿越，以及公开示例。
- 四向预览和规划一致、零地形写入、清障冲突数、地形变化失效、门上半格变化、区块恢复、世界高度边界及128标记不截断校验通过。
- 1913项目标静态API检查通过（含新增GuiDraw、投影矩阵、Margin和线框接口）。
- 原有分批、取消、撤销、失败回滚、权限及结构/家具回归通过。
- Portable Fortress目标原生DAE导入、Release 0警告0错误，以及netmod入口/资源/XDB/编号/专有程序集排除检查通过。
- 未启动游戏进程；真实GPU绘制、Windows游戏内、Android7723和真实联机均为待验证。静态API/规则检查不能代替这些项目。

## Android7723验收

1. 新建可丢弃单人创造世界，安装0.22.0。903打开管理，导入最小示例和含家具书屋；非法JSON不得替换先前有效蓝图。
2. 中文命名保存两次，列表显示两个文件；重进世界载入并核对名称、尺寸、方块与家具。
3. 删除先取消，文件仍存在；再次删除并确认，重进后文件消失；当前内存蓝图仍可预览或重新保存。损坏JSON也可在列表选择删除。
4. 点击选择落点并预览，对地使用903，世界不应发生任何写入。移动相机观察范围、原点、箭头和高度；对非正方形建筑旋转四次，范围与建造保持一致。
5. 升降预览并重选落点，确认不会超过Y0..255；门上半格位于范围内。贴近高度边界或未就绪区块时不得施工。
6. 有障碍时显示橙色/红色标记和总数；清障关闭拒绝覆盖，开启后需确认。取消确认回到预览；确认前让另一玩家修改目标格，应要求重新确认。
7. 空旷目标也要明确点击确认建造。核对落成坐标与预览一致，902分批撤销；施工失败/中断沿用已有回滚。
8. 预览或删除确认中换工具、死亡、离开/重新进入世界，确认没有残留窗口、线框、引用或误建。
9. 测试横竖比例、UI缩放、软键盘、滚动、返回键和原生移动/视角控件；10000格蓝图记录帧时间。占用多于128格时总数完整，线框只显示样本。
10. 单机、房主、同版本普通客户端分别测试。普通客户端可浏览本地蓝图库但不可施工；多人握手和地形同步必须实测。

## 已知范围

这是落点与冲突线框预览，不是完整建筑材质幽灵模型；没有原生微体素家具或新方块。当前采用一个本地放置会话，另一个本地操作者接管工具会取消先前会话。覆盖仍不承诺完整恢复物品、电路、掉落和流体连锁副作用。

产物：`BuilderAssistant-0.22.0.netmod`；大小 2398097 字节；SHA-256 `623D03FDF16C1F5D33A22C36DB9F20DD47BDD91C8318918A274D3420CC6AE55F`。
