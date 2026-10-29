# 一室一厅示范蓝图 v0.23.0

这三份JSON由真实生成器导出，均使用schema3、现有原版材料和7个结构块家具模板。不要将`layouts.json`作为蓝图导入，它只供俯视图渲染使用。

| 文件 | 占地 | 种子 | 外墙/屋顶 | 展开方块数 |
|---|---|---|---|---|
| furnished-home-9x13.json | 9×13 | 0 | 木板/人字 | 621 |
| furnished-home-11x15.json | 11×15 | 1 | 石砖/人字，家具镜像 | 812 |
| furnished-home-13x17.json | 13×17 | -27 | 木板/四坡 | 1190 |

JSON数量不含原生门自动补出的上半格；规划器和事务会单独计入。世界内建造会保留未写入位置的原地形，不会自动挖空房间。

在903中粘贴JSON导入，可命名保存、选择落点、预览和确认施工；也可以用904工作台自行配置生成。床榻、柜子、灯架均是原有结构装饰，不新增睡眠/储物/照明功能。

重新生成：

```powershell
dotnet tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll --furnished-homes docs/examples/furnished-homes
python scripts/Render-FurnishedHomes.py --output artifacts/furnished-homes
```

需要先构建测试项目；绘图需要Pillow和可显示中文的字体（可用`--font`指定）。PNG是实际JSON坐标的布局示意，隐藏屋顶，并非游戏截图。Android7723实际画面与交互待验证。
