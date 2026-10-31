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
        private readonly BlockMesh mesh=new BlockMesh();private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize()
        {
            foreach(var m in ContentManager.Get<Model>("Models/Excalibur").Meshes)foreach(var part in m.MeshParts)
            {
                int start=mesh.Vertices.Count;
                mesh.AppendModelMeshPart(part,BlockMesh.GetBoneAbsoluteTransform(m.ParentBone),false,false,false,false,Color.White);
                if(m.Name.StartsWith("Inscription"))for(int i=start;i<mesh.Vertices.Count;i++){var v=mesh.Vertices.Array[i];v.IsEmissive=true;mesh.Vertices.Array[i]=v;}
            }
            texture=ContentManager.Get<Texture2D>("Textures/Excalibur");base.Initialize();
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator g,TerrainGeometry t,int v,int x,int y,int z){}
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D r,int v,Color c,float size,ref Matrix m,DrawBlockEnvironmentData e)
            =>BlocksManager.DrawMeshBlock(r,mesh,texture,c,size,ref m,e);
    }
}
