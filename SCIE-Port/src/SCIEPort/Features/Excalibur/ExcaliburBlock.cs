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
            if(glow)QueueGlow(r,m,size,(float)(Time.RealTime%60),e?.ViewProjectionMatrix);
        }
        // Same local blade coordinates, size, matrix and optional projection as DrawMeshBlock.
        // This works for FPP, third person, either hand, and all camera yaw/pitch angles.
        internal static void QueueGlow(PrimitivesRenderer3D renderer,Matrix matrix,float size,float time,Matrix? projection=null)
        {
            if(projection.HasValue)matrix*=projection.Value;matrix=Matrix.CreateScale(size)*matrix;
            var batch=renderer.FlatBatch(2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Additive);
            for(int strand=0;strand<4;strand++)for(int i=0;i<12;i++)
            {
                float y=.2f+i*1.4f/12,z=.2f+(i+1)*1.4f/12;
                float a=y*12-time*3+strand*MathUtils.PI*.5f,b=z*12-time*3+strand*MathUtils.PI*.5f;
                Vector3 p=new Vector3(MathUtils.Cos(a)*.065f,y,MathUtils.Sin(a)*.055f),q=new Vector3(MathUtils.Cos(b)*.065f,z,MathUtils.Sin(b)*.055f);
                var width=new Vector3(.012f,0,.006f);float fade=MathUtils.Sin((y-.2f)/1.4f*MathUtils.PI);
                batch.QueueQuad(GlowPoint(p-width,matrix),GlowPoint(q-width,matrix),GlowPoint(q+width,matrix),GlowPoint(p+width,matrix),new Color(255,207,74,150)*(.45f+.55f*fade));
            }
        }
        private static Vector3 GlowPoint(Vector3 p,Matrix m)
        {var v=Vector4.Transform(new Vector4(p,1),m);return new Vector3(v.X,v.Y,v.Z)/v.W;}
    }
}
