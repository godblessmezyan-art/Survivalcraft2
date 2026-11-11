using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>Original three-dimensional drafting scroll, distinct from the reading books.</summary>
    public sealed class BlueprintToolBlock : Block
    {
        /// <summary>New content ID; existing 955/956 books and all old IDs remain unchanged.</summary>
        public const int Index = 957;
        private readonly BlockMesh mesh = new BlockMesh();
        private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize()
        {
            Build(mesh);texture=new Texture2D(1,1,1,ColorFormat.Rgba8888);texture.SetData(0,new byte[]{255,255,255,255});base.Initialize();
        }
        internal static void Build(BlockMesh mesh)
        {
            Box(mesh, Vector3.Zero, new Vector3(.68f,.035f,.86f), new Color(14,74,82));
            foreach (float x in new[] { -.37f,.37f })
            {
                Box(mesh,new Vector3(x,0,0),new Vector3(.12f,.12f,1.04f),new Color(233,221,180));
                foreach(float z in new[]{-.43f,.43f}) Box(mesh,new Vector3(x,0,z),new Vector3(.14f,.14f,.065f),new Color(211,165,72));
                Box(mesh,new Vector3(x,.081f,.43f),new Vector3(.06f,.025f,.045f),new Color(50,230,200));
            }
            for (int i=-3;i<=3;i++)
            {
                Box(mesh,new Vector3(i*.085f,.022f,0),new Vector3(.006f,.006f,.76f),new Color(39,143,150));
                Box(mesh,new Vector3(0,.022f,i*.11f),new Vector3(.60f,.006f,.006f),new Color(39,143,150));
            }
            Box(mesh,new Vector3(0,.065f,0),new Vector3(.28f,.08f,.34f),new Color(67,210,197));
            Box(mesh,new Vector3(0,.13f,0),new Vector3(.36f,.05f,.40f),new Color(126,244,211));
            Box(mesh,new Vector3(0,.18f,0),new Vector3(.22f,.05f,.40f),new Color(126,244,211));
            Box(mesh,new Vector3(0,.23f,0),new Vector3(.08f,.05f,.40f),new Color(126,244,211));
        }
        private static void Box(BlockMesh m,Vector3 center,Vector3 size,Color color)
        {
            var h=size*.5f;
            var vertices=new[]{new Vector3(-h.X,-h.Y,-h.Z),new Vector3(h.X,-h.Y,-h.Z),new Vector3(h.X,h.Y,-h.Z),new Vector3(-h.X,h.Y,-h.Z),new Vector3(-h.X,-h.Y,h.Z),new Vector3(h.X,-h.Y,h.Z),new Vector3(h.X,h.Y,h.Z),new Vector3(-h.X,h.Y,h.Z)};
            int[] faces={0,3,2,1,5,6,7,4,1,2,6,5,4,7,3,0,3,7,6,2,4,0,1,5};
            for(int f=0;f<6;f++)
            {
                ushort start=(ushort)m.Vertices.Count;
                for(int j=0;j<4;j++)m.Vertices.Add(new BlockMeshVertex{Position=center+vertices[faces[f*4+j]],TextureCoordinates=new Vector2(.5f),Color=Color.MultiplyColorOnly(color,f==4?1:f==5?.5f:.8f),Face=(byte)f});
                foreach(int k in new[]{0,1,2,0,2,3})m.Indices.Add((ushort)(start+k));
            }
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator,TerrainGeometry geometry,int value,int x,int y,int z) { }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer,int value,Color color,float size,ref Matrix matrix,DrawBlockEnvironmentData environment)
        { BlocksManager.DrawMeshBlock(renderer,mesh,texture,color,size,ref matrix,environment); }
    }
}
