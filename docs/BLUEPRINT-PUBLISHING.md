# 建筑蓝图默认交付到数字书房

2026-11-15用户要求：蓝图生成并验证后默认上传、发布到[数字书房蓝图库](https://wxhappylife.top/digital-study/index.html#blueprints)。技能：[survivalcraft-blueprints](../skills/survivalcraft-blueprints/SKILL.md)，已安装到当前用户Codex skills目录，允许自动选用。

使用当前Mod真实导入器与能力表预检；SCIE-Port使用`SCIE-Port/scripts/Test-BlueprintJson.ps1`生成包含文件SHA256的报告。上传网站现有限制为10000格/每轴64/2MiB/UTF-8无BOM，小于绘卷本地限制；站点只认识旧10个家具模板。需要时由实际解析器展开结构家具到独立上传副本，不改变原文件的模板引用或丢弃DATA。

发布脚本位于`skills/survivalcraft-blueprints/scripts/publish_blueprints.py`。它走正常管理员预检→草稿→发布，随后匿名获取公开元信息、版本化下载与列表，验证原始字节、SHA及尺寸/数量/名称。重复同内容复用已发布条目；POST不盲目重试；内容更新需要指定旧ID和最新expectedRevision。凭据仅在本机技能的DPAPI私有文件中保存，既有账号正常登录，口令和临时Bearer不入Git或发布报告。

## 本次已发布

| 名称 | 内容 | ID / revision | SHA256 |
|---|---|---|---|
| [古建细节展台](https://wxhappylife.top/digital-study/api/game-blueprints/v1/blueprints/0c1e35c3-acbc-4e12-82bc-190fabe1adff/json?revision=1) | 406格，18×2×21，完整28外观 | 0c1e35c3-acbc-4e12-82bc-190fabe1adff / 1 | 3e0136ee932c89b684f5f7af171ee369e736a45d757e2abf3ef0f8b7e878cb1b |
| [书屋细节组合](https://wxhappylife.top/digital-study/api/game-blueprints/v1/blueprints/f0bf100e-c524-480d-b468-c0da6edd983a/json?revision=1) | 109格，12×3×8，月窗/茶席/观景台 | f0bf100e-c524-480d-b468-c0da6edd983a / 1 | 337d5ffdadcc1b466e6871a3e01e14d3e0a9e90615c260b8fc7dee7cbb74424d |

两项需要荒野探险家0.89.0及以上的古建部件。展台上传原文件；组合示例用实际解析器展开为cells上传，原模板JSON保留。公开列表可见、published状态、回读文件逐字节和SHA与本地报告一致，发布回执位于`artifacts/digital-study/publication.json`。游戏内可在绘卷“云端→刷新”后下载；Android与真实房间施工仍待验证。

本次没有修改网站、生产数据库结构、游戏运行代码或Mod版本，没有生成新netmod或源码push。现有0.89.0构建/包验证记录继续适用。
