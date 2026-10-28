# 阿比斯·终焉之镰 V0.1（port 0.16.0）

基线：port/scnet-x25.01.17 69db2ff。原创资产由New-AbyssReaperAssets.py可重复生成，无外部美术来源。仅新增此神器；没有合入main或改动Builder Assistant。没有传送门、黑洞、天气或地形写入。

## 模型与文件

Assets/Models/AbyssReaper.dae：Y_UP、identity单根、32 meshes、930 triangles。暗星铁柄、独立连接器/核心、三段有真实间隙的厚弧刃、五片碎片、尾端星锥。128×16全不透明色板PNG；模型/纹理由物品Initialize缓存，枚举全部mesh。实际细分弧刃只需930面，低于初始预算。几何绕序、法线和UV守卫见Test-AbyssGeometry.py。

比例图docs/assets/abyss-reaper/proportions.png是软件预览，使用CSV的.64第一人称和.80第三人称缩放；第三人称旁1.77格人体标尺。不是目标游戏截图，不证明持握位置、透视投影、动画或Android显示正确。

## ID与注册

Resolve-AbyssId.ps1扫描目标原生260个方块常量、本分支全部const Index以及origin/main保留号；949全知之眼、950吞噬之眼、951主线波塞冬已占用，首次选出952并生成AbyssId.cs。之后只验证且拒绝碰撞，不随新扫描结果静默重编号。CSV/XDB有独立资源路径与新GUID。其他Mod仍可能占952，需安装组合验证。

## 普攻和蓄力

原生UpdateInput之后，仅持此物品时捕获世界Hit/Dig/Aim，清除原生攻击/挖掘/Interact旁路；不创建普攻UI按钮。轻触松开依次右向左(.35s)、反向(.42s)、举镰下斩(.58s)，末击结束后1.05s连击超时重置。长按至少.65s松开触发.85s蓄力重斩，不自动连发。忙碌时松开不排队。切物品、死亡、移出Project、模态UI/对话框取消。

目标真实API：ComponentFirstPersonModel.ItemRotationOrder及ComponentCreatureModel.InHandItemRotationOrder。通过游戏更新持续下达不同旋转轨迹，原生插值处理；不是替换玩家骨骼。远程人物未同步自定义轨迹。

AbyssCombo的独立Impact边缘触发单次结算，命中与Draw解耦。48%时间点，普通3格/蓄力3.5格，水平扇区及1.7格高度限制，目标中心LOS墙体遮挡，实体HashSet去重，每击最多24实体，每次最多审查512个body。攻击力22/22/34/52，走原生AttackBody盔甲/伤害/联机路径。第三击及蓄力附带.22s锯齿裂纹线，不使用常驻光环。

## 配置与权限

XDB子系统参数AllowPlayers=false、AllowPassive=false、ShowRifts=true。默认仅LandPredator/WaterPredator，不攻击玩家及非掠食动物；此分类不等于实时仇恨，掠食类即使当下未追击仍会命中。玩家开关仍服从原生友伤设置。所有原生领地内目标保守跳过，不声称兼容第三方保护Mod。

Client从不结算自定义伤害；Server只允许本地IsMainPlayer输入，普通远程玩家战斗未开放，不新增网络包。客户端面板明确提示此限制。普通联机客户端战斗为不支持，主机伤害传播/动画仍待联机验证。

## UI及验收

非交互深黑紫金属边框面板，四个单色菱形槽标：裂界星门、断界斩、虚影瞬狩、终焉·万象坍缩，全部标明锁定，无释放逻辑。仅持物显示，近顶部中央，不拦截原生触控；卸装/Dispose移除引用。

待验收：Android7723冷启动、图标/第一人称与第三人称持握比例，左右手/不同UI分辨率遮挡，轻触三连轨迹、长按蓄力/模态取消，墙后及中立/玩家安全，原生领地、同一目标单击去重，关ShowRifts后伤害，主机伤害传播、切换世界与帧率。未操作游戏或连接设备，不能宣称Android兼容。

原创资产SHA256：DAE 1B0D0240C18B6BA60AF6EC263ECA5E98059CAA75434E54991604D6D708A5DF27；PNG 4F64390769ED1650503F93F50BCE5842FFBADE2BCE7CDF4443B13CABDFE71309。
