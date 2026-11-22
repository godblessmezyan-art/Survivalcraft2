# 神器图谱的地形图鉴 1.9.0

手持神器图谱 955 点击使用，进入「地形图鉴」。奇景、地标、竹林与遗迹三栏共 46 条。左边或上方是目录，另一侧是独立滚动的正文。点击「返回神器」回到原来的神器条目。换掉手持物品、死亡或关闭时退出面板。

每条先介绍游戏里的外形和适生环境。有记录的现实参考再介绍地点、成因、景观特征和游戏改编。正文随模组提供，离线可读。17 个条目提供「资料来源」按钮，玩家主动点击后通过原生浏览器打开相关页面。其余条目介绍当前生成器表现，包含原创的水幕沉殿和日阶神殿。

图鉴复用神器图谱 955、既有使用入口与槽位主题。持有移速与近战攻速沿用图谱当前口径。图鉴使用原生控件和滚动布局，技能表现维持现有实现。本次为阅读功能，PixelFX 粒子和技能图标与本入口无关。

## 新生成材料

| 方块 | 方块 ID / DATA | 出现地形与表现 |
| --- | --- | --- |
| 贝纹礁岩 | 895 / 0 | 环礁蓝洞的外岸礁脊、浅海礁道的高脊。浅色岩面带弧形贝纹，与周围钙华石相间。 |
| 冰纹蓝冰 | 896 / 0 | 冰川长脊的横向冰带、雪脊冰阶的部分外缘。浅裂纹与深蓝冰层相间，摩擦参数与原有蓝冰一致。 |
| 层纹灰岩 | 897 / 0 | 三桥天坑的部分峡谷肩面和桥侧。侧面有浅深相间的沉积纹，步行底面与跨梁仍用既有灰岩。 |

三种都是可采掘、可放置的实心方块，挖取掉落自身。创造栏归「自然与生物」。地形生成只在既有形态的表面按位置选择材料，使用原有区块保护、列校验和写入范围。已存区块由原存档加载，新增露头随新区块探索出现。`New-GeologyAtlas.py` 重建原创像素图集，原有地质和盐晶工艺纹理保留原像素。

## 现实取景资料

| 游戏条目 | 现实取景或类比 | 资料 |
| --- | --- | --- |
| 冷却火山口 | 美国夏威夷的基拉韦厄伊基和附近独立的纳胡库熔岩管 | [美国国家公园管理局 · 火山坑](https://www.nps.gov/places/kilauea-iki-overlook.htm)、[熔岩管](https://www.nps.gov/havo/planyourvisit/day-hike-nahuku.htm) |
| 余火火山口 | 地学类比为夏威夷哈莱茂茂火山口 2008–2018 年的熔岩湖 | [美国地质调查局 · 峰顶熔岩湖研究](https://pubs.usgs.gov/publication/pp1867A) |
| 三色火山湖 | 印度尼西亚弗洛勒斯岛的克里穆图三座火山口湖 | [NASA 地球观测站](https://science.nasa.gov/earth/earth-observatory/volcanic-mood-rings-92377/) |
| 钙华彩池 | 中国四川松潘的黄龙钙华梯池 | [联合国教科文组织 · 黄龙](https://whc.unesco.org/en/list/638/) |
| 七彩丘陵 | 中国甘肃张掖世界地质公园的彩色丘陵 | [联合国教科文组织 · 张掖](https://www.unesco.org/en/iggp/zhangye-unesco-global-geopark) |
| 黑沙柱岩海岸 | 冰岛南岸卡特拉地质公园的雷尼斯一带 | [卡特拉世界地质公园 · 雷尼斯山](https://www.katlageopark.com/geosites/mainly-geology/reynisfjall/) |
| 尖石迷宫 | 马达加斯加西部贝马拉哈尖石林 | [国际地质科学联合会](https://iugs-geoheritage.org/geoheritage_sites/tsingy-of-bemaraha/) |
| 地下河峡谷 | 斯洛文尼亚什科茨扬溶洞和雷卡河 | [联合国教科文组织 · 什科茨扬溶洞](https://whc.unesco.org/en/list/390/) |
| 蓝冰洞 | 冰岛瓦特纳冰川与冰下融水通道 | [瓦特纳冰川国家公园](https://www.vatnajokulsthjodgardur.is/en/melting-glaciers/glaciology)、[美国地质调查局 · 冰的颜色](https://www.usgs.gov/faqs/why-glacier-ice-blue) |
| 天空之镜盐原 | 玻利维亚乌尤尼盐沼 | [NASA 地球观测站](https://science.nasa.gov/earth/earth-observatory/a-salt-bath-in-bolivia-149502/) |
| 柱状节理台地 | 地学类比为美国加利福尼亚魔鬼柱堆 | [美国国家公园管理局 · 魔鬼柱堆地质](https://www.nps.gov/depo/learn/nature/geology.htm) |
| 三桥天坑 | 中国重庆武隆的天龙桥、青龙桥和黑龙桥 | [武隆区人民政府 · 天生三桥](https://cqwl.gov.cn/bmjz_sites/bm/wlw/zwxx_98939/jqjd/jdjd_1/202007/t20200702_7634529.html)、[联合国教科文组织 · 中国南方喀斯特](https://whc.unesco.org/en/list/1248/) |
| 白沙蓝潟 | 巴西东北部伦索伊斯－马拉年塞斯的海岸沙丘与雨季潟湖 | [联合国教科文组织 · 国家公园](https://whc.unesco.org/en/list/1611/) |
| 环礁蓝洞 | 伯利兹灯塔礁的大蓝洞 | [NASA 地球观测站](https://science.nasa.gov/earth/earth-observatory/lighthouse-reef-and-the-great-blue-hole-147158/) |
| 冰川长脊 | 瑞士阿尔卑斯山少女峰－阿莱奇的冰川谷 | [联合国教科文组织 · 少女峰－阿莱奇](https://whc.unesco.org/en/list/1037/) |
| 寒崖峡湾 | 挪威的盖朗厄尔峡湾和纳柔依峡湾 | [联合国教科文组织 · 西挪威峡湾](https://whc.unesco.org/en/list/1195/) |
| 断云桌山 | 委内瑞拉卡奈马国家公园的桌状山 | [联合国教科文组织 · 卡奈马国家公园](https://whc.unesco.org/en/list/701/) |
