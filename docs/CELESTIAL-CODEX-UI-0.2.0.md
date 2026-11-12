# 云天书典0.2.0：阅读界面改版

日期2026-11-07；以本地main `76aacfe` 为基准。按用户指定使用已安装的ui-ux-pro-max，只改独立书典、其测试、构建版本和相关说明。未部署Digital-Study，未同步书籍，未修改其它Mod或游戏文件。

## 设计与实现

- 书库采用书名/作者两层列表、细边框和宽松间距；AI解读放在详情首位，长作者不会遮掉来源标识。顶部有继续阅读卡片；只保留一个刷新入口。
- 目录标出正在阅读的章节，按24节分页。正文独立章节标题、竖向滚动、8单位附加行距，阅读列最长720逻辑单位；5档字号保留。
- 正文底部为上节/目录/设置/下节；字号与纸页/夜读收进设置页。返回/完成恢复同一章节与相对滚动位置。主题有“当前”文字和选中边框，选中状态不只靠颜色。
- 纸页为米白纸面、深褐文字；夜读为深蓝灰纸面、浅暖文字。所有颜色集中在ReaderTheme；普通文字在两主题的背景/卡片/按压态达到4.5:1，按钮边界对卡片达到3:1，纯计算测试覆盖。
- 底部按钮48高、间距8，外侧保守留白至少16；短横屏压缩元信息行，不挤占按钮。只用文字控件与原生矩形，不引入字体包、网页UI、运行库或动画。

skill检索实际执行 `ebook reader parchment calm --design-system`，结果为网页Hero/CTA与蓝橙配色；按skill要求缩窄重试 `reader typography --design-system` 仍无匹配书典的整体系统，**未采用或持久化该输出**。采用项目定制纸页/夜读方向；有匹配的局部查询为 `line length --domain ux`（行宽/行距）与 `safe area --domain web`（原生移动安全区）。阅读了SKILL、pro-rules及quick-reference第1–3节。网页字符数、CSS像素、SVG和在线字体建议未直接套到SCNET；不将逻辑48单位声称为Android48dp。

## 文字错误与目标ABI证据

目标LabelWidget.Text会对普通字符串查询LanguageControl.Get("Usual", value)，对`[x:y]`调用GetContentWidgets。云端正文、书名或章节名若碰到语言键会显示无关译文。新增CodexLabel直接写目标public `m_text`并清除`m_linesSize`，原生Draw/UpdateLines仍使用同一缓存与字体；没有修改全局语言表。

目标原生Canvas将“右侧剩余空间”传给Label测量；原生Label在应用自己的Size之前就换行/省略，可能导致长书名盖住返回/关闭。CodexLabel.MeasureOverride先把可用测量空间限制在自身Size，再调用原生测量。真实小屏预览发现并验证此问题后修复，新增测量回归。

本机实际语言表可复现`on → 开`及`[PlayerScreen:4] → 玩家皮肤`；书典控件保留输入原文。纯数字、括号语法、中文与换行/emoji文本均验证原样保留。**本机表没有在此路径匹配到用户所述的精确“皮肤颜色”，没有取得手机截图和对应输入，不能将该文字的来源宣称为已完全定位。** 若它来自其它原生面板，本次没有修改那些面板。

ButtonWidget、ClickableWidget、Label字段及FontSpacing/MaxLines等均回到固定Windows目标反编译与成员解析验证；未引入原先Android已证缺失的List谓词便利方法、CreateLinkedTokenSource、CancelAfter或新的HTTP接口。网络取消、权限与错误回退保留0.1.3实现。

## 缓存与身份

position.json增加可选整数theme（0纸页、1夜读）；缺省为0，旧文件的章节、字体、滚动比例保留。旧版读取会忽略额外字段。主题和字号通过原有后台PositionWriter保存；目录和正文缓存、API schema/revision及大小/数量/容量限制不变。没有整本预下载或新增“整本已缓存”标识。

版本0.2.0/程序集0.2.0.0；PackageName/AssemblyName CelestialCodex，956/DATA0，3个XDB GUID不变。包仍为DLL、modinfo、CSV、XDB、书本PNG共5项，未新增资源或专有内容。所有端按原生握手统一0.2.0。

