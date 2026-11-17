# 云天书典 · 手绘纸页 UI

本次版本0.90.0，2026-11-17，基于最新main `72ec090`。仅调整显示层；设计采用暖纸色、墨褐笔线、鼠尾草绿与自然水彩。UI/UX技能的初次通用设计系统查询偏向营销页，未采用其布局；收窄到style后采用E-Ink/Paper与手绘质感的阅读原则，touch检索采用48逻辑单位控件与至少8单位间隔。没有引入Web框架、外部字体或技能推荐的CSS依赖。

## 原创美术

通过内置 `image_gen.imagegen` 分别生成纸纹与植物角花；完整英文提示词见 [prompts.json](prompts.json)。未使用外部图像/角色参考。

| 对象 | 原始生成图 | 正式运行资源 |
|---|---|---|
| 羊皮纸/手工纸纹 | [Paper.png](source/Paper.png) | [256×256 Paper.png](../../src/SCIEPort/Assets/Textures/CelestialCodex/Paper.png) |
| 自然水彩角花 | [Botanical.png](source/Botanical.png) | [256×256 Botanical.png](../../src/SCIEPort/Assets/Textures/CelestialCodex/Botanical.png) |

原图1254×1254，正式PNG合计151210字节；两张RGBA纹理解码后合计0.5 MiB。原图不进入netmod。 [manifest.json](manifest.json) 保存原始/运行SHA256， [build_assets.py](build_assets.py) 仅做RGBA/256px缩放及透明纹理GPU编码转换，不重画插画。

目标 `Game.IContentReader.Texture2DReader` 直接以Texture2D.Load默认参数加载；`PrimitivesRenderer2D` 默认使用One/InverseSourceAlpha混合。因此植物运行PNG的RGB预乘alpha、alpha原值保留，控件淡化色也用预乘值；纸纹完全不透明。原图保持原始直通alpha。不得把运行PNG再预乘一次或按普通直通alpha显示后修改颜色。

## 控件与布局

- 原生Canvas/Stack/ScrollPanel/Label/Button/Clickable，字体继续使用安装游戏的Pericles；不分发游戏字体。两纹理在Draw时由ContentManager按需加载并复用；不增加渲染目标、shader、动画或平台DLL。
- 页面共用纸纹、双层细笔边框及水彩角花。阅读页与紧凑横屏隐藏角花/品牌眉题，让出正文空间。纸页使用墨褐/鼠尾草绿，夜读使用暖深绿/浅金墨。
- 阅读栏最大660逻辑单位；外侧保留16–32屏幕边距、内侧24–40留白。按钮不低于48高，四操作栏保留8间隔；导入三操作栏同样保留8间隔。书籍行88、来源/导入行60、续读行100、文件行64。
- 五档原字号继续为`.79 + .13 * font`；正文FontSpacing.Y由8调至11，保留原文段落及滚动。原生中文换行按标点回退会造成短行，现只调整显示行缓存：按真实字体宽度排版，保护UTF16代理对、英文词组及中文标点禁则；原m_text、章节内容与revision不改。
- 设置仍为原字号与纸页/夜读，选中主题增加下划线；没有新增开关。导入面板使用同一套控件和当前主题，仍自动启动既有系统选书。Android系统文件选择器由系统绘制，不改其宿主/Manifest/业务。

原生GPU预览在 `artifacts/CelestialCodex/ui-0.90.0/`，共36张：1280×720、375×812、812×375 × 云端/本地/章节/正文/设置/导入 × 纸页/夜读。它们使用实际编译控件、真实游戏字体/引擎及示例数据，没有玩家世界、网络请求或Android系统窗口；不是手机截图。构建、精确指纹及验收见 [专项](../../docs/CODEX-PAPER-UI-0.90.0.md)。
