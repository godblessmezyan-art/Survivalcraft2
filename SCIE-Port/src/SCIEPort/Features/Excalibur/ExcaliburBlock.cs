using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.Excalibur
{
    /// <summary>Original faceted lake sword, double-sided gold rune inlays and sapphire regalia.</summary>
    public sealed class ExcaliburBlock:Block
    {
        /// <summary>Audited new contents identity; no existing item is reassigned.</summary>
        public const int Index=953;
        private readonly BlockMesh mesh=new BlockMesh(),enchanted=new BlockMesh();private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize()
        {
            foreach(var m in ContentManager.Get<Model>("Models/Excalibur").Meshes)foreach(var part in m.MeshParts)
            {
                int start=mesh.Vertices.Count;
                mesh.AppendModelMeshPart(part,BlockMesh.GetBoneAbsoluteTransform(m.ParentBone),false,false,false,false,Color.White);
                if(m.Name.StartsWith("Inscription"))for(int i=start;i<mesh.Vertices.Count;i++){var v=mesh.Vertices.Array[i];v.IsEmissive=true;mesh.Vertices.Array[i]=v;}
                int glowStart=enchanted.Vertices.Count;
                enchanted.AppendModelMeshPart(part,BlockMesh.GetBoneAbsoluteTransform(m.ParentBone),false,false,false,false,Color.White);
                if(m.Name.StartsWith("Inscription")||m.Name=="SilverWhiteDiamondBlade")for(int i=glowStart;i<enchanted.Vertices.Count;i++){var v=enchanted.Vertices.Array[i];v.IsEmissive=true;v.Color=new Color(255,218,112);enchanted.Vertices.Array[i]=v;}
            }
            texture=ContentManager.Get<Texture2D>("Textures/Excalibur");base.Initialize();
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator g,TerrainGeometry t,int v,int x,int y,int z){}
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D r,int v,Color c,float size,ref Matrix m,DrawBlockEnvironmentData e)
        {
            bool glow=e?.SubsystemTerrain?.Project.FindSubsystem<SubsystemExcalibur>(false)?.EnchantedDraw(e)==true;
            BlocksManager.DrawMeshBlock(r,glow?enchanted:mesh,texture,c,size,ref m,e);
        }
    }
}
