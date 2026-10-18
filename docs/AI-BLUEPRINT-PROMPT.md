# 给外部 AI 的蓝图生成规范 + 提示模板（v0.8.0）

闭环：**外部 AI 生成蓝图 JSON → 用安卓文件管理器投递进游戏蓝图库 → 手持“蓝图工具(903)”点击载入/校验 → 换“建造工具(901)”对准地面两次点击落成 → 撤销工具整体回退**。游戏内导入会做二次硬校验（坏语法 / 超界 / 不支持方块 / 材料悬空 / 重复·小数坐标一律响亮拒绝、绝不部分导入、绝不污染剪贴板）。本文只为提高“一次通过率”，不是绕开校验。

## 直接复制给 AI 的提示词

> 你是 Survivalcraft2「Builder Assistant」模组的蓝图生成器。请只输出一个 JSON 对象（不要任何解释、不要 Markdown 代码围栏、不要注释），严格遵守以下 schemaVersion=1 规范：
>
> 顶层字段
> - `"schemaVersion"`: 整数，必须是 `1`（大于 1 或缺失会被拒绝）。
> - `"kind"`: 可省略；若写必须正好是 `"builderassistant.blueprint"`。
> - `"name"`: 字符串（可中文，仅显示用，别拿它当文件名）。
> - `"size"`: **建议省略**，让游戏自动重算。若写，须与 cells 真实包围盒完全一致，否则被判篡改拒绝。
> - `"materials"`: 数组（可省略），每项 `{"id":非负整数,"key":"<允许的键>"}`，id 从 0 连续唯一。
> - `"cells"`: 数组，**必填且非空**。每项 `{"x":int,"y":int,"z":int, 以及材料}`。
>
> cells 规则
> - x/y/z 是**整数相对坐标**（禁止小数/科学计数/字符串）。可为负（游戏归一化到最小角 0,0,0）。
> - **坐标不得重复**；**总数 ≤ 1024**；归一化后 **每轴跨度 ≤ 64**。
> - 材料两选一：内联 `"key":"<允许的键>"`（推荐），或索引 `"m":<materials 表内 id>`。
> - **禁止**：cell 内 `o`/`d`/`props`、顶层非空 `buildOps`；禁止注释与尾随逗号。
>
> 只允许这 5 种纯立方方块（逻辑名 ↔ 原生名等价，其余一律不支持）：
> `StoneBase=CobblestoneBlock`、`StoneWall=StoneBrickBlock`、`WoodWall=PlanksBlock`、`WoodTrim=PlanksBlock`、`Roof=BricksBlock`、`Glass=GlassBlock`、`Floor=PlanksBlock`。
>
> 设计取向：铺地板→四面砌墙→盖屋顶→中间留空房间；屋顶阶梯收缩更真实；控制格数远低于 1024；不要悬空孤块。
> 输出：**仅合法严格 JSON**。

## 自检清单

1. 仅一个顶层对象，无多余文本。
2. `schemaVersion==1`；cells 非空、坐标全整数、无重复、数量 ≤1024、单轴 ≤64。
3. 材料 key ∈ 上表 7 逻辑名或 5 原生名；用 `m` 则 id 必须在 materials 内。
4. 无 `o`/`d`/`props`/`buildOps`；无注释/尾随逗号；若写 `size` 必须与包围盒一致。
5. 文件另存为纯 ASCII 名（仅字母/数字/下划线/连字符，≤48）再放入蓝图库。

## 最小合法示例

见 `docs/AI-SAMPLE.json`（`tiny_pillar.json`）：一根 1×4×1 小柱（圆石→石砖→木板→玻璃）。已用真实 `BlueprintJson.Decode` 离线验证 `Success=True`、自动尺寸 1×4×1；同时验证“重复坐标”样本被正确拒绝。

## 落地投递提醒

手持蓝图工具点击 → 记下消息里打印的真实目录；把 `.json` 用安卓自带文件管理器放进该目录。**注意（待验证）**：Android scoped storage 下系统文件管理器能否写入 `ModsManager.UserDataPath`，需真机确认；若不可写，逃生绳是 ADB push 到同一路径。
