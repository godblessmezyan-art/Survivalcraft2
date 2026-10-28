using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Single-root scythe with lit steel and small full-bright energy surfaces.</summary>
    public sealed class AbyssReaperBlock : Block
    {
        /// <summary>Saved contents ID remains unchanged.</summary>
        public const int Index = AbyssId.Value;
        readonly BlockMesh mesh = new BlockMesh(); Texture2D texture;
        int[] energy, polished;
        readonly Color[] faceGlints=new Color[6];
        static readonly Vector3 KeyLight=Vector3.Normalize(new Vector3(-.35f,.8f,-.48f));
        /// <inheritdoc/>
        public override void Initialize() {
            foreach(var m in ContentManager.Get<Model>("Models/AbyssReaper").Meshes)
                foreach(var p in m.MeshParts) mesh.AppendModelMeshPart(p,BlockMesh.GetBoneAbsoluteTransform(m.ParentBone),false,false,false,false,Color.White);
            var emissive=new List<int>();var metal=new List<int>();
            for(int i=0;i<mesh.Vertices.Count;i++) {
                var v=mesh.Vertices.Array[i];int swatch=(int)(v.TextureCoordinates.X*16);
                if(swatch>=12){v.IsEmissive=true;v.Color=Color.White;emissive.Add(i);}
                else if(swatch==8||swatch==9)metal.Add(i);
                mesh.Vertices.Array[i]=v;
            }
            energy=emissive.ToArray();polished=metal.ToArray();
            texture=ContentManager.Get<Texture2D>("Textures/AbyssReaper");base.Initialize();
        }
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator g,TerrainGeometry t,int v,int x,int y,int z) { }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D r,int v,Color c,float s,ref Matrix m,DrawBlockEnvironmentData e) {
            // The native block path has no material roughness/specular shader. A bounded directional
            // facet highlight changes with item orientation; it is not an environment reflection.
            for(int face=0;face<6;face++){
                Vector3 normal=Vector3.TransformNormal(CellFace.FaceToVector3(face),m);
                float glint=normal.LengthSquared()>.00001f?MathUtils.Pow(MathUtils.Saturate(Vector3.Dot(Vector3.Normalize(normal),KeyLight)),12):0;
                byte level=(byte)(155+100*glint);faceGlints[face]=new Color(level,level,level,(byte)255);
            }
            foreach(int i in polished){var vertex=mesh.Vertices.Array[i];vertex.Color=faceGlints[vertex.Face];mesh.Vertices.Array[i]=vertex;}
            float pulse=.90f+.10f*MathUtils.Sin((float)(Time.RealTime%60)*1.8f);
            Color glow=c*pulse;glow.A=c.A;
            foreach(int i in energy){var vertex=mesh.Vertices.Array[i];vertex.Color=glow;mesh.Vertices.Array[i]=vertex;}
            BlocksManager.DrawMeshBlock(r,mesh,texture,c,s,ref m,e);
        }
    }
}
