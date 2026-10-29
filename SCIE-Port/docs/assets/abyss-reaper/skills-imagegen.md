# 阿比斯技能图集 V1 / 0.54.0

- 来源：本次用户要求，使用 Codex 内置 `imagegen`，原创生成；没有使用第三方游戏截图或素材，也没有修改武器 DAE 或原有 PNG。
- 文件：`src/SCIEPort/Assets/Textures/AbyssSkillsV1.png`。
- 输出原图 1254 × 1254、RGBA；以 2 × 2 等分（每格 627）使用。未改绘、裁剪或重采样，原图逐字节进入安装包。
- 顺序：左上裂界星门、右上断界斩、左下虚影瞬狩、右下终焉坍缩。
- SHA-256：`F7834AE5DB890FC245BF954F474BF497B22362DB5797FF37629D1C3897981DB0`。
- 基础解码内存约 6 MiB；原生 PNG 读取与透明外沿校验通过，实际手机 GPU、图像可读性和触屏体验待验证。
- 目标 Windows `Texture2DReader` 使用 `Texture2D.Load(Stream)`，默认单级纹理；`RectangleWidget` 走现有精灵图集路径，未使用未经验证的渲染 Hook。

完整生成提示词（内置工具；`transparent_background=true`）：

```text
Use case: stylized-concept. Asset type: production game HUD skill-button sprite atlas for Abyss Reaper, a dark-violet cosmic scythe. Generate ONE square RGBA sprite sheet with exactly FOUR circular skill medallions in a precise equal 2-by-2 grid. Each disc centered at the center of its quadrant (25%,25%), (75%,25%), (25%,75%), (75%,75%); all discs same diameter 42% of the whole sheet width, leave transparent gaps. Genuine transparent background outside each circle, no lettering or numbers anywhere. Each button: crisp thin faceted dark silver circular rim, narrow purple illuminated inlay, nearly black dark-violet interior; bold readable central illustrated symbol at mobile 72-pixel size. Top-left: a tall oval dimensional gateway split open, dark center, two sharp violet curved portal edges and small star. Top-right: one huge sweeping black crescent SCYTHE slash with a bright lavender razor edge crossing diagonally. Bottom-left: a ghostly hooded human silhouette lunging sideways through three sharp horizontal dimensional tears, readable motion. Bottom-right: a menacing black-hole event horizon, black circular center, bright pale-violet tilted accretion ring and four inward-pointing broken rifts; make this ultimate visually stronger. Consistent premium hand-painted dark fantasy game UI, rich material detail confined to rim, unmistakably distinct large symbols, high contrast pale lavender and silver on black. Strict orthographic flat front-facing circular icons; no perspective or scene, no external drop shadows, no text, no panel, no extra symbols outside circles, no purple particle spheres, no weapon redesign.
```
