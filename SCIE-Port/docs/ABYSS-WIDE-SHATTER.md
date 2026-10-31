# 万象破碎：前方裂界扩大 · 0.61.1

按用户“当前碎裂风格很好，只是范围不足，希望第一人称前方整体覆盖后碎裂爆炸”的反馈，仅扩大三技能视觉。基准为已同步main `83e43fa` / 0.61.0，开发前工作区干净、fetch与ff-only确认最新；回退分支 `backup/abyss-0.61.0-before-wide-shatter`，旧0.61.0版本包保留。

## 实现

`VoidShatterScene` 继续使用原有12/20/28块厚片、固定种子、裂边和分离/坍缩结构。将局部横向尺寸放大 **7.2倍**、纵向 **4.5倍**，裂面从原来前方的一小块变成覆盖前方视野的大面积空间断层；厚度方向不整体拉伸。爆炸正面波随裂面扩大，横向空间环放大3.2倍，并调整波前扩张曲线，让0.4秒爆炸期能看到波从中心越过画面。

施法者自己的 `FppCamera` 采用同一组世界坐标几何的前景绘制：原生 `DepthStencilState.None` 覆盖近景，才能让地面、近建筑也呈现前方整体开裂。没有使用全屏黑色矩形，没有读取/切割真实场景纹理，仍为原创几何近似。方向与中心沿用主机接受时的位置，转头看向背后不会把裂面拖到屏幕中央。旁观者及第三人称仍用 `DepthRead` 参与地形遮挡；仅施法者第一人称的前景层有此覆盖行为。前景批次排在其他施法者的世界批次之后，避免多人时叠加顺序错误。目标原生类型 `Game.FppCamera` 已从固定x25.01.17程序集核实，未增加Shader或Hook。

时间保持 **1.8秒**：0–0.3秒裂边出现、世界仍可见；0.3秒裂界铺满前方后厚片分离；1.2–1.4秒坍缩；1.4–1.8秒扩张爆炸；1.8秒所有网格退出，恢复原画面。每档片数和每次生成缓存的方式保持，单次三角形仍小于1300，没有增加粒子或每帧大型数组。

## 战斗和联机范围

本次扩大的是**视觉范围**。主机瞄准15格内、伤害球半径10格、名义攻击160、1.35秒命中一次、冷却22秒保持；遮挡/领地/加载校验、玩家和玩家承载体排除、每帧128候选预算保持。`VoidSpaceRuntime/Damage/Rules/Package` 均未修改，251字段、种子和时间同步保持；虚夜4.2秒/180秒、星门250和v1存档、其他神器/技能、HUD、模型与图标也保持。此次不新增共享大招领域功能。

第一人称裂界可以视觉覆盖近处障碍，但不因此穿墙攻击或扩大真实伤害球。三技能真实画面切片/像素折射仍未实现；本版沿用用户认可的黑紫几何碎裂风格。

## 测试与预览

完整Release和原生回归通过，详见 [TEST-RESULTS.md](TEST-RESULTS.md)。新GPU脚本共126项目标Engine/OpenGL检查，另2次CPU HUD导出。12组覆盖检查为低/中/高 × 16:9/20:9投影 × 垂直FOV65°/85°；统一960×540离屏输出，0.3秒裂面覆盖像素变化均为100%，初始裂边阶段仍保留至少80%原景。观察者保留更多受地形遮挡的近景，转身不粘屏，1.8秒原景逐像素恢复。它们是实际原生GPU渲染**合成场景**，没有加载真实存档或验证Android设备/网络。

[新旧范围与关键阶段对比](../design/abyss-wide-shatter-0.61.1/wide-shatter-stages.png) 左上为0.61.0原生GPU旧范围，其余为本版；图上明确标注非游戏/非Android实拍。覆盖率和最终DLL哈希见同目录 `coverage.csv` / `preview.json`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Build-WithPinnedSdk.ps1 -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-AbyssSpaceGpu.ps1 -GameDir D:/Games/SCNET-x25.01.17 -AssemblyPath SCIE-Port/artifacts/build/SCIEPort.dll -OutputDir SCIE-Port/artifacts/wide-shatter-gpu
python SCIE-Port/scripts/Render-WideShatterPreview.py SCIE-Port/artifacts/wide-shatter-gpu --assembly SCIE-Port/artifacts/build/SCIEPort.dll
python SCIE-Port/scripts/Compare-AbyssSpacePackage.py SCIE-Port/artifacts/荒野探险家-0.61.0.netmod SCIE-Port/artifacts/荒野探险家-0.61.1.netmod --visual-only
```

生成对比图可附加 `--baseline` 指向保留的0.61.0原生GPU帧。新版与旧版包比较仅DLL/modinfo变化，其余144项逐字节保持。协议和存档兼容；同版0.61.1才能保证相同视觉尺度。Android7723实际第一人称/第三人称、宽屏与FOV设置、近墙/空中朝向、多人同时施放、取消清理、GPU填充率与手机帧率仍**待验证**。只本地提交，不自动推送或发布。
