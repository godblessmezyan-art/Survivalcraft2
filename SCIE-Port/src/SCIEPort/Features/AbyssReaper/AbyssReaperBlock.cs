using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Original, non-placeable single-root battle scythe.</summary>
    public sealed class AbyssReaperBlock : Block
    {
        /// <summary>Audited generated allocation, fixed for saved inventories.</summary>
        public const int Index = AbyssId.Value;
        readonly BlockMesh mesh = new BlockMesh(); Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize() {
            foreach(var m in ContentManager.Get<Model>("Models/AbyssReaper").Meshes)
                foreach(var p in m.MeshParts) mesh.AppendModelMeshPart(p,BlockMesh.GetBoneAbsoluteTransform(m.ParentBone),false,false,false,false,Color.White);
            texture=ContentManager.Get<Texture2D>("Textures/AbyssReaper");base.Initialize();
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator g,TerrainGeometry t,int v,int x,int y,int z) { }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D r,int v,Color c,float s,ref Matrix m,DrawBlockEnvironmentData e) { BlocksManager.DrawMeshBlock(r,mesh,texture,c,s,ref m,e); }
    }
}
