using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Furniture
{
    // Templates are versioned by id: changing an existing shape requires a new id.
    internal sealed class FurnitureTemplate
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int SizeZ { get; private set; }
        public CellPos BoundsMin { get { return new CellPos(0, 0, 0); } }
        public CellPos BoundsMax { get { return new CellPos(SizeX - 1, SizeY - 1, SizeZ - 1); } }
        public IReadOnlyList<int> SupportedRotations { get; private set; }
        public IReadOnlyList<BlueprintCell> Cells { get; private set; }
        public string Backend { get { return "structure-blocks-v1"; } }

        internal FurnitureTemplate(string id, string name, Blueprint shape)
        {
            if (string.IsNullOrEmpty(id) || shape == null || shape.CellCount == 0)
                throw new ArgumentException("家具模板必须有 id 和非空形状。");
            Id = id; Name = name;
            SizeX = shape.SizeX; SizeY = shape.SizeY; SizeZ = shape.SizeZ;
            Cells = new List<BlueprintCell>(shape.Cells).AsReadOnly();
            SupportedRotations = Array.AsReadOnly(new[] { 0, 1, 2, 3 });
        }
    }
}
