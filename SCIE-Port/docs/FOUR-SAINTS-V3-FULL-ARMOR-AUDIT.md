# 四圣兽V3整套：0.43.0 统一验收候选

2026-10-28。用户明确要求补齐其余三件后交付一个netmod，由其亲自统一验收，不再由Agent操作游戏界面。青龙冠沿用0.42.0真实3D网格，新增三套真实立体装甲，不以基础贴图或短时符纹替代。

## 内容与实际结构

| 衣物数据 | 资源 | 实际导入骨骼/网格/三角面 | 挂接 |
|---|---|---|---|
| 200 万里青龙 | QinglongCrown.dae | 1 / 31 / 680 | Head |
| 201 北冥玄武 | XuanwuArmorV3.dae | 1 / 40 / 1212 | Body |
| 202 镇狱白虎 | BaihuLegArmorV3.dae | 3 / 36 / 804 | Leg1 / Leg2，独立左右几何 |
| 203 炽焰朱雀 | ZhuqueBootsV3.dae | 3 / 30 / 596 | Leg1 / Leg2，独立左右几何 |

后三件共享FourSaintsArmorV3.png（256×16不透明色板），玄铁内衬和古金色与青龙保持一致。整套3292三角面；新装甲31/40/36/30的结构网格在加载时合并到Head/Body/双腿的缓存BlockMesh，绘制时不按每个装饰件建立批次。后三件五个跟骨部件共用一个纹理批次，每位角色每相机只Flush一次；青龙单独共用已有冠体材质批次。调试轴默认关闭。

- 玄武：多层凸起六角胸甲、龟壳形双肩、隆起背板、真实管状盘蛇与独立蛇首、金属连接扣和分片腰裙。腰裙最低点在原生髋骨摆动区域之上。
- 白虎：双腿分段银甲、立体石墨虎纹构件、成对虎首膝甲、各两根向下收尖的獠牙、外侧后掠护胫和后小腿甲片。左右分开建模，不在运行时负缩放镜像，也不把一整条裤子绑在Body上。
- 朱雀：实厚深色鞋底/金边、尖鸟喙鞋头和凸金脊、赤红脚踝甲、每侧三片薄羽翼和羽轴、后扬短尾羽。羽翼位于脚踝外侧，以薄片实厚几何表现羽片，未加机械翼骨架或常驻发光。
- 四件创造栏、快捷栏及装备栏图标都来自各自实际独立网格，保留既有衣物块203和数据200–203。

## 渲染接入与性能

新增FourSaintsArmorRenderer.cs，从现有OnModelRendererDrawExtra绘制，与QinglongCrownRenderer并列；ClothingBlock.DrawBlock按四件衣物数据分别取模型图标。绘制启用取决于实际穿戴，不取决于回血/技能/涅槃状态，也不新增网络字段。FourSaintsVisuals.Reset沿用现有Project销毁入口清理新缓存。

实际目标Engine和游戏程序集已核实m_bodyBone、m_leg1Bone、m_leg2Bone和现有绘制API；缓存原始绑定基逆矩阵，乘当前完整动画矩阵与camera.InvertedViewMatrix。原生Body原点在脚下附近；Leg1/Leg2的髋骨原点在男性约0.71184米、女性约0.67621米，而不是膝/踝位置。模型以这些真实原点制作。女性躯干/腿长保留0.95比例，腿部X宽度按实际原生几何近似不缩，避免鞋底高度随性别漂移或双腿装甲挤入中线。

模型、纹理、三个部位的绑定矩阵、图标均缓存复用。每帧只提交缓存顶点的当前骨骼变换，不创建新模型/纹理/状态列表。沿用0.45米相机近头裁剪与48米距离裁剪，避免本地第一人称遮挡。人物隐藏/淡出、死亡、卸装后不再提交；使用头部地形光照，非自发光。投影只在Flush时应用一次。世界重载不保留玩家或Project引用，GPU内容资源由ContentManager管理。

设置：SCIEPortFourSaintsArmorV3Preview默认true，SCIEPortFourSaintsArmorDiagnostics默认false；冠体仍沿用SCIEPortQinglongCrownV3Preview/Diagnostics。都通过既有SaveSettings/LoadSettings入口保存，未改变玩家正常技能操作。

## 本机验证与边界

新增Test-FourSaintsArmorV3.ps1并接入完整构建。检查每个三角面的索引、非退化、法线与绕序、UV、部件结构、移动端面数预算、纹理完全不透明；然后用真实Engine.Media.ModelData.Load导入三件DAE并核对Body和双Leg分组。男女原生模型仅在内存读取，不复制或打包人物资产。30次Body/Leg1/Leg2合成角度及相机往返测试、鞋底距地面1cm以内、静态左右腿中线至少5mm净空、腰裙高于髋骨至少2cm均通过。这些是离线几何和矩阵检查，**不代替游戏内动画、穿模与帧率验收**。

目标：D:\Games\SCNET-x25.01.17；SDK9.0.300；net472引用包来自Microsoft.NETFramework.ReferenceAssemblies.net472 1.0.3，FrameworkPathOverride指向D:\Dev\Tools\net472\build\.NETFramework\v4.7.2。构建命令、最终包条目/字节/SHA256见TEST-RESULTS.md。

目标哈希与上一阶段相同：Survivalcraft.exe `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`；Engine.dll `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`；EntitySystem.dll `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`。SCIE只读上游为4864d956793b6e670413019f1f6b1cc5c8bb0970，本次美术为原创，不使用上游装甲。

实际资源SHA256：

- XuanwuArmorV3.dae：2FD471578046520C2967696414886B1A284CF9CF362631A6182E57B35D07435D
- BaihuLegArmorV3.dae：65F4ADEAE161FA24D0C94F44E415724826154E84A586BCDFEA21B047C3B1D6F4
- ZhuqueBootsV3.dae：EBB304B8CDA15B0D90A6167EA726CA9FC513477562CCCB124152822DF08378E1
- FourSaintsArmorV3.png：2101375E160068E978C72294398C2FD390F125691E5068F100E7366A46D498EC

脚本New-FourSaintsArmorV3Assets.mjs用Node内置模块从原创参数化几何生成资源、正背左右侧/三分之四/白模及整套离线审查图。docs/assets/four-saints-v3图片全部是实际导出几何的离线预览，非游戏截图，不包含原生人物资产。可复现生成不需要第三方美术库。

## 不变项与统一验收

未改.clo及基础层贴图、衣物203/200–203、角色属性/技能/伤害/治疗/体温、碰撞、GUID、网络包240–245、存档逻辑和Builder Assistant；blueprint-block-reference.md能力清单没有变化。无新增上游素材或许可范围。

模型生成/真实导入/矩阵检查/Release编译与打包已确认。Windows带Mod画面、Android7723五种动作及第一人称、男女角色单件/组合穿戴、换装/死亡/读档、主机/普通客户端/观察者、多人同屏帧率全部待验证，交由用户一次验收；未声称Android或联机通过。候选的剩余风险包括动态姿势穿插、图标实际尺寸、不同皮肤或模型替换Mod、移动端DrawCall/帧率和客户端衣物更新时机。下一步最小任务是用户用可丢弃备份世界安装唯一一份0.43.0后统一验收四件，报告截图/日志以便集中修正。
