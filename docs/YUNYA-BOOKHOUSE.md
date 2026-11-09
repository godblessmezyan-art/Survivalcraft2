# 云崖书屋参考图蓝图

2026-11-09，根据用户在本次聊天中提供的单张建筑图重建。包含用户补充指出的**左侧独立偏屋和半幅低坡屋顶**，不是只延长主屋檐口。本任务交付蓝图资产，不修改 Mod 运行代码、不提升 Mod 版本，也不生成新的 `.netmod`。

## 导入文件与尺寸

- 正式蓝图：[blueprints/yunya_bookhouse.json](../blueprints/yunya_bookhouse.json)，UTF-8、schema 3，名称“云崖书屋”。
- **9670 格，X/Y/Z = 52 / 47 / 48**，25 种已支持原版材料。无外部 Mod 方块、原生家具设计 ID、空气格或私有状态。
- 主层地板在相对 **Y=18**，圆窗和茶廊面向 **-Z**，玻璃书阁及悬挑露台在 **+X**。
- SHA-256：`233FAE9F480D550DE42BCCD6DC3A533964F86CF804F35C708480B4EA8E41DBCB`。

以 903 导入 JSON 并保存，再选择落点预览。放置原点对应崖台最低处，不是主屋门口；将主层地板对齐崖边地面时，蓝图底点应比该地面低 18 格。原点世界 Y 须为 0..209。建议先在可丢弃的新世界、侧面留有空间的崖沿或空旷场地预览。

空白坐标保持原地形，不会自动挖空房间。嵌入山体会导致房间/偏屋被原地形填满；要选择足够净空的落点。沿用原来的默认不覆盖、明确确认覆盖、房主权限、分批施工和 902 撤销流程。

## 重建内容与推定

按原图优先重建：深褐色主山墙坡顶、上翘檐端、前檐下的木构支撑、白色墙体、阁楼暖窗、通高玻璃书阁、圆形月窗、茶廊和短帘、粉色树冠、左侧半屋顶偏屋、石阶、厚石砌崖台、下层亮窗、悬挑露台及斜撑、栏杆、灯笼和铜色望远镜。附带一棵积雪层冠雪松与近景绿植，不复制画面中的远山、云海和远处森林。

单图没有展示的背面、西立面、偏屋完整进深、室内动线和下层空间按结构补全，不能作为原图已知尺寸。当前尺寸是为单份 10000 格预算选择的方块比例，不承诺单图恢复隐藏结构或固定相似度百分比。

已有能力限制的替代：

| 图中细节 | 蓝图实现 |
|---|---|
| 樱花 | 粉色大理石半砖与原木枝干，非新樱花方块 |
| 竹子 | 绿色染色木栅栏装饰 |
| 书架与书籍 | 原木/半砖框架、染色木板书脊，自定义结构组合 |
| 月窗 | 真实阶梯圆形开口、自然木色凸起窗框和后方书架 |
| 望远镜、茶具 | 铜块/煤块/半砖装饰，无交互实体 |
| 灯笼 | 原生 WickerLampBlock 加帽檐，使用原版发光 |
| 雪冠 | 白色半砖，避免依赖自然积雪持久不融化 |

没有借用 SCIE-Port 家具、代码或资源。所有几何均为最终 JSON 中的结构格；家具不提供坐下、储物或睡眠功能。植物和叶块仍受原生环境变化影响。

## 生成与验证命令

以下命令从仓库根目录执行；`$dotnetExe` 指向本机实际可用的 SDK。测试宿主使用原项目 net8.0，.NET 9 SDK 下通过 `DOTNET_ROLL_FORWARD=Major` 运行；没有改变 Mod 的 net472 目标。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Environment.ps1 -GameDir D:/Games/SCNET-x25.01.17
python scripts/New-YunyaBookhouse.py
$dotnetExe = 'C:/Users/85196/AppData/Local/Microsoft/dotnet/dotnet.exe'
$env:DOTNET_ROLL_FORWARD='Major'
& $dotnetExe run --project tests/BuilderAssistant.Tests/BuilderAssistant.Tests.csproj -c Release --no-restore
pwsh -NoProfile -File scripts/Test-BlueprintJson.ps1 -Path artifacts/yunya-bookhouse/云崖书屋.json
# Blender 5.1.1；将 $blenderExe 设置为本机实际位置。
& $blenderExe --background --threads 10 --python scripts/Render-YunyaBookhouse.py -- --source artifacts/yunya-bookhouse
```

实际结果：

| 范围 | 状态 | 结果 / 边界 |
|---|---|---|
| 固定 Windows x25.01.17 环境与 ABI | 已确认 | Test-Environment 三程序集登记哈希、net472 引用、API 1.42 和快艇 ABI 检查通过 |
| 既有测试工程 Release 编译与离线回归 | 已确认 | `ALL 3443 CHECKS PASSED`；Mod 运行代码未改，无新 Mod 包 |
| 真实导入解析与材料 DATA | 已确认 | 实际 BlueprintImportService / BlueprintJson 接受 9670 格；25 种材料来自当前矩阵 |
| 旋转 / 镜像 / 保存重载 | 已确认 | 4 朝向 × 2 镜像，共 8 组，数量不漂移 |
| 模拟施工 / 撤销 | 已确认 | 使用 MaterialTerrain/FakeTerrain 与实际 BuildPlanner/BuildTask；8 组分批施工和逐格撤销后全空，单批不超过 32 |
| 取消、回滚与默认不覆盖 | 已确认 | 本蓝图施工 3 批后取消并完成分批回滚；预置非空气阻止计划且零写入 |
| 花草支撑 / 主层通路 | 已确认 | 26 个依附格支撑检查；偏屋、茶廊、书阁、露台之间存在两格净高的保守通路 |
| Blender 几何预览 | 已确认 | Blender 5.1.1；预览只读取最终 JSON，使用原创程序材质近似，不是游戏截图 |
| Android / 7723 与游戏内效果 | 待验证 | 未运行手机或游戏；材质、采光、性能和自然变化须在实机确认 |
| 实际联机建造与撤销 | 待验证 | 未执行真实 Local/Server/Client；普通客户端主动施工沿用不支持 |

完整离线报告生成于 `artifacts/yunya-bookhouse/validation.json`，通路报告为 `circulation.json`。预览为 `hero.png`，可选 `--view interior --draft` 查看掀顶室内。不能把几何预览或模拟地形通过写成 Android 兼容。
