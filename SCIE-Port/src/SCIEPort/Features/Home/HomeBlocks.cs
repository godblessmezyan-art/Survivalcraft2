using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Home
{
    /// <summary>Original furniture using baked Y-up meshes and a shared opaque PNG atlas.</summary>
    public abstract class HomeBlock : Block
    {
        private readonly Dictionary<int, BlockMesh> meshes = new Dictionary<int, BlockMesh>();
        private readonly Dictionary<int, BoundingBox[]> collisions = new Dictionary<int, BoundingBox[]>();
        private readonly Dictionary<int, Vector3> iconOffsets = new Dictionary<int, Vector3>();
        private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize()
        {
            texture = ContentManager.Get<Texture2D>("Textures/HomeFurniture");
            for (int variant = 0; variant < HomeRules.VariantCount(BlockIndex); variant++)
            for (int state = 0; state < (HomeRules.IsSwitchable(BlockIndex) ? 2 : 1); state++)
            {
                int data = (variant << 3) | (state << 2);
                Model model = ContentManager.Get<Model>("Models/" + HomeRules.Model(BlockIndex, data));
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var mesh = new BlockMesh();
                    Matrix placement = Matrix.CreateRotationY(rotation * MathUtils.PI / 2f) * Matrix.CreateTranslation(.5f, 0, .5f);
                    foreach (ModelMesh source in model.Meshes)
                    foreach (ModelMeshPart part in source.MeshParts)
                        mesh.AppendModelMeshPart(part, BlockMesh.GetBoneAbsoluteTransform(source.ParentBone) * placement, false, false, false, false, Color.White);
                    mesh.GenerateSidesData();
                    meshes[data | rotation] = mesh;
                    if (state == 0 && rotation == 0)
                    {
                        BoundingBox bounds = mesh.CalculateBoundingBox();
                        iconOffsets[data] = -.5f * (bounds.Min + bounds.Max);
                    }
                    collisions[data | rotation] = Boxes(BlockIndex, variant, rotation, false, state != 0);
                }
            }
            base.Initialize();
        }
        internal static BoundingBox[] Boxes(int id, int variant, int rotation, bool upper, bool open)
        {
            if (id >= HomeRules.Fireplace && id <= HomeRules.WallArt) return HomeExtensionGeometry.Boxes(id, rotation, 0);
            var boxes = new List<BoundingBox>();
            if (id == HomeRules.Curtain)
            {
                // Fabric is traversable. The raycast still selects the top rail/panels.
                return new BoundingBox[0];
            }
            if (id == HomeRules.Fridge) boxes.Add(new BoundingBox(new Vector3(.07f, 0, .12f), new Vector3(.93f, upper ? .90f : 1, .94f)));
            else if (id == HomeRules.Cabinet && variant == 2) boxes.Add(new BoundingBox(new Vector3(.02f, .32f, .42f), new Vector3(.98f, .85f, .97f)));
            else if (id == HomeRules.Cabinet || id == HomeRules.Sink) boxes.Add(new BoundingBox(new Vector3(.02f, 0, .04f), new Vector3(.98f, .85f, .97f)));
            else if (id == HomeRules.Sofa)
            {
                boxes.Add(new BoundingBox(new Vector3(.03f, .08f, .05f), new Vector3(.97f, .52f, .96f)));
                boxes.Add(new BoundingBox(new Vector3(.03f, .52f, .77f), new Vector3(.97f, .95f, .96f)));
                if (variant == 0 || variant == 1 || variant == 4) boxes.Add(new BoundingBox(new Vector3(.03f, .4f, .05f), new Vector3(.17f, .72f, .9f)));
                if (variant == 0 || variant == 3) boxes.Add(new BoundingBox(new Vector3(.83f, .4f, .05f), new Vector3(.97f, .72f, .9f)));
                if (variant == 4) boxes.Add(new BoundingBox(new Vector3(.03f, .52f, .06f), new Vector3(.24f, .94f, .90f)));
            }
            else
            {
                boxes.Add(new BoundingBox(new Vector3(.07f, .37f, .12f), new Vector3(.93f, .45f, .88f)));
                boxes.Add(new BoundingBox(new Vector3(.13f, 0, .19f), new Vector3(.24f, .37f, .30f)));
                boxes.Add(new BoundingBox(new Vector3(.76f, 0, .19f), new Vector3(.87f, .37f, .30f)));
                boxes.Add(new BoundingBox(new Vector3(.13f, 0, .70f), new Vector3(.24f, .37f, .81f)));
                boxes.Add(new BoundingBox(new Vector3(.76f, 0, .70f), new Vector3(.87f, .37f, .81f)));
            }
            Matrix transform = Matrix.CreateTranslation(-.5f, 0, -.5f) * Matrix.CreateRotationY(rotation * MathUtils.PI / 2) * Matrix.CreateTranslation(.5f, 0, .5f);
            for (int i = 0; i < boxes.Count; i++)
            {
                BoundingBox b = boxes[i]; Vector3 a = Vector3.Transform(b.Min, transform), c = Vector3.Transform(b.Max, transform);
                boxes[i] = new BoundingBox(Vector3.Min(a, c), Vector3.Max(a, c));
            }
            return boxes.ToArray();
        }
        private int Data(int value) => HomeRules.Normalize(BlockIndex, Terrain.ExtractData(value));
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => collisions[Data(value)];
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
            => BlockIndex == HomeRules.Curtain ? new[] { new BoundingBox(new Vector3(.02f, 0, .02f), new Vector3(.98f, 1.98f, .98f)) }
                : BlockIndex == HomeRules.WallArt ? HomeExtensionGeometry.ArtInteraction(HomeRules.Rotation(Terrain.ExtractData(value))) : GetCustomCollisionBoxes(terrain, value);
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
            => generator.GenerateShadedMeshVertices(this, x, y, z, meshes[Data(value)], Color.White, null, null, geometry.GetGeometry(texture).SubsetOpaque);
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, meshes[HomeRules.ItemData(BlockIndex, Terrain.ExtractData(value))], texture, color, size, ref matrix, environment);
        /// <inheritdoc/>
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environment) => iconOffsets[HomeRules.ItemData(BlockIndex, Terrain.ExtractData(value))];
        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues()
        {
            for (int v = 0; v < HomeRules.VariantCount(BlockIndex); v++) yield return Terrain.MakeBlockValue(BlockIndex, 0, v << 3);
        }
        /// <inheritdoc/>
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
        {
            Vector3 forward = Matrix.CreateFromQuaternion(miner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            int rotation = HomeRules.Facing(forward.X, forward.Z);
            Point3 p = hit.CellFace.Point + CellFace.FaceToPoint3(hit.CellFace.Face);
            if (BlockIndex == HomeRules.WallArt)
            {
                Point3 normal = CellFace.FaceToPoint3(hit.CellFace.Face);
                if (normal.Y != 0) return new BlockPlacementData { CellFace = hit.CellFace, Value = 0 };
                rotation = normal.Z < 0 ? 0 : normal.X < 0 ? 1 : normal.Z > 0 ? 2 : 3;
            }
            bool valid = terrain.Project.FindSubsystem<SubsystemHomeFurniture>(false)?.MayPlace == true
                && p.Y > 0 && p.Y < (HomeRules.IsTall(BlockIndex) ? 254 : 255)
                && terrain.Terrain.GetCellContents(p.X, p.Y, p.Z) == 0;
            if (HomeRules.IsTall(BlockIndex)) valid &= terrain.Terrain.GetCellContents(p.X, p.Y + 1, p.Z) == 0;
            if (HomeRules.HasFootprint(BlockIndex)) valid &= HomeExtensionGeometry.CanPlace(terrain, p, rotation);
            if (BlockIndex == HomeRules.WallArt) valid &= HomeExtensionGeometry.HasWall(terrain, p, rotation);
            return new BlockPlacementData { CellFace = hit.CellFace, Value = valid ? Terrain.MakeBlockValue(BlockIndex, 0, HomeRules.ItemData(BlockIndex, Terrain.ExtractData(value)) | rotation) : 0 };
        }
        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool showDebris)
        {
            showDebris = true;
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(BlockIndex, 0, HomeRules.ItemData(BlockIndex, Terrain.ExtractData(oldValue))), Count = 1 });
        }
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
        {
            int v = HomeRules.Variant(HomeRules.Normalize(BlockIndex, Terrain.ExtractData(value)));
            if (BlockIndex == HomeRules.Cabinet) return new[] { "橱柜", "转角橱柜", "吊柜", "抽屉柜" }[v];
            if (BlockIndex == HomeRules.Sofa) return new[] { "单人沙发", "沙发左座", "沙发中座", "沙发右座", "转角沙发" }[v];
            if (BlockIndex == HomeRules.WallArt) return new[] { "山月挂画", "山峦挂画", "轨迹挂画" }[v];
            return base.GetDisplayName(terrain, value);
        }
    }
    /// <summary>Two-cell refrigerator with native inventory.</summary>
    public sealed class HomeFridgeBlock : HomeBlock { /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Fridge; }
    /// <summary>Four compatible cabinet modules.</summary>
    public sealed class HomeCabinetBlock : HomeBlock { /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Cabinet; }
    /// <summary>Inset ceramic basin with a switchable visual faucet.</summary>
    public sealed class HomeSinkBlock : HomeBlock { /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Sink; }
    /// <summary>Five upholstered sofa modules.</summary>
    public sealed class HomeSofaBlock : HomeBlock { /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Sofa; }
    /// <summary>Low wooden coffee table.</summary>
    public sealed class HomeCoffeeTableBlock : HomeBlock { /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Table; }
    /// <summary>Two-cell fabric curtain with open and closed models.</summary>
    public sealed class HomeCurtainBlock : HomeBlock { /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Curtain; }
    /// <summary>Hidden upper occupancy cell; never an inventory item or independent drop.</summary>
    public sealed class HomeUpperBlock : Block
    {
        /// <summary>Reserved content index.</summary>
        public const int Index = HomeRules.Upper;
        /// <inheritdoc/>
        public override IEnumerable<int> GetCreativeValues() { yield break; }
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value)
            => HomeRules.LowerId(Terrain.ExtractData(value)) == HomeRules.Curtain ? "窗帘" : "冰箱";
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            int data = Terrain.ExtractData(value), id = HomeRules.LowerId(data);
            return HomeBlock.Boxes(id, 0, HomeRules.Rotation(data), true, HomeRules.IsOpen(data));
        }
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
            => new[] { new BoundingBox(new Vector3(.02f, 0, .02f), new Vector3(.98f, .98f, .98f)) };
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment) { }
        /// <inheritdoc/>
        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool showDebris) { showDebris = false; }
    }
}
