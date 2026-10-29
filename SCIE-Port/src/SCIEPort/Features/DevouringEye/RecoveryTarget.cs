using Engine;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.DevouringEye
{
    internal sealed class RecoveryTarget
    {
        internal Ray3 Ray;
        internal Vector3 ActorPosition;
        internal Entity Entity;
        internal Point3 Cell;
        internal int Value;
        internal float Distance;
        internal bool Moving;
        internal string Name;
        internal bool Same(RecoveryTarget other) => other != null && Moving == other.Moving && (Entity != null ? Entity == other.Entity : other.Entity == null && Cell == other.Cell && Value == other.Value);
        internal string Describe() => Name + "\n类型：" + (Moving ? "移动方块（拒绝）" : Entity != null ? "持久化实体候选" : "地形方块") +
            (Entity != null ? "\n模板：" + Entity.ValuesDictionary.DatabaseObject.Name : "\n坐标：" + Cell + "  ID " + Terrain.ExtractContents(Value) + " DATA " + Terrain.ExtractData(Value)) + "\n距离：" + Distance.ToString("0.0") + " 格";
    }
}
