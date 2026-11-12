using System;
namespace SCIEPort.Features.Codex
{
    internal static class CodexRules
    {
        internal const int Book=955;
        internal const uint AllMask=511;
        internal static readonly int[] Items={942,943,948,949,950,951,952,953,971};
        internal static readonly string[] Names={"妙尔尼尔","雷神之锤·伪","昆古尼尔","全知之眼","吞噬之眼","波塞冬","阿比斯·终焉之镰","圣剑·Excalibur","天工绘卷"};
        internal static readonly string[] Information={
            "北欧雷神的战锤，拥有物理攻击与独立雷电附伤。\n技能：雷霆万钧、雷霆王域、雷神降世。降世期间雷电强化、技能加速，并产生周期雷域。\n定位：高强度雷霆战斗神器。",
            "雷神之锤的仿制品，保留原来的造型与手感。\n长按瞄准蓄力后释放雷击与连锁雷电，近战带较弱雷电附伤。\n仿制品与妙尔尼尔分别解放，互不代替。",
            "北欧神枪，长按原生攻击蓄力投出，贯穿后自动返回。\n技能：卢恩神佑、神王敕令·命运裁断、永恒裁决。命运裁断由主机对符合规则的敌人随机结算；终式召来巨枪。",
            "万象解析之眼。\n打开后可读取位置、气候和方块资料，对准目标进行只读扫描，并开启全知视界。\n不会修改地形或实体，没有耐久消耗。",
            "单个目标的安全回收神器。\n先识别、再确认并执行回收；仅允许既有白名单坦克和普通方块。容器、未知实体及不满足安全条件的目标拒绝。\n解放不会授予管理权限，普通客户端仍只能识别。",
            "深海王权三叉戟。\n技能：水御潮环、沧海怒涛、海神领域。潮环提供护盾，巨浪向前推进，领域形成持续伤害的环形水龙卷。\n水中强化伤害与移动，领域期间缩短技能冷却。",
            "兼具虚空与死亡权柄的终焉之镰。\n虚空：蓄力重击、折跃、万象破碎、虚夜与共享星门。\n死亡：夺魂赋刃、死亡蔓延、冥界军势和次位面管理。\n军势可召唤冥界生物，切换护卫、自由行动或攻击模式。\n已有契约保留，解放后恢复技能及管理权限。",
            "湖中圣剑，银白剑刃、金色护手与深蓝握柄。\n湖光附魔：金光附刃、额外近战距离与独立圣光伤害。\n誓约胜利之剑：举剑下劈、圣光洪流与持续余光。\n阿瓦隆：短暂庇护、瞬间恢复与持续回血；完整阿瓦隆仍比圣剑珍贵十倍。",
            "复制、收藏与重建蓝图的绘卷。"
        };
        internal static uint Bit(int id){int i=Array.IndexOf(Items,id);return i<0?0:1u<<i;}
        internal static bool Newer(uint a,uint b)=>a!=b&&unchecked((int)(a-b))>0;
        internal static bool Unlocked(uint mask,int id)=>id==971||(Bit(id)!=0&&(mask&Bit(id))!=0);
    }
}