## 验证与交付

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CelestialCodex.ps1 -GameDir E:\SCNET\SCNETx25.01.17
tests\CelestialCodex.TargetTests\bin\Release\net472\CelestialCodex.TargetTests.exe E:\SCNET\SCNETx25.01.17 E:\Survivalcraft2 --ui-probe
tests\CelestialCodex.TargetTests\bin\Release\net472\CelestialCodex.TargetTests.exe E:\SCNET\SCNETx25.01.17 E:\Survivalcraft2 --https-probe
tests\CelestialCodex.Tests\bin\Release\net472\CelestialCodex.Tests.exe artifacts\celestial-api-fixtures
```

普通构建使用原生字体的真实字形尺寸、目标中文语言表与真实Widget布局/点击检测；没有用伪造游戏接口。UI测试通过延迟首载构造同一生产控件树，使用合成书籍数据，无HTTP、世界或用户进度写入；字体缓存只存在测试进程。测试覆盖320×240、375×812、812×375、1280×720、2340×1080、768×1024，两主题×五字号×四视图，检查固定按钮尺寸/边距/不被遮挡、正文换行滚动、长标题边界、原文缓存失效、设置返回位置。原权限、损坏JSON/文件、缓存预算、超时/取消、旧DLL负对照及CSV/XDB/PNG检查继续执行。

可选ui-probe通过实际目标Engine/OpenGL绘制15张合成界面图，位于`artifacts/CelestialCodex/ui-0.2.0/`；使用本机游戏字体，仅作本机取证，不提交或打包这些图。已实际查看小屏目录/书库、短横屏正文、设置、纸页与夜读。预览不是完整游戏或Android截图。机器无可用音频设备时OpenAL输出Device init failed，图像绘制正常完成；本Mod不播放新音效。

最终构建、包指纹及公网探测结果见本文下方交付记录。

### 最终交付记录

- 环境脚本通过：net472、API1.42、SC2.4.10.6与固定Windows三程序集SHA-256相符，未替换本机游戏程序集。
- Release **0错误、0警告**；纯客户端 **439项**通过，带原始API夹具为 **442项**；目标原生/ABI检查 **13936项**、最终DLL外部成员解析 **347条**通过，1个已安装包及其它工程的ID/GUID审计通过。计数包含原有冲突与负对照检查，不等同于13936项手机功能验收。
- 两份实际历史故障DLL继续被守卫按预期拒绝；15张原生合成预览生成成功。曾并发运行两个目标探测，竞争同一个member-refs.txt而有一次文件占用失败；改为顺序执行后HTTPS与UI探测均通过，普通构建原本就是顺序执行。
- 最终DLL公网目录/详情/正文 **3次200**：20本目录、clouds-and-stars/c1、正文2097 UTF-16单位，响应4279/362/6211字节；revision `1ab420b0764a28b0916d0708089e48de3a171f28e96c8c031324f5ad5e61f55e`。只读访问，没有生产变更。
- `artifacts/CelestialCodex/云天书典.netmod`与`云天书典-0.2.0.netmod`均 **26079字节 / 5条目**；SHA-256 **`210D991E40B386405591CC53641345FA5F1ADB923ABFA65A7961569A713A3E67`**；DLL MVID **`e76b0e0472cf45a3ac51272ed2e6f142`**。
- `git diff --check`通过；本轮只提交到本地main，不push。

### 修改文件范围

| 范围 | 文件 |
|---|---|
| 原生界面 | `src/CelestialCodex/UI/CodexDialog.cs`、新增`CodexWidgets.cs` |
| 布局/主题/进度 | `src/CelestialCodex/Core/ReaderLayout.cs`、`BoundedCache.cs` |
| 版本与构建 | `src/CelestialCodex/CelestialCodex.csproj`、`modinfo.json`、`Mod/CodexBlock.cs`、`scripts/Build-CelestialCodex.ps1` |
| 回归与预览 | `tests/CelestialCodex.Tests/Program.cs`、新增`ReaderPresentationChecks.cs`；`tests/CelestialCodex.TargetTests/Program.cs`、新增`NativeUiChecks.cs` |
| 说明 | 根README、书典README、COMPATIBILITY、blueprint-block-reference、本文 |

## Android与联机验收

用户已反馈此前书典测试成功；没有附该次精确版本/MVID、缓存重启和房间角色证明，记录为用户成功反馈。0.2.0新UI均待验证，不自动继承手机兼容结论。

1. 用新建可丢弃创造世界，替换为唯一的0.2.0书典包并重启；原缓存、世界和UserId.dat无需删除。朝天空使用956。
2. 刷新后选书/章节。检查长标题、作者/AI标识、数字章节名及是否仍出现“皮肤颜色”；核对该文字来自书典列表/正文还是其它面板。
3. 滑动至章节中段→设置→分别试5档字号/两主题→完成，检查位置与文本；回目录确认当前节高亮，测试首末节、目录分页与返回/关闭。
4. 重启后继续阅读，核对主题、字号和位置；已读章节断网可用，未缓存内容短反馈。网络加载中关闭/退出世界/重新进入，无迟到UI或闪退。
5. 在手机实际横屏、不同游戏界面缩放与系统最大文字设置下检查按钮可达性、刘海/挖孔/导航区域、长文性能。当前没有验证SCNET系统安全区API、系统字体联动或读屏桥接，不宣称完整无障碍支持；新增UI无动画，原生Dialog动画沿用客户端。
6. 分别记录Local、Server、Client，所有参与端安装相同0.2.0；仅本机读取API/缓存/位置，不写服务器阅读状态、玩家属性、地形或世界存档。

| 范围 | 状态 |
|---|---|
| 固定Windows ABI/Release/包、原生布局/点击/字体、Engine合成预览 | 已确认 |
| Android7723新界面、实际安全区/触屏/性能/缓存重启 | 待验证 |
| 真实Local、Server、Client阅读与同房安装验收 | 待验证 |
| 旧版与0.2.0混装联机 | 不支持 |
2026-11-12更新：本文为远端0.2.0界面开发历史。当前实现已迁至SCIE-Port/src/SCIEPort/Features/CelestialCodex，并统一为荒野探险家0.83.0单包；实际构建、裁剪BCL适配、验证及发布边界以[0.83记录](../SCIE-Port/docs/TIANGONG-DIRECT-0.83.0.md)为准，不再恢复独立书典工程或产物。
