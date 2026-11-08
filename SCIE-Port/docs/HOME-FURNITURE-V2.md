# 现代科技家居 V2：本地源码与资源修改

日期：2026-11-08。基于本地0.78.0重构；最初按用户指令只修改源码/资源。用户随后要求打包，当前已生成 **0.79.0**，包含本页V2及五类家居扩展。固定Windows Release、完整旧检查、24模型实际Engine导入、494104规则与325原生实体/容量/保存/掉落/碰撞隔离检查通过；没有启动游戏或推送，Android与真实联机仍待验证。[实际命令和哈希](TEST-RESULTS.md)。

## 实际美术范围

统一使用冰川白 `#E5EAEE`、钛银灰 `#A4B1BD`、石墨黑 `#303943`、深空蓝 `#546C80`、冰蓝 `#65D5EC`；浅银灰织物、浅灰石材仅作该色板的明度延伸。冰蓝只占显示面板、状态灯及已有水流外观的小面积，不新增实时光源。全部模型、纹理为本项目原创程序化资产，没有引入第三方家具。

| 家具 | 实际资源改动 |
|---|---|
| 冰箱，含开门状态 | 钛银壳体与门、石墨门封/嵌入把手、银色窄拉手；上门增加黑色面板、细状态刻线和冰蓝点；面板与把手随原 72° 开门姿态移动 |
| 直柜、转角柜、吊柜、抽屉柜 | 全部从原木/框线门改为石墨壳体、冰川白平齐门板和浅灰石台面；统一门缝、嵌入拉手、内缩踢脚与银色唇边；转角侧门沿用相同细节，吊柜不加落地踢脚 |
| 水槽，含水流状态 | 共用新橱柜；不锈钢色槽体，保留约 0.23 格内凹深度，增加深色排水孔；提高龙头弧线与出水口，细化混水杆比例，已有水流使用冰蓝不透明色 |
| 五种沙发模块 | 雾白细织纹、分层坐垫/靠背/包边，深空蓝抱枕；内缩石墨底座、窄银边与阴影层形成悬浮视觉；保留模块方向与转角结构 |
| 茶几 | 深色不透明玻璃外观台面，贴图中有克制的斜向明暗带和蓝灰薄边；钛银细腿/横撑、石墨脚垫；装饰改为薄型平板与瓷杯，无新增可取物品 |
| 窗帘，含开合状态 | 浅银灰织纹、规律渐变褶皱，薄型钛银导轨、深色轨槽与隐藏滑块；保留两格结构和无实体碰撞 |
| 悬浮陈列器 954 | 只调整现有标记顶点色与单槽面板为白/银/深空蓝和少量冰蓝；没有实体柜体，不给陈列物本身染色，不改变其悬浮/旋转行为 |

渲染仍走原 `HomeBlock` / `BlockMesh` / `SubsetOpaque` 与 `HomeFurniture.png`。没有改成 PBR、透明玻璃、反射、法线贴图、额外着色器或实时灯。细拉丝、布纹、微弱石纹、玻璃明暗带均写在原 512×512 全不透明图集，各部件继续使用独立的图集区域与 `HomeUV`。Blender 预览也不启用金属/透射/发光材质，减少预览与游戏能力的差距；预览灯光仍不能代替游戏光照。

16 个现有 DAE 的文件名、单 identity 场景节点、Y_UP、米单位、前方 -Z、位置/法线 `(x,z,-y)`、UV 的 V 翻转和独立索引导出结构保持。导出器记账为 29,244 三角面，原 V1 为 30,000；这不是目标 Engine 实际导入或手机性能测试。C# 放置、碰撞、模块/开合 DATA、创造栏条目、掉落、配方及家居交互包均未改变。物品栏仍使用原有按实际包围盒居中逻辑，直接显示新模型/图集。

## 容量与旧存档

源码/数据库扫描定位到自定义 `ComponentHomeStorage` 和 `ComponentSuspendedItem` 两类原生 Chest 派生容器。当前工程没有其它已实现的自定义储物柜；车厢文档中的未实现货箱没有可减半的运行时容量。

| 自定义容器 | 原可存槽数 | V2 新增存放槽数 | 底层保留槽数 |
|---|---:|---:|---:|
| 冰箱 957 | 16 | 8 | 16 |
| 直柜 958 / DATA0 | 16 | 8 | 16 |
| 转角柜 958 / DATA8 | 16 | 8 | 16 |
| 吊柜 958 / DATA16 | 16 | 8 | 16 |
| 抽屉柜 958 / DATA24 | 16 | 8 | 16 |
| 悬浮陈列器 954 | 1（且每槽仅一件） | 1，即 ceil(1/2) | 1 |

`HomeRules.Slots=16` 和 XDB `SlotsCount=16` 故意保留，新增 `UsableSlots=(Slots+1)/2`。不裁剪 `m_slots`，不改 `SlotsCount`/`VisibleSlotsCount`，不重编号、不自动搬运、不合堆，也不引入新存档键。原生 Load 仍加载全部旧 `Slots/Slot0..Slot15`，Save 与拆除掉落仍遍历全部底层槽，原 `Id`、实体 GUID 与物品 value/DATA 保留。

