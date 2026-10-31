# 给 AI 的蓝图生成提示词 v0.24.0

唯一生成规范：[BLUEPRINT-SPEC.md](BLUEPRINT-SPEC.md)。材料与家具：[blueprint-block-reference.md](blueprint-block-reference.md)；精确 DATA：[机器矩阵](blueprint-block-capabilities.json)。本文件不维护另一套旧格式。

将以下提示词和上述三个文件的内容一起交给 AI：

> 你是 Survivalcraft NET x25.01.17 / Builder Assistant v0.24.0 的蓝图生成器。请依据提供的统一规范、材料能力表和机器矩阵生成指定建筑。只输出一个严格 JSON 对象，不解释、不加 Markdown。
>
> 使用 schemaVersion=3、kind="builderassistant.blueprint"、中文 name 和 cells 数组。每格 x/y/z 为唯一整数，材料只用当前81种能力表里的精确 key；非 cube 必须写实际 f，d 必须来自该类型 allowedData。推荐内联 key 并省略 size。未知能力请在设计中改用已支持材料，不捏造字段或方块。
>
> 根据需要添加 furniturePlacements，使用已有模板 ID、整数位置和 rotation=0/1/2/3；留出家具完整包围盒，地板在其下方。只支持 natural 颜色和 default 变体。
>
> 结构与家具展开合计不超过10000格、每轴不超过64格（含门自动上半格），文本不超过2097152个UTF-16代码单元。不要写空气格、原生家具设计索引、props/state/buildOps、流体、电路、容器内容或Mod神器。
>
> 建筑要保留门上半格、可通行走道、足够层高和楼梯空间，依附方块须有支撑。未写的格不会被清障挖空。输出前自检坐标冲突、家具净空、材料族、DATA、数量和尺寸。
>
> 我的建筑需求：在这里填写用途、风格、占地、楼层、材料和房间要求。

生成后：复制 JSON → 903 导入 → 显式保存 → 选择落点并预览 → 调整朝向/高度 → 确认建造。不要使用历史版本的“5种方块/1024格/换901两次点击”流程。

自然材料生成前必须读取支撑与变化限制：沙/砂砾用falling，高草0/8与花0/1不能混淆；请优先生成有完整地基、成熟花草的景观，勿依赖悬空沙、永不融化的雪或永不消失的落叶。
