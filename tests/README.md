# Tests

Place game-independent unit tests here. Tests that require a live SCNET client must be documented as manual Windows/Android test cases under `docs/` and must never use a player's primary world.


## v0.18.0 DATA与事务回归

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Logic.ps1`：无游戏引用；穷举每种能力的合法DATA、复制/JSON往返、旋转/镜像群关系、支撑排序、后验检查、事件失败/取消/回滚、旧schema、4419格139批建造及139批撤销。

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-BuilderAssistantStates.ps1 -GameDir <目标目录>`：净化输出的net472测试程序直接调用固定哈希x25.01.17的颜色/轴向/朝向静态API，与生产codec逐项交叉检查；不启动游戏，不加载世界，不复制或提交游戏DLL。此步骤已加入Release打包。

`dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --capabilities docs/blueprint-block-capabilities.json`：从生产能力目录导出逐类型精确DATA集合和能力矩阵。Android的触屏/渲染/原生事件/存读档/联机验证仍须单独记录。
