using Engine;using Engine.Graphics;using Game;
namespace SCIEPort.Features.Codex
{
    /// <summary>Original leather-bound atlas, layered pages, gilt corners, clasps and raised cover sigils.</summary>
    public sealed class ArtifactCodexBlock:Block
    {
        /// <inheritdoc/>
        public const int Index=955;private readonly BlockMesh mesh=new BlockMesh();private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize(){Build(mesh);texture=new Texture2D(1,1,1,ColorFormat.Rgba8888);texture.SetData(0,new byte[]{255,255,255,255});base.Initialize();}
        internal static void Build(BlockMesh m)
        {
            Box(m,new Vector3(0,0,0),new Vector3(.66f,.14f,.92f),new Color(229,215,174));
            Box(m,new Vector3(0,.09f,0),new Vector3(.74f,.045f,1),new Color(31,36,72));
            Box(m,new Vector3(0,-.09f,0),new Vector3(.74f,.045f,1),new Color(25,29,57));
            Box(m,new Vector3(-.355f,0,0),new Vector3(.055f,.20f,1),new Color(39,26,65));
            for(int i=0;i<9;i++)Box(m,new Vector3(.334f,-.055f+i*.014f,0),new Vector3(.005f,.003f,.88f),new Color(162,139,97));
            foreach(float z in new[]{-.40f,-.22f,.22f,.40f})Box(m,new Vector3(-.385f,0,z),new Vector3(.025f,.17f,.035f),new Color(189,141,58));
            foreach(float x in new[]{-.29f,.29f})foreach(float z in new[]{-.42f,.42f})Box(m,new Vector3(x,.12f,z),new Vector3(.13f,.014f,.085f),new Color(216,176,83));
            foreach(float x in new[]{-.31f,.31f})Box(m,new Vector3(x,.117f,0),new Vector3(.014f,.012f,.76f),new Color(180,135,56));
            foreach(float z in new[]{-.37f,.37f})Box(m,new Vector3(0,.117f,z),new Vector3(.62f,.012f,.014f),new Color(180,135,56));
            Box(m,new Vector3(.34f,.05f,0),new Vector3(.10f,.18f,.09f),new Color(195,154,68));
            Box(m,new Vector3(0,.13f,0),new Vector3(.21f,.026f,.25f),new Color(104,72,138));
            for(int i=0;i<8;i++){float a=i*MathUtils.PI/4;Box(m,new Vector3(MathUtils.Cos(a)*.17f,.139f,MathUtils.Sin(a)*.23f),new Vector3(.028f,.015f,.038f),new Color(244,214,132));}
            Box(m,new Vector3(0,.15f,0),new Vector3(.025f,.016f,.17f),new Color(255,227,151));
            Box(m,new Vector3(0,.15f,0),new Vector3(.13f,.016f,.025f),new Color(255,227,151));
            Box(m,new Vector3(.10f,-.11f,.48f),new Vector3(.055f,.01f,.20f),new Color(130,31,53));
        }
        private static void Box(BlockMesh m,Vector3 center,Vector3 size,Color color)
        {
            var h=size*.5f;var v=new[]{new Vector3(-h.X,-h.Y,-h.Z),new Vector3(h.X,-h.Y,-h.Z),new Vector3(h.X,h.Y,-h.Z),new Vector3(-h.X,h.Y,-h.Z),new Vector3(-h.X,-h.Y,h.Z),new Vector3(h.X,-h.Y,h.Z),new Vector3(h.X,h.Y,h.Z),new Vector3(-h.X,h.Y,h.Z)};
            int[] faces={0,3,2,1,5,6,7,4,1,2,6,5,4,7,3,0,3,7,6,2,4,0,1,5};
            for(int f=0;f<6;f++){ushort start=(ushort)m.Vertices.Count;for(int j=0;j<4;j++)m.Vertices.Add(new BlockMeshVertex{Position=center+v[faces[f*4+j]],TextureCoordinates=new Vector2(.5f),Color=Color.MultiplyColorOnly(color,f==4?1:f==5?.50f:.80f),Face=(byte)f});foreach(int k in new[]{0,1,2,0,2,3})m.Indices.Add((ushort)(start+k));}
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator g,TerrainGeometry t,int v,int x,int y,int z){}
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D r,int v,Color c,float size,ref Matrix matrix,DrawBlockEnvironmentData e)=>BlocksManager.DrawMeshBlock(r,mesh,texture,c,size,ref matrix,e);
    }
}
