# 0.43.1 装备页人物预览修复

2026-10-28。用户先报告ComponentSAClothing越界，随后确认是另一个已安装Mod造成，移除后已能加载。用户接着报告“贴图有了，模型仍旧”，并明确是在背包/装备页面的人物预览。不是已证实的世界人物附件失效。

## 已确认的原因与修复

0.43.0的世界绘制接入OnModelRendererDrawExtra不会影响ClothingWidget。目标Windows以及官方Android均使用ClothingWidget.m_outerClothingModelWidget -> PlayerModelWidget.m_modelWidget -> ModelWidget.Draw独立渲染装备页人物。之前漏掉此UI路径。

FourSaintsClothingPreview.Update从既有GuiUpdate入口取得当前ClothingWidget，仅将外层原生ModelWidget替换为派生ArmorModelWidget；保持同一宿主、子控件位置、皮肤/模型引用、骨骼动画、颜色、镜头、布局/渲染矩阵和交互状态。PlayerModelWidget继续写入旋转和Measure参数。替换仅首次打开该页面执行，后续不逐帧创建控件。其他Mod定制的ModelWidget子类跳过，避免再次覆盖第三方预览。

ArmorModelWidget.Draw先画原生衣物基础层，再从原生ModelWidget当前shader读取完整View/Projection（已包含UI大小、GlobalTransform、DPI和屏幕归一化）。使用当前UI骨骼数组、ModelMatrix和相同自动旋转，提交QinglongCrownRenderer.QueuePreview及FourSaintsArmorRenderer.QueuePreview；共享原有模型/纹理缓存和穿戴判断。UI路径不使用世界相机、相机距离或人物世界可见性判断。DrawMeshBlock先应用UI投影，之后Flush(Matrix.Identity)只提交一次投影；保留原生深度/裁剪。内层皮肤预览不显示外层装甲，保持游戏原有内外层语义。

新增Test-Environment检查实际ModelWidget.Draw可覆盖、公共骨骼/shader字段、ClothingWidget/PlayerModelWidget所有者字段、Children.IndexOf/Remove/Insert接口。Release编译及完整构建证据见TEST-RESULTS.md。没有修改四件几何、基础贴图、世界Hook行为、衣物203/200–203、属性技能、网络包240–245或存档。

## Android取证边界

从SCNET项目GitHub x25.01.17发布页取得官方APK，未安装、未改包，也未将程序集放入仓库。解包后核对ModelWidget、PlayerModelWidget、ClothingWidget和SubsystemModelsRenderer实现，UI/世界路径与Windows一致。

- 官方APK SHA256：B654F5AE8E9A3A9844ED461139F44707E265A3EEE53D81A874E2F1F8AD928A47
- 官方Android Survivalcraft.dll：3520162AFB8DCCC51DD32A2FE22114D0325DD55D270B4B922AABF16A4AA4429B
- 官方Android Engine.dll：8811109FDA2EF14AAA97DE537A686A355F32784996181C5A9FE9416ECBF314A7
- Windows三个目标程序集哈希、SCIE上游固定提交沿用0.43.0记录。

官方APK不含ComponentSAClothing。不能把官方包的取证结论直接当作7723重打包客户端真机通过。

## 验收与Git状态

本轮未操作用户游戏界面、未测试Android设备/主机/客户端。新包装备页四件单独/混穿、旋转、男女角色、不同分辨率、卸装重开和世界第三人称回归均待用户验收。安装时NetMods仅保留一个SCIEPort包。

按用户明确要求停止所有Git操作。上一步仅上传了未引用的blob/tree对象，未创建commit，也未更新main或任何远程分支；修复后不自动恢复提交。旧暂存区不是本修复的最终快照，后续只有用户重新授权提交后才能重新检查/暂存。


2026-10-28 后续：用户明确重新授权合并main并推送GitHub。恢复Git操作，合并远端0.44.0更新，最终版本0.44.1；上面的停止状态仅描述修复当时。
