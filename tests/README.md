## v0.24 地形和装饰

`TerrainMaterialTests.cs`验证18种新增材料的规划/建造/撤销、地基排序、悬空拒绝、Y0、支撑丢失和后续修改保护；既有FineDetailTests对81种材料穷举合法DATA并验证变换/往返。目标项目的新探针运行真实游戏DATA和支撑方法。范围及自然副作用边界见[v0.24记录](../docs/BUILDER-MATERIALS-0.24.0.md)。

## v0.23 一室一厅回归

`FurnishedHomeTests.cs`验证参数矩阵、实际JSON往返与材料、两格高通行负例、四向施工/撤销、取消/失败回滚和工作台入口。`--furnished-homes <目录>`导出三份可导入蓝图与布局清单；绘图脚本从实际导出坐标渲染俯视图。验证记录见 [v0.23说明](../docs/BUILDER-FURNISHED-HOME-0.23.0.md)。

## v0.22 蓝图库与预览回归

`BlueprintLibraryTests.cs` 使用真实文件目录和真实导入/规划代码验证持久化、删除、命名冲突、路径隔离、10000格导入、公开JSON示例、四向预览、门关联格、地形快照失效、零写入和绘制标记预算。目标测试新增GuiDraw和原生线框ABI检查。设备验收见 [v0.22记录](../docs/BUILDER-BLUEPRINT-0.22.0.md)。

# Tests

Place game-independent unit tests here. Tests that require a live SCNET client must be documented as manual Windows/Android test cases under `docs/` and must never use a player's primary world.


## v0.18.0 DATA与事务回归

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Logic.ps1`：无游戏引用；穷举每种能力的合法DATA、复制/JSON往返、旋转/镜像群关系、支撑排序、后验检查、事件失败/取消/回滚、旧schema、4419格139批建造及139批撤销。

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-BuilderAssistantStates.ps1 -GameDir <目标目录>`：净化输出的net472测试程序直接调用固定哈希x25.01.17的颜色/轴向/朝向静态API，与生产codec逐项交叉检查；不启动游戏，不加载世界，不复制或提交游戏DLL。此步骤已加入Release打包。

`dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --capabilities docs/blueprint-block-capabilities.json`：从生产能力目录导出逐类型精确DATA集合和能力矩阵。Android的触屏/渲染/原生事件/存读档/联机验证仍须单独记录。