`ComponentHomeStorage.GetSlotCapacity` 只对 0–7 返回原堆叠容量，对 8–15 返回 0；`AddNetSlotItems` 再检查正数、容量和加法溢出。既有主机 `HandleMoveItem`/`HandleDragDrop` 会调用该组件的虚方法，限制不只存在于客户端面板。`RemoveNetSlotItems` 拒绝负数；`SetSlotValue` 在主机禁止历史槽新增/增量替换，在客户端接受服务器的全量槽快照，避免把旧物误当成非法存入。历史槽的加工容量为零。

有历史物品时面板保留 4×4 显示，后两行以深色区分，标题仅追加“旧物取出”；点按历史槽通过既有 `HandleMoveItem` 转到背包可接收槽位，背包满时物品仍留在原位。一次点按最多迁移一个目标槽可容纳的数量，部分取出后剩余数量仍保存在旧槽；可反复点按取完。历史区不接受拖入、交换、拆分源或加工拖放。正常前 8 格仍使用原生触屏拖放。最后一个历史槽清空后自动收起后两行，底层槽仍保留且不能再存入。

采用点按取出的原因：原生 `HandleDragDrop` 的加工路径先扣源，再尝试把加工结果放回源；只取槽的回填容量为零，直接保留该拖放可能丢失加工结果。本面板不从历史槽启动此路径，不用缩数组或删除物品规避。普通加工应先取到背包，再在正常槽操作。

## 兼容边界与未确认项

- 已发布 Block ID 954、957–963、全部 DATA、实体/参数 GUID、组件类名、存档 `Id/Slots/SlotN`、包 239 与原生槽包的字节格式不变；不改原版容器类、模板或箱子容量。`ComponentHomeStorage` 只是从原文件拆到独立文件，完整命名空间/类名保持。
- 0.79.0已有本轮Windows编译、Engine导入及原生库存保存/重载/取出/拆除隔离证据；不能继承为Android、触屏、完整世界或真实Local/Server/Client验证。V1的0.78.0仍保留为历史包。
- 原生包没有新增服务端来源/目标权限钩子。本轮限制约束自有组件的容量写入，安全取出 UI 使用原生普通移动；不能据此承诺修复原生包越权问题。绕开本面板直接构造原生加工包、其它 Mod 直接写公开 `m_slots` 或反射改内部数据，不在此无损保证内；该组合的端到端审计/拦截仍是阻塞项，未通过允许历史槽存入来掩盖。已有物品数据保留，不清空隔离槽。各端需使用同一份后续自行构建的 V2 代码，混版行为待验证。
- 真实金属反射、透明玻璃和灯光照亮周围不属于本次实现；Android 的可辨识度、贴图滤波、法线/平滑导入、褶皱观感与多柜同屏性能待用户实际检查。
- 本次用户授权打包后完成相关Windows检查，按根规范只在本地提交，不推送GitHub；原始“未构建”阶段的记录保留在TEST-RESULTS历史段落。

## 修改文件清单

- 源码：`Features/Home/HomeRules.cs`、`SubsystemHomeFurniture.cs`、新增 `ComponentHomeStorage.cs` / `HomeStorageWidget.cs`；`Features/Suspension/SuspendedItemBlock.cs` / `SuspendedItemWidget.cs`。
- 资源（位于 `src/SCIEPort/Assets`）：`Textures/HomeFurniture.png`；`Models/HomeFridge.dae` / `HomeFridgeOpen.dae`、`HomeCabinet0.dae`–`HomeCabinet3.dae`、`HomeSink.dae` / `HomeSinkOpen.dae`、`HomeSofa0.dae`–`HomeSofa4.dae`、`HomeCoffeeTable.dae`、`HomeCurtain.dae` / `HomeCurtainOpen.dae`。数据库和 CSV 未修改。
- 制作脚本：`scripts/New-HomeFurnitureAssets.py`、`scripts/Prepare-HomePreviews.py`。
- 文档：本文、`docs/HOME-FURNITURE.md`、`docs/COMPATIBILITY.md`、`docs/TEST-RESULTS.md`、`README.md`、根 `docs/COMPATIBILITY.md` / `docs/blueprint-block-reference.md`。
- 来源/制作记录：`docs/assets/home-furniture/manifest.json`、`reference_manifest.json`、`registration_report.json`、`atlas_regions.json`、`validation/multiview_fit_report.json`、`BUILD_NOTES.md` 和真实 Blender 预览 `overview.png`。
- 忽略的本地产物：`artifacts/home-furniture-v2/ModernFurnitureV2.blend`（打包图集）、16 个导出记账 JSON、前/后视 PNG、`all-variants.png`。原 V1 的本地 `.blend` 和旧安装包保留，未覆盖为 V2。
