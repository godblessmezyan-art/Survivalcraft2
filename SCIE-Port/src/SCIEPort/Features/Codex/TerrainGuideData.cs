namespace SCIEPort.Features.Codex
{
    internal sealed class TerrainGuideEntry
    {
        internal readonly string Name, Text, SourceUrl;

        internal TerrainGuideEntry(string name, string landscape, string reference = null, string source = null, string url = null)
        {
            Name = name;
            Text = "游戏地形\n" + landscape;
            if (reference != null) Text += "\n\n现实取景\n" + reference + "\n\n资料来源　" + source;
            SourceUrl = url;
        }
    }

    internal static class TerrainGuideData
    {
        // Match GeologyKind and LandmarkKind order so additions can be checked against the generator.
        internal static readonly TerrainGuideEntry[][] Sections = { Wonders(), Landmarks(), Structures() };

        private static TerrainGuideEntry[] Wonders() => new TerrainGuideEntry[]
        {
            new TerrainGuideEntry("冷却火山口", "暖温内陆高地的黑色火山盆地。坑底有凝固岩面和裂纹，熔岩洞从盆底方向穿进山腹。",
                "美国夏威夷大岛的基拉韦厄伊基（Kīlauea Iki）曾在 1959 年喷发。熔岩喷泉把火山渣堆在坑边，熔岩在下方汇成炽热的湖，凝固后留下宽阔的黑色坑底。附近的纳胡库（Nāhuku）由熔岩管形成。熔岩流的外层先冷却，内部继续流动，排空后留下管道。游戏把这两处景观组合到一座火山。",
                "美国国家公园管理局 · 夏威夷火山国家公园", "https://www.nps.gov/places/kilauea-iki-overlook.htm"),
            new TerrainGuideEntry("余火火山口", "内陆高地的火山坑底保留一盆原生岩浆。高处可看清暗色坑壁、池岸和洞口的位置。",
                "现实可对照夏威夷大岛基拉韦厄峰顶的哈莱茂茂火山口（Halemaʻumaʻu）。美国地质调查局记录了 2008 至 2018 年的峰顶熔岩湖，其液面曾升降，个别阶段曾漫到坑底，2018 年岩浆撤出后峰顶发生大规模塌陷。游戏把这个动态过程定格为围岸内的小型岩浆池。",
                "美国地质调查局 · 基拉韦厄峰顶熔岩湖研究", "https://pubs.usgs.gov/publication/pp1867A"),
            new TerrainGuideEntry("三色火山湖", "火山环脊包围三座独立的矿色湖，湖间保留可走的岩地。湖色固定为乳白、青蓝和深红。",
                "印度尼西亚弗洛勒斯岛的克里穆图（Kelimutu）在山顶保留三座火山口湖，东侧两湖共用一道坑壁。NASA 对比多期 Landsat 影像，显示各湖会在青绿、棕红和深色等外观之间变化。火山气体与湖水的化学作用会改变其中矿物和溶解物的状态。游戏保留三湖紧邻与颜色反差，用固定矿色表现其中一种景象。",
                "NASA 地球观测站 · 克里穆图火山湖", "https://science.nasa.gov/earth/earth-observatory/volcanic-mood-rings-92377/"),
            new TerrainGuideEntry("钙华彩池", "暖湿的内陆缓坡上，白色钙华围出阶梯浅池，水从上层池埂的缺口落到下一层。",
                "四川省阿坝州松潘县的黄龙沟位于岷山高处。富含碳酸氢钙的水在坡面流动并逸出二氧化碳，碳酸钙逐渐沉积为浅色钙华，池埂随沉积增高。阳光、水深、池底和悬浮物共同影响可见色泽。游戏缩成三级池和明显溢口，以暖湿缓坡作为生成条件。",
                "联合国教科文组织 · 黄龙风景名胜区", "https://whc.unesco.org/en/list/638/"),
            new TerrainGuideEntry("七彩丘陵", "暖干内陆的起伏岩丘露出连续斜层。斜着越过山脊，可以从不同坡面追踪同一条色带。",
                "甘肃河西走廊南缘、祁连山北麓的张掖世界地质公园包括彩色丘陵与冰沟丹霞等地貌。彩色丘陵的岩层经构造抬升和倾斜，再由地表侵蚀揭露不同层位，从远处看像沿山坡延伸的条带。游戏提炼这种连续性，用赭红、橙黄、灰蓝和浅金四组彩岩模拟层序。",
                "联合国教科文组织 · 张掖世界地质公园", "https://www.unesco.org/en/iggp/zhangye-unesco-global-geopark"),
            new TerrainGuideEntry("黑沙柱岩海岸", "海岸从黑砂滩过渡到错落的玄武岩柱，海侧岩体间留有水洞。",
                "冰岛南岸维克附近的雷尼斯山（Reynisfjall）属于卡特拉火山系统。地质公园将山体追溯到冰期晚段的冰下或水下喷发，并记录了侵入其中的熔岩冷却收缩成多边形岩柱的过程。雷尼斯一带的暗色滩面与柱壁是游戏的构图依据，游戏还把可驶入的水洞安排在同一段海岸。",
                "卡特拉世界地质公园 · 雷尼斯山", "https://www.katlageopark.com/geosites/mainly-geology/reynisfjall/"),
            new TerrainGuideEntry("尖石迷宫", "偏暖的内陆出现成片石灰岩尖峰和窄缝，中间有弯曲的通道。",
                "马达加斯加西部梅拉基大区的贝马拉哈（Bemaraha）石灰岩高原，被峡谷与密集裂隙切开。水沿裂隙溶蚀碳酸盐岩，削尖的石脊和尖峰构成当地称作 Tsingy 的石林，部分高差可达约百米。这里的尖峰、窄缝和植被相间给了游戏迷宫的基本轮廓，中央通路是便于步行的改编。",
                "国际地质科学联合会 · 贝马拉哈尖石林", "https://iugs-geoheritage.org/geoheritage_sites/tsingy-of-bemaraha/"),
            new TerrainGuideEntry("地下河峡谷", "湿润内陆岩脊下藏着一段水道。河心较深，岩壁内沿有步行空间，洞道两端接向地表。",
                "斯洛文尼亚西南部克拉斯（Kras）高原的什科茨扬溶洞，保留了雷卡河（Reka）切入厚层石灰岩的一段地下峡谷。水从地表消失，在洞内经过跌水、崩塌洼地和巨大的洞厅。游戏用短而开敞的两端、浅水和岩壁旁的立足面呈现地下河的高低关系。",
                "联合国教科文组织 · 什科茨扬溶洞", "https://whc.unesco.org/en/list/390/"),
            new TerrainGuideEntry("蓝冰洞", "寒冷高地的山腹洞道，冰面和浅融水沟通向雪坡一带。",
                "冰岛瓦特纳冰川由多年积雪压实形成，冰体在自重下缓慢流向较低处。融水能沿冰体或冰床汇集并向冰舌末端排出，其通道随融化、降雪和冰体运动而改变。厚冰吸收较多红光，使穿过冰体的光偏蓝。游戏把这种冰壁和融水通道放进可探索的山腹，洞形由种子固定。",
                "瓦特纳冰川国家公园 · 冰川学", "https://www.vatnajokulsthjodgardur.is/en/melting-glaciers/glaciology"),
            new TerrainGuideEntry("天空之镜盐原", "干暖的低缓盐盆有白盐壳、浅卤池和岩岛。下雨时，露天盐面出现随天空变色的湿光。白盐壳可制盐晶。",
                "玻利维亚西南部高原的乌尤尼盐沼（Salar de Uyuni）干季铺展成白色盐壳，覆盖约一万平方公里。雨季积水在平整地面铺成薄层，低角度观察时能映出天空。NASA 用卫星影像跟踪过 2022 年较大范围的季节性积水。游戏用局部雨天反光和常见浅卤池表现干湿两种观感。",
                "NASA 地球观测站 · 玻利维亚乌尤尼盐沼", "https://science.nasa.gov/earth/earth-observatory/a-salt-bath-in-bolivia-149502/"),
            new TerrainGuideEntry("柱状节理台地", "暖内陆的阶梯状玄武岩平台交错抬高，平台之间留着蜿蜒低槽。",
                "现实可对照美国加利福尼亚内华达山脉的魔鬼柱堆（Devils Postpile）。厚熔岩流冷却时体积收缩，裂缝向内部延伸，把岩体分成常见六边形的柱。之后冰川侵蚀揭开柱顶和侧壁，部分顶面还能看到磨光痕。游戏用方格拼成柱群，保留了高低台阶和柱体横断面。",
                "美国国家公园管理局 · 魔鬼柱堆地质", "https://www.nps.gov/depo/learn/nature/geology.htm"),
            new TerrainGuideEntry("干峡谷", "暖干的内陆地面向碎石干河床下降。弯曲的主槽两侧有多层彩岩台阶。"),
            new TerrainGuideEntry("花岗岩穹丘", "略湿润的内陆有三座高低、宽窄不同的花岗岩圆丘。丘间有浅裂隙和碎石带。"),
            new TerrainGuideEntry("冲蚀沟壑", "暖干内陆的斜向支沟切开彩岩山脊，最终汇入弯曲的碎石主沟。"),
            new TerrainGuideEntry("冰蚀围谷", "寒冷高地的三面岩壁围住蓝冰谷底，敞开一侧用缓坡与外面的雪地相接。"),
            new TerrainGuideEntry("三桥天坑", "暖湿内陆高地有三道灰岩桥和两段拓宽的峡谷盆地。桥侧露出层纹灰岩，主要通路从桥下通过。",
                "重庆武隆的天生三桥跨在羊水河峡谷上，从上游到下游依次为天龙桥、青龙桥、黑龙桥。水沿石灰岩裂隙溶蚀，洞顶逐次塌落，较完整的岩体留下桥拱，桥间开敞处形成天坑。天龙桥有两个穿洞，青龙桥桥面高，黑龙桥拱洞较深且有悬泉。游戏保留三桥夹两坑的布局，缩短峡谷和桥距。",
                "重庆市武隆区人民政府 · 天生三桥", "https://cqwl.gov.cn/bmjz_sites/bm/wlw/zwxx_98939/jqjd/jdjd_1/202007/t20200702_7634529.html"),
            new TerrainGuideEntry("白沙蓝潟", "温暖湿润的近海陆地上，弯月白沙脊之间散落两至三座浅池。",
                "巴西东北部马拉尼昂州海岸的伦索伊斯－马拉年塞斯国家公园，位于亚马孙、塞拉多和卡廷加生物群区的过渡地带。盛行风把海岸沙粒堆成相连的新月形沙丘，雨季的淡水积在丘间低地，形成深浅、颜色各异的水面。游戏缩小沙丘并保留清楚的白沙边、弯月形脊和几处青蓝水池。",
                "联合国教科文组织 · 伦索伊斯－马拉年塞斯国家公园", "https://whc.unesco.org/en/list/1611/"),
            new TerrainGuideEntry("环礁蓝洞", "暖水浅海台地上有贝纹礁岩、浅色礁缘和一侧水道缺口。礁圈中央的竖洞可深至海面下约 27 格。",
                "伯利兹外海灯塔礁（Lighthouse Reef）中央附近的大蓝洞，是海水淹没的石灰岩洞穴。海面较低时洞穴曾露在空气里，留下钟乳石等记录；海面上升后，深洞被淹没，从上方看比周围浅礁更暗。游戏以浅色环礁与深色井口再现这种落差，并留出侧向的水道。",
                "NASA 地球观测站 · 灯塔礁与大蓝洞", "https://science.nasa.gov/earth/earth-observatory/lighthouse-reef-and-the-great-blue-hole-147158/"),
            new TerrainGuideEntry("冰川长脊", "寒冷内陆高地出现沿山势延伸的蓝冰舌。冰纹蓝冰横过部分冰面，冰舌两侧是碎石冰碛和花岗岩坡。",
                "瑞士伯尔尼和瓦莱一带的少女峰－阿莱奇，是阿尔卑斯山区具有连续冰川景观的世界遗产。大阿莱奇冰川由高处冰雪汇流向较低的谷地，冰流携带碎屑，汇流线上的岩屑在冰面形成醒目的纵向冰碛。裸露坡面也留下冰退的痕迹。游戏从中取了延伸的冰舌、岩壁和碎石带，并用冰纹标出冰面层次。",
                "联合国教科文组织 · 瑞士阿尔卑斯山少女峰－阿莱奇", "https://whc.unesco.org/en/list/1037/"),
            new TerrainGuideEntry("寒崖峡湾", "凉爽的海岸被幽深水道切开，两岸花岗岩崖壁错落，水道深处有浅滩。",
                "挪威西部的盖朗厄尔峡湾和纳柔依峡湾是冰川侵蚀形成的深谷被海水淹没后的地貌。两侧结晶岩壁陡峭，世遗资料记录局部岩壁可从海面升至约 1,400 米，并延伸到海面下 500 米。峭壁瀑布和水下冰碛是其典型细节。游戏保留低水道夹高崖的结构，并将峡湾尽头压缩成浅滩。",
                "联合国教科文组织 · 西挪威峡湾", "https://whc.unesco.org/en/list/1195/"),
            new TerrainGuideEntry("断云桌山", "暖湿内陆的砂岩平顶高出周围地面，陡壁露出彩岩带。一侧有斜向坡面通往高处。",
                "委内瑞拉东南部圭亚那高地的卡奈马国家公园，有大面积的桌状山（tepui）。巨大的平顶和近直立山壁把高处环境与四周低地隔开，许多瀑布从边缘跌落，安赫尔瀑布是知名例子。游戏提取突起的平顶和连续陡壁，用彩岩增强剖面，并给其中一侧加了一段游戏化登坡。",
                "联合国教科文组织 · 卡奈马国家公园", "https://whc.unesco.org/en/list/701/"),
            new TerrainGuideEntry("浅海礁道", "近海水面下有三条浅色礁脊，两条水道沿脊延伸。中脊有横向缺口，部分高脊露出贝纹礁岩。"),
            new TerrainGuideEntry("雪脊冰阶", "寒冷高地的蓝冰阶面与花岗岩肩交替出现。阶缘可见冰纹蓝冰，中央保留一条随山势升降的坡道。"),
            new TerrainGuideEntry("云瀑峰林", "暖润内陆有一圈叠纹石英砂岩峰柱。三道不同落差的瀑布汇进同一座潭，崖苔岩在高处峰肩显露。沿开口一侧的石道能经过高处观景台再下到潭边。",
                "湖南省张家界市武陵源以石英砂岩峰柱闻名。世遗资料记载，这片景区有三千多座狭窄砂岩柱，其中许多高逾二百米。峰柱之间的深谷和林冠强化了垂直高差。游戏提取密集峰柱与岩层纹理，把三道不同高度的水流、共用水潭和步行路线组合到一座较小的峡谷。",
                "联合国教科文组织 · 武陵源风景名胜区", "https://whc.unesco.org/en/list/640/")
        };

        private static TerrainGuideEntry[] Landmarks() => new TerrainGuideEntry[]
        {
            new TerrainGuideEntry("瀑布与跌水潭", "暖润内陆的断崖和普通坡地都可能形成瀑布。泉水沿岩面落入脚下小潭，较高的岩壁后有时藏着水帘洞。"),
            new TerrainGuideEntry("浅滩渡口", "水面连续、两岸可落脚的浅水区有间隔踏石，有时还能见到断木桥。"),
            new TerrainGuideEntry("樱花溪谷", "温润内陆的连贯溪岸长有数棵粉色樱花，岸边有时露出一小块平岩。"),
            new TerrainGuideEntry("海蚀拱门", "岩拱连接岸体和海中的石脚。拱孔下保留开放水面和水下通道。"),
            new TerrainGuideEntry("雪线岩棚", "雪线高处的岩壁有一处可站立的凹棚，天然岩顶挡在头上，棚底可搭篝火。"),
            new TerrainGuideEntry("立石环", "内陆非沙地的开阔处，八根高低错落的石柱围着一块石灰岩。"),
            new TerrainGuideEntry("风蚀蘑菇岩", "干热内陆的砂岩细柱顶着较宽的帽岩，远望像几把错落的石伞。"),
            new TerrainGuideEntry("冰塔林", "寒冷且稍湿润的高地上，数组蓝冰高塔顶着雪，塔间留有间隙。"),
            new TerrainGuideEntry("沙丘脊", "暖干连续沙地上，弯沙脊在中间拱起，沿两侧和两端逐渐融回沙地。"),
            new TerrainGuideEntry("海蚀柱群", "近岸浅海中，三组错落石柱由海床升出水面，主柱之间保留水路。"),
            new TerrainGuideEntry("天坑竖井", "内陆湿润平台上开有小型石灰岩塌口。坑底有碎石，一侧的天然台阶可上下。"),
            new TerrainGuideEntry("碎石坡扇", "山坡上缘的窄碎石带向下逐渐展宽，扇面中夹着就地露出的岩块。"),
            new TerrainGuideEntry("草覆岩丘", "暖湿内陆草地有三座低矮石丘，丘顶覆草，原来的草地穿过各丘之间。"),
            new TerrainGuideEntry("冻土石纹", "寒冷内陆高地的碎石接缝与薄雪构成错落斑块，低起伏处最容易看清。"),
            new TerrainGuideEntry("潮缘礁池", "温暖海岸稍高于海面的岩地上，石灰岩围住两座浅池，池底可见碎石。"),
            new TerrainGuideEntry("湿地草岛", "温暖湿润的浅水区有两片露出水面的草岛。草岛之间仍有敞开的水面。"),
            new TerrainGuideEntry("雪线风脊", "寒冷高地的低矮花岗岩脊中央略高，两端缓降，脊顶覆雪。"),
            new TerrainGuideEntry("红岩窄缝", "暖干沙地或砂岩地里的短窄沟，中段两侧岩壁错开，两头逐渐接回地表。")
        };

        private static TerrainGuideEntry[] Structures() => new TerrainGuideEntry[]
        {
            new TerrainGuideEntry("竹林", "暖湿低地的竹株高低错落，竹根保留时能够再长。林下可见竹笋。"),
            new TerrainGuideEntry("竹林小径", "竹株之间的短碎石径绕过植被，遇到坡地时逐格升降。"),
            new TerrainGuideEntry("竹亭", "竹林旁平缓的内陆水岸偶有坡顶竹亭。门朝陆地，碎石路可接回林缘。"),
            new TerrainGuideEntry("水幕沉殿", "海床上的原创遗迹。断塔、淹没的柱廊和两翼围着王座厅，部分高处留有换气空间。"),
            new TerrainGuideEntry("日阶神殿", "干暖内陆的原创阶梯金字塔。砂岩与灰白横带围成高台，入口内能看到主厅和两侧通廊。")
        };
    }
}
