using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Home
{
    /// <summary>Five modern home additions; only new content identities are assigned.</summary>
    public sealed class HomeFireplaceBlock : HomeBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.Fireplace; }
    /// <summary>One table and two chairs sharing a protected two-by-two footprint.</summary>
    public sealed class HomeDiningSetBlock : HomeBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.Dining; }
    /// <summary>Double bed, using the native sleep suitability and sleep component.</summary>
    public sealed class HomeDoubleBedBlock : HomeBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.Bed; }
    /// <summary>Bedside cabinet with the same capacity policy as existing home storage.</summary>
    public sealed class HomeNightstandBlock : HomeBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.Nightstand; }
    /// <summary>Three original geometric artworks mounted to a vertical supporting face.</summary>
    public sealed class HomeWallArtBlock : HomeBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.WallArt; }

    /// <summary>Invisible occupancy, selection and collision; never an independent item.</summary>
    public abstract class HomeFootprintPartBlock : Block
    {
        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues() { yield break; }
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
            => BlockIndex == HomeRules.BedPart ? "双人床" : "双人餐桌";
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value), part = (data >> 2) & 3;
            if (part == 0 || data != ((part << 2) | (data & 3))) return new BoundingBox[0];
            return HomeExtensionGeometry.Boxes(HomeRules.PartRootId(BlockIndex), data & 3, part);
        }
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value) => GetCustomCollisionBoxes(terrain, value);
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment) { }
        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool debris) { debris = false; }
    }
    /// <summary>Dining-set occupancy has no bed suitability.</summary>
    public sealed class HomeDiningPartBlock : HomeFootprintPartBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.DiningPart; }
    /// <summary>Bed occupancy retains native sleep suitability across the whole mattress.</summary>
    public sealed class HomeBedPartBlock : HomeFootprintPartBlock { /// <summary>Content index.</summary>
        public const int Index = HomeRules.BedPart; }

    internal static class HomeExtensionGeometry
    {
        internal static Point3 Offset(int rotation, int part)
        {
            int x = part & 1, z = (part >> 1) & 1;
            switch (rotation & 3)
            {
                case 1: return new Point3(z, 0, -x);
                case 2: return new Point3(-x, 0, -z);
                case 3: return new Point3(-z, 0, x);
                default: return new Point3(x, 0, z);
            }
        }
        internal static bool Cell(Point3 root, int rotation, int part, out Point3 point)
        {
            Point3 delta = Offset(rotation, part);
            long x = (long)root.X + delta.X, z = (long)root.Z + delta.Z;
            point = root;
            if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue) return false;
            point = new Point3((int)x, root.Y, (int)z); return true;
        }
        internal static bool CanPlace(SubsystemTerrain terrain, Point3 root, int rotation)
        {
            if (root.Y < 1 || root.Y > 254) return false;
            for (int i = 0; i < 4; i++)
            {
                Point3 p;
                if (!Cell(root, rotation, i, out p)) return false;
                var chunk = terrain.Terrain.GetChunkAtCell(p.X, p.Z);
                if (chunk == null || chunk.State < TerrainChunkState.Valid || terrain.Terrain.GetCellContents(p.X, p.Y, p.Z) != 0) return false;
            }
            return true;
        }
        internal static bool HasWall(SubsystemTerrain terrain, Point3 root, int rotation)
        {
            bool supported;
            return TryWall(terrain, root, rotation, out supported) && supported;
        }
        // Unknown/unloaded support must not be treated as a deleted wall.
        internal static bool TryWall(SubsystemTerrain terrain, Point3 root, int rotation, out bool supported)
        {
            supported = false;
            Point3 back = Offset(rotation, 2), wall;
            long x = (long)root.X + back.X, z = (long)root.Z + back.Z;
            if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue) return true;
            wall = new Point3((int)x, root.Y, (int)z);
            var chunk = terrain.Terrain.GetChunkAtCell(wall.X, wall.Z);
            if (chunk == null || chunk.State < TerrainChunkState.Valid) return false;
            int contents = terrain.Terrain.GetCellContents(wall.X, wall.Y, wall.Z);
            supported = contents != 0 && BlocksManager.Blocks[contents] != null && BlocksManager.Blocks[contents].IsCollidable;
            return true;
        }
        private static BoundingBox B(float x, float y, float z, float a, float b, float c)
            => new BoundingBox(new Vector3(x, y, z), new Vector3(a, b, c));
        internal static BoundingBox[] ArtInteraction(int rotation) => Rotate(new[] { B(.10f, .12f, .91f, .90f, .88f, .99f) }, rotation, Vector3.Zero);
        internal static BoundingBox[] Boxes(int id, int rotation, int part)
        {
            var boxes = new List<BoundingBox>();
            if (id == HomeRules.WallArt) return boxes.ToArray();
            if (id == HomeRules.Fireplace) boxes.Add(B(.03f, 0, .52f, .97f, .96f, .98f));
            else if (id == HomeRules.Nightstand) boxes.Add(B(.18f, 0, .24f, .82f, .60f, .86f));
            else if (id == HomeRules.Bed)
            {
                boxes.Add(B(.04f, .12f, .04f, 1.96f, .64f, 1.96f));
                boxes.Add(B(.04f, .64f, 1.84f, 1.96f, .98f, 1.96f));
                boxes.Add(B(.18f, .04f, .18f, 1.82f, .24f, 1.82f));
            }
            else if (id == HomeRules.Dining)
            {
                boxes.Add(B(.55f, .72f, .34f, 1.45f, .78f, 1.66f));
                foreach (float x in new[] { .65f, 1.35f }) foreach (float z in new[] { .45f, 1.55f }) boxes.Add(B(x-.025f, 0, z-.025f, x+.025f, .73f, z+.025f));
                foreach (float x in new[] { .29f, 1.71f })
                {
                    boxes.Add(B(x-.25f, .37f, .74f, x+.25f, .46f, 1.26f));
                    float back = x < 1 ? .07f : 1.93f;
                    boxes.Add(B(back-.035f, .43f, .74f, back+.035f, .96f, 1.26f));
                    foreach (float legX in new[] { x-.18f, x+.18f }) foreach (float legZ in new[] { .81f, 1.19f }) boxes.Add(B(legX-.02f, 0, legZ-.02f, legX+.02f, .39f, legZ+.02f));
                }
            }
            Point3 canonical = Offset(0, part), rotated = Offset(rotation, part);
            if (HomeRules.HasFootprint(id))
            {
                var clipped = new List<BoundingBox>();
                foreach (BoundingBox b in boxes)
                {
                    Vector3 min = Vector3.Max(b.Min, new Vector3(canonical.X, 0, canonical.Z));
                    Vector3 max = Vector3.Min(b.Max, new Vector3(canonical.X + 1, 1, canonical.Z + 1));
                    if (min.X < max.X && min.Y < max.Y && min.Z < max.Z) clipped.Add(new BoundingBox(min, max));
                }
                return Rotate(clipped.ToArray(), rotation, new Vector3(rotated.X, 0, rotated.Z));
            }
            return Rotate(boxes.ToArray(), rotation, Vector3.Zero);
        }
        private static BoundingBox[] Rotate(BoundingBox[] boxes, int rotation, Vector3 cellOffset)
        {
            Matrix transform = Matrix.CreateTranslation(-.5f, 0, -.5f) * Matrix.CreateRotationY((rotation & 3) * MathUtils.PI / 2) * Matrix.CreateTranslation(.5f, 0, .5f);
            for (int i = 0; i < boxes.Length; i++)
            {
                Vector3 a = Vector3.Transform(boxes[i].Min, transform) - cellOffset, b = Vector3.Transform(boxes[i].Max, transform) - cellOffset;
                boxes[i] = new BoundingBox(Vector3.Min(a, b), Vector3.Max(a, b));
            }
            return boxes;
        }
    }
}
