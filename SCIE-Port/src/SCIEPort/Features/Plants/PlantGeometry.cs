using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Apple;

namespace SCIEPort.Features.Plants
{
    // Original, deterministic low-poly meshes. No foreign model, atlas, or runtime dependency.
    internal static class PlantGeometry
    {
        private static readonly Color Stem = new Color(68, 112, 45);
        private static readonly Color LeafGreen = new Color(79, 135, 49);
        private static readonly Color Bark = new Color(111, 77, 47);

        internal static BlockMesh Build(int id, int data)
        {
            var m = new BlockMesh();
            int stage = data & 7;
            if (id == 939) Apple(m, data);
            else if (id == 929 || id == 934 || id == 936 || id == 911) Crop(m, id, stage);
            else if (id == 932) Bamboo(m);
            else if (id == 905) Cattail(m, data);
            else if (id == 903 || id == 915) BerryBush(m, id, data);
            else Item(m, id, id == 909 || id == 913 ? (data >> 6) & 3 : data);
            return m;
        }

        internal static int VisualData(int id, int data)
        {
            if (id == 939)
            {
                int part=AppleTreeRules.Part(data);
                if(part==AppleTreeRules.Leaf)
                {
                    int stage=AppleRules.GetFruiting(data)?15:AppleRules.GetSize(data)>=3?3:AppleRules.GetSize(data);
                    return part|stage|(data&8128);
                }
                return (data&63)|(part==AppleTreeRules.Trunk?data&7168:0);
            }
            if (id == 903) return data & 15;
            if (id == 905 || id == 907 || id == 915) return data & 3;
            if (id == 909 || id == 913) return data & (3 << 6);
            if (id == 911 || id == 929 || id == 934 || id == 936) return data & 7;
            return 0;
        }

        private static void Crop(BlockMesh m, int id, int stage)
        {
            float growth = stage / 7f;
            float height = .17f + (id==936?.64f:.75f) * growth;
            int stalks = id == 934 ? 4 : id == 911 ? 3 : 1;
            for (int s = 0; s < stalks; s++)
            {
                float angle = s * 2.39996f;
                Vector3 root = new Vector3(.5f + (stalks > 1 ? .11f * (float)Math.Cos(angle) : 0), 0,
                    .5f + (stalks > 1 ? .11f * (float)Math.Sin(angle) : 0));
                Vector3 tip = root + new Vector3(.028f * (float)Math.Cos(angle), height * (1f - s * .06f), .028f * (float)Math.Sin(angle));
                Color stem = stage == 7 && id == 934 ? new Color(151, 142, 68) : Stem;
                Tube(m, root, tip, id == 929 ? .022f : .012f, .008f, stem);
                int leaves = 2 + stage / 2;
                for (int i = 0; i < leaves; i++)
                {
                    float a = angle + i * 2.39996f;
                    Vector3 start = Vector3.Lerp(root, tip, .22f + .105f * i);
                    Vector3 direction = new Vector3((float)Math.Cos(a), .30f, (float)Math.Sin(a));
                    float length = (.10f + .23f * growth) * (1f - i * .055f);
                    Leaf(m, start, start + direction * length, id == 934 ? .028f : id == 929 ? .075f : .095f, LeafGreen);
                    if (id == 911 && stage >= 5 && i > 0)
                    {
                        Vector3 pod = start + direction * .08f;
                        Tube(m, pod, pod + new Vector3(.025f, -.13f, .015f), .025f, .014f,
                            stage == 7 ? new Color(172, 150, 67) : new Color(118, 153, 54));
                        for (int b = 0; b < 3; b++) Ellipsoid(m, pod + new Vector3(.012f, -.025f - b * .037f, .008f), new Vector3(.024f,.023f,.024f), new Color(145, 161, 67), 6);
                    }
                }
                if (id == 929 && stage >= 5)
                {
                    Vector3 ear = root + new Vector3(.105f, height * .55f, .025f);
                    Tube(m, Vector3.Lerp(root, tip, .45f), ear, .018f, .014f, Stem);
                    Tube(m, ear, ear + new Vector3(.035f,.23f,0), .042f,.025f, new Color(150,172,61));
                    if (stage >= 6)
                        for (int row = 0; row < 6; row++) for (int k = 0; k < 5; k++)
                        {
                            float a = k * (2f * MathUtils.PI) / 5;
                            Ellipsoid(m, ear + new Vector3(.035f * (float)Math.Cos(a) + row * .005f, .035f + row * .029f, .035f * (float)Math.Sin(a)),
                                new Vector3(.017f,.019f,.018f), new Color(235,189 + row * 3,54), 5);
                        }
                    for (int i = -1; i <= 1; i++) Tube(m, tip, tip + new Vector3(i * .08f,.045f,-.03f), .007f,.002f,new Color(185,161,78));
                }
                if (id == 936 && stage >= 4)
                {
                    Vector3 center = tip + new Vector3(0,-.08f,-.025f);
                    float r = stage == 4 ? .075f : .125f;
                    Ellipsoid(m, center, new Vector3(r, r, .045f), new Color(85,61,29));
                    if (stage >= 5)
                    {
                        for (int p = 0; p < 12; p++)
                        {
                            float a = p * (2f * MathUtils.PI) / 12;
                            Vector3 v = new Vector3((float)Math.Cos(a), (float)Math.Sin(a), -.08f);
                            Leaf(m, center + v * .10f, center + v * .23f, .052f, new Color(242,191 + p % 3 * 10,38), Vector3.UnitZ);
                        }
                        for (int p = 0; p < 13; p++)
                        {
                            float a = p * 2.39996f, r2 = .023f * (float)Math.Sqrt(p);
                            Ellipsoid(m, center + new Vector3(r2 * (float)Math.Cos(a), r2 * (float)Math.Sin(a), -.045f), new Vector3(.012f,.012f,.009f), new Color(184,136,56),5);
                        }
                    }
                }
                if (id == 934 && stage >= 5)
                {
                    if (stage <= 6) Flower(m, tip, new Color(118,153,226), .048f);
                    else Ellipsoid(m, tip - new Vector3(0,.025f,0), new Vector3(.032f,.037f,.032f),new Color(179,146,79),6);
                }
            }
        }

        private static void Bamboo(BlockMesh m)
        {
            Tube(m,new Vector3(.5f,0,.5f),new Vector3(.5f,1,.5f),.061f,.054f,new Color(113,146,60),8);
            foreach (float y in new[] {.08f,.42f,.70f})
            {
                Tube(m,new Vector3(.5f,y,.5f),new Vector3(.5f,y+.035f,.5f),.068f,.068f,new Color(165,175,87),8);
                Vector3 start = new Vector3(.5f,y+.03f,.5f), end = start + new Vector3(y < .5f ? -.21f : .21f,.15f,.09f);
                Tube(m,start,end,.015f,.005f,Stem);
                for(int i=0;i<3;i++) Leaf(m,Vector3.Lerp(start,end,.4f+i*.2f),end+new Vector3((i-1)*.08f,.09f,-.10f),.024f,LeafGreen);
            }
        }

        private static void Cattail(BlockMesh m, int stage)
        {
            for(int i=0;i<3;i++)
            {
                Vector3 root = new Vector3(.40f+i*.10f,0,.46f+(i%2)*.08f);
                float growth = stage == 0 ? 1f : .25f * stage;
                float h = (.70f + i * .09f) * growth;
                Tube(m,root,root+new Vector3(0,h,0),.012f,.009f,Stem);
                if (stage == 0 || stage == 3) Tube(m,root+new Vector3(0,h-.24f,0),root+new Vector3(0,h-.03f,0),.043f,.036f,new Color(108+i*9,69,38),8);
                Leaf(m,root+new Vector3(0,.03f,0),root+new Vector3(-.22f,.68f * growth,.11f),.033f,LeafGreen);
                Leaf(m,root+new Vector3(0,.06f,0),root+new Vector3(.20f,.57f * growth,-.16f),.029f,Stem);
            }
        }

        private static void BerryBush(BlockMesh m, int id, int data)
        {
            bool cloud=id==915, ripe=cloud ? (data&3)==0 : (data&1)!=0;
            bool green=cloud?(data&3)==3:!ripe&&((data>>1)&7)>=2;
            for(int b=0;b<5;b++)
            {
                float a=b*2.39996f;
                Vector3 root=new Vector3(.5f,0,.5f), tip=new Vector3(.5f+.23f*(float)Math.Cos(a),cloud?.36f:.62f,.5f+.23f*(float)Math.Sin(a));
                Tube(m,root,tip,.017f,.008f,cloud?Stem:Bark);
                for(int j=0;j<3;j++)
                {
                    Vector3 p=Vector3.Lerp(root,tip,.45f+j*.22f);
                    Vector3 d=new Vector3((float)Math.Cos(a+j*2),.14f,(float)Math.Sin(a+j*2));
                    Leaf(m,p,p+d*.18f,.085f,LeafGreen);
                }
                if(ripe||green) for(int k=0;k<(cloud?5:3);k++)
                    Ellipsoid(m,tip+new Vector3(.025f*(float)Math.Cos(k*2.4f),-.018f*k,.025f*(float)Math.Sin(k*2.4f)),
                        new Vector3(.035f,.037f,.035f),green?new Color(133,159,63):cloud?new Color(232,151+b*5,41):new Color(146+b*10,38,65),6);
                else if(cloud&&(data&3)==2||!cloud&&((data>>1)&7)==1) Flower(m,tip,new Color(237,231,209),.055f);
            }
        }

        private static void Apple(BlockMesh m, int data)
        {
            int part=AppleTreeRules.Part(data);
            if(part==AppleTreeRules.Leaf)
            {
                int ox=AppleTreeRules.OffsetX(data),oz=AppleTreeRules.OffsetZ(data),oy=AppleTreeRules.OffsetY(data);
                Ellipsoid(m,new Vector3(.5f,.50f,.5f),new Vector3(.48f,.48f,.48f),new Color(68,116,42));
                for(int i=0;i<4;i++)
                {
                    float a=i*2.39996f+(ox+oz)*.37f;
                    Vector3 p=new Vector3(.5f+.19f*(float)Math.Cos(a),.30f+(i%2)*.36f,.5f+.19f*(float)Math.Sin(a));
                    Ellipsoid(m,p,new Vector3(.29f,.29f,.29f),new Color(72+i*6,120+i*6,45+i*3));
                    for(int j=0;j<4;j++)
                    {
                        float angle=a+j*1.57f;
                        Vector3 start=new Vector3(.5f+.29f*(float)Math.Cos(angle),.48f+(j%2)*.25f,.5f+.29f*(float)Math.Sin(angle));
                        Leaf(m,start,start+new Vector3(.16f*(float)Math.Cos(angle),.08f,.16f*(float)Math.Sin(angle)),.085f,new Color(104+i*3,152+i*3,57));
                    }
                }
                Vector3 branchStart=new Vector3(.5f-ox*.45f,.32f,.5f-oz*.45f);
                Tube(m,branchStart,new Vector3(.5f,.58f,.5f),.038f,.013f,Bark);
                // Continuous woody structure through neighboring reserved cells prevents floating crowns.
                Tube(m,new Vector3(.5f,0,.5f),new Vector3(.5f,1,.5f),oy==4?.020f:.035f,.012f,Bark);
                if(oy==3)
                {
                    Tube(m,new Vector3(0,.50f,.5f),new Vector3(1,.50f,.5f),.022f,.022f,Bark);
                    Tube(m,new Vector3(.5f,.50f,0),new Vector3(.5f,.50f,1),.022f,.022f,Bark);
                }
                if(AppleRules.GetFruiting(data)||AppleRules.GetSize(data)>=3)
                    for (int f = 0; f < 2; f++)
                    {
                        float spacing = f == 0 ? -.19f : .19f;
                        Vector3 fruit = new Vector3(.5f + ox * .417f + (ox == 0 ? spacing : 0f), .14f + f * .09f,
                            .5f + oz * .417f + (oz == 0 && ox != 0 ? spacing : 0f));
                        bool ripe = AppleRules.GetFruiting(data);
                        float radius = ripe ? .065f : .046f;
                        Tube(m, fruit + Vector3.UnitY * radius, fruit + new Vector3(-ox * .16f, .23f, -oz * .16f),
                            .009f, .013f, Bark, 5);
                        AppleFruit(m, fruit, radius, ripe);
                    }
                else if(AppleRules.GetSize(data)==1)
                    for(int f=0;f<4;f++) Flower(m,new Vector3(.23f+f%2*.54f,.39f+f/2*.30f,f<2?.94f:.06f),new Color(249,218,222),.075f,Vector3.UnitZ);
                return;
            }
            if(part==AppleTreeRules.Root||part==AppleTreeRules.Trunk)
            {
                Tube(m,new Vector3(.5f,0,.5f),new Vector3(.5f,1,.5f),part==AppleTreeRules.Root?.15f:.12f,.105f,Bark,8);
                for(int i=0;i<5;i++)
                {
                    float a=i*(2f * MathUtils.PI)/5;
                    Vector3 p=new Vector3(.5f+.111f*(float)Math.Cos(a),.09f,.5f+.111f*(float)Math.Sin(a));
                    Tube(m,p,p+new Vector3(0,.82f,0),.009f,.005f,new Color(82,57,38),4);
                    if(part==AppleTreeRules.Root) Tube(m,new Vector3(.5f,.19f,.5f),new Vector3(.5f+.28f*(float)Math.Cos(a),.015f,.5f+.28f*(float)Math.Sin(a)),.06f,.015f,Bark);
                }
                if(part==AppleTreeRules.Trunk&&AppleTreeRules.OffsetY(data)==2)
                    for(int i=0;i<4;i++)
                    {
                        float a=i*MathUtils.PI/2;
                        Tube(m,new Vector3(.5f,.30f,.5f),new Vector3(.5f+.48f*(float)Math.Cos(a),.82f,.5f+.48f*(float)Math.Sin(a)),.065f,.022f,Bark);
                    }
                return;
            }
            float h=.20f+(data&7)*.105f;
            Tube(m,new Vector3(.5f,0,.5f),new Vector3(.5f,h,.5f),.018f+(data&7)*.004f,.009f,Bark);
            for(int i=0;i<3+(data&7);i++)
            {
                float a=i*2.39996f;
                Vector3 p=new Vector3(.5f,h*(.32f+i*.055f),.5f),end=p+new Vector3(.22f*(float)Math.Cos(a),.10f,.22f*(float)Math.Sin(a));
                Tube(m,p,end,.010f,.003f,Bark);
                Leaf(m,end,end+new Vector3(.08f*(float)Math.Cos(a),.10f,.08f*(float)Math.Sin(a)),.08f,LeafGreen);
            }
        }

        private static void Item(BlockMesh m, int id, int stage)
        {
            if (id == 940) { AppleFruit(m, new Vector3(.5f, .205f, .5f), .218f, true); return; }
            if (id == 931)
            {
                Vector3 tip = new Vector3(.5f, .62f, .5f);
                Tube(m, new Vector3(.5f, .08f, .5f), tip, .088f, .05f, new Color(217, 161, 49), 8);
                for (int row = 0; row < 7; row++)
                for (int kernel = 0; kernel < 6; kernel++)
                {
                    float a = kernel * MathUtils.PI / 3f;
                    float radius = .08f - row * .004f;
                    Ellipsoid(m, new Vector3(.5f + radius * (float)Math.Cos(a), .13f + row * .066f,
                        .5f + radius * (float)Math.Sin(a)), new Vector3(.034f, .038f, .034f),
                        new Color(235, 184 + row * 4, 45), 5);
                }
                Leaf(m, new Vector3(.5f, .08f, .5f), new Vector3(.36f, .42f, .47f), .058f, LeafGreen);
                Leaf(m, new Vector3(.5f, .08f, .5f), new Vector3(.62f, .37f, .54f), .058f, LeafGreen);
                return;
            }
            if (id == 935)
            {
                for (int i = 0; i < 5; i++)
                    Tube(m, new Vector3(.43f + .035f * i, .03f, .51f),
                        new Vector3(.40f + .05f * i, .50f - .025f * i, .48f), .013f, .008f,
                        new Color(172 + i * 6, 147 + i * 5, 96), 5);
                Tube(m, new Vector3(.5f, .22f, .50f), new Vector3(.5f, .27f, .50f), .086f, .086f, Bark, 7);
                return;
            }
            if (id == 912 || id == 930 || id == 937)
            {
                Color seed = id == 912 ? new Color(153, 172, 76) :
                    id == 930 ? new Color(221, 178, 55) : new Color(88, 75, 61);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * 2.39996f;
                    Vector3 center = new Vector3(.5f + .06f * (float)Math.Cos(angle), .09f + i * .019f,
                        .5f + .06f * (float)Math.Sin(angle));
                    Ellipsoid(m, center, id == 937 ? new Vector3(.035f, .080f, .025f) :
                        new Vector3(.053f, .045f, .046f), seed, 8);
                }
                return;
            }
            if(id==906)
            {
                Tube(m,new Vector3(.5f,.015f,.5f),new Vector3(.5f,.42f,.5f),.045f,.027f,new Color(211,217,155),8);
                for(int i=0;i<4;i++) Leaf(m,new Vector3(.5f,.24f,.5f),new Vector3(.5f+(i%2==0?-.13f:.13f),.59f,.5f+(i<2?-.08f:.08f)),.026f,LeafGreen);
                return;
            }
            if(id==904||id==916)
            {
                for(int i=0;i<(id==916?9:4);i++)
                {
                    float a=i*2.39996f,r=id==916?.075f:.065f;
                    Ellipsoid(m,new Vector3(.5f+r*(float)Math.Cos(a),.11f+i%3*.045f,.5f+r*(float)Math.Sin(a)),new Vector3(.052f,.052f,.052f),id==916?new Color(235,151+i*5,37):new Color(131+i*12,36,69),6);
                }
                Leaf(m,new Vector3(.5f,.25f,.5f),new Vector3(.66f,.32f,.53f),.06f,LeafGreen);
                m.TransformPositions(Matrix.CreateTranslation(-.5f, 0f, -.5f) * Matrix.CreateScale(.8f) * Matrix.CreateTranslation(.5f, 0f, .5f));
                return;
            }
            if(id==907)
            {
                if (stage != 0)
                {
                    float radius = .025f + stage * .021f;
                    Ellipsoid(m, new Vector3(.5f, radius, .5f), new Vector3(radius, radius, radius),
                        new Color(126, 157, 62), 8);
                    if (stage == 2) Flower(m, new Vector3(.5f, .14f, .5f), new Color(236, 170, 155), .06f);
                    return;
                }
                Ellipsoid(m,new Vector3(.5f,.19f,.5f),new Vector3(.13f,.18f,.13f),new Color(184,66,85));
                for(int i=0;i<8;i++)
                {
                    float a=i*2.39996f;
                    Tube(m,new Vector3(.5f+.12f*(float)Math.Cos(a),.10f+i*.022f,.5f+.12f*(float)Math.Sin(a)),new Vector3(.5f+.14f*(float)Math.Cos(a),.12f+i*.022f,.5f+.14f*(float)Math.Sin(a)),.005f,.002f,new Color(230,201,140),4);
                }
                Flower(m,new Vector3(.5f,.36f,.5f),new Color(127,90,82),.055f);return;
            }
            if(id==909||id==910||id==913||id==914)
            {
                bool red=id==913||id==914;
                int count = id == 909 || id == 913 ? (stage == 0 ? 4 : stage) : 1;
                float growth = stage == 0 ? 1f : .30f + .18f * stage;
                for(int i=0;i<count;i++)
                {
                    Vector3 p=new Vector3(.38f+i%2*.25f,.02f,.38f+i/2*.23f);
                    float h = (.16f + i % 3 * .05f) * growth;
                    Tube(m,p,p+new Vector3(0,h,0),.028f,.019f,new Color(219,204,166));
                    Ellipsoid(m,p+new Vector3(0,h,0),new Vector3(.12f,.065f,.12f) * growth,red?new Color(189,48,36):new Color(153,110,66));
                    if(red && stage == 0) for(int j=0;j<5;j++)
                    {
                        float a=j*2.4f;
                        Ellipsoid(m,p+new Vector3(.067f*(float)Math.Cos(a),h+.05f,.067f*(float)Math.Sin(a)),new Vector3(.017f,.008f,.016f),new Color(242,225,188),5);
                    }
                }
                return;
            }
            Color c=id==916?new Color(231,157,48):id==904?new Color(154,42,67):id==908?new Color(155,103,49):new Color(131,157,70);
            Ellipsoid(m,new Vector3(.5f,.15f,.5f),new Vector3(.12f,.14f,.12f),c);
            if(id==908) Ellipsoid(m,new Vector3(.5f,.24f,.5f),new Vector3(.13f,.065f,.13f),Bark);
            Tube(m,new Vector3(.5f,.25f,.5f),new Vector3(.51f,.34f,.5f),.012f,.006f,Bark);
            Leaf(m,new Vector3(.5f,.29f,.5f),new Vector3(.64f,.34f,.52f),.047f,LeafGreen);
        }

        private static void AppleFruit(BlockMesh m,Vector3 p,float r,bool ripe)
        {
            Ellipsoid(m,p,new Vector3(r,r*.91f,r),ripe?new Color(194,48,37):new Color(135,170,58),10);
            Ellipsoid(m,p+new Vector3(-r*.22f,r*.23f,-r*.25f),new Vector3(r*.60f,r*.63f,r*.59f),ripe?new Color(216,69,40):new Color(159,185,68),8);
            Tube(m,p+new Vector3(0,r*.73f,0),p+new Vector3(.012f,r*1.35f,0),r*.10f,r*.07f,Bark,5);
            Leaf(m,p+new Vector3(0,r*1.15f,0),p+new Vector3(r*.9f,r*1.35f,.025f),r*.25f,LeafGreen);
        }

        private static void Flower(BlockMesh m,Vector3 p,Color c,float r,Vector3? facing=null)
        {
            Vector3 normal=facing??Vector3.UnitY,vertical=facing.HasValue?Vector3.UnitY:Vector3.UnitZ;
            for(int i=0;i<5;i++)
            {
                float a=i*(2f * MathUtils.PI)/5;
                Vector3 d=Vector3.UnitX*(float)Math.Cos(a)+vertical*(float)Math.Sin(a)+normal*.14f;
                Leaf(m,p,p+d*r,r*.38f,c,normal);
            }
            Ellipsoid(m,p+normal*.009f,new Vector3(r*.24f,r*.14f,r*.24f),new Color(224,182,67),5);
        }

        // Closed folded leaves retain thickness, ridges and a useful silhouette from every view.
        private static void Leaf(BlockMesh m,Vector3 a,Vector3 b,float width,Color c,Vector3? normal=null)
        {
            Vector3 axis=Vector3.Normalize(b-a),n=normal??Vector3.UnitY;
            Vector3 side=Vector3.Cross(axis,n);
            if(side.LengthSquared()<.001f) side=Vector3.Cross(axis,Vector3.UnitZ);
            side=Vector3.Normalize(side)*width;
            Vector3 mid=Vector3.Lerp(a,b,.47f),ridge=mid+n*width*.24f,under=mid-n*.006f;
            Vector3 l=mid+side,r=mid-side;
            Tri(m,a,l,ridge,c,n);Tri(m,l,b,ridge,c,n);Tri(m,b,r,ridge,c,n);Tri(m,r,a,ridge,c,n);
            Color dark=Color.MultiplyColorOnly(c,.76f);
            Tri(m,l,a,under,dark,-n);Tri(m,b,l,under,dark,-n);Tri(m,r,b,under,dark,-n);Tri(m,a,r,under,dark,-n);
        }

        private static void Tube(BlockMesh m,Vector3 a,Vector3 b,float ra,float rb,Color c,int sides=6)
        {
            Vector3 axis=Vector3.Normalize(b-a),u=Vector3.Cross(axis,Math.Abs(axis.Y)>.9f?Vector3.UnitX:Vector3.UnitY);
            u=Vector3.Normalize(u);Vector3 v=Vector3.Cross(axis,u);
            for(int i=0;i<sides;i++)
            {
                float q=i*(2f * MathUtils.PI)/sides,q2=(i+1)*(2f * MathUtils.PI)/sides;
                Vector3 d=u*(float)Math.Cos(q)+v*(float)Math.Sin(q),e=u*(float)Math.Cos(q2)+v*(float)Math.Sin(q2);
                Vector3 p=a+d*ra,r=a+e*ra,s=b+e*rb,t=b+d*rb;
                p.Y=Math.Max(0,p.Y);r.Y=Math.Max(0,r.Y);
                Tri(m,p,r,s,c,d+e);Tri(m,p,s,t,c,d+e);
                Tri(m,a,r,p,c,-axis);Tri(m,b,t,s,c,axis);
            }
        }

        private static void Ellipsoid(BlockMesh m,Vector3 center,Vector3 radius,Color c,int sides=8)
        {
            // Three rings, single pole vertices. No zero-area pole quads.
            for(int ring=0;ring<4;ring++) for(int i=0;i<sides;i++)
            {
                float a=-MathUtils.PI/2+ring*MathUtils.PI/4,b=a+MathUtils.PI/4;
                float u=i*(2f * MathUtils.PI)/sides,v=(i+1)*(2f * MathUtils.PI)/sides;
                Vector3 p=Sphere(center,radius,a,u),q=Sphere(center,radius,a,v),r=Sphere(center,radius,b,v),s=Sphere(center,radius,b,u);
                if(ring>0) Tri(m,p,q,r,c,(p+q+r)/3-center);
                if(ring<3) Tri(m,p,r,s,c,(p+r+s)/3-center);
            }
        }
        private static Vector3 Sphere(Vector3 p,Vector3 r,float a,float b)
            =>p+new Vector3(r.X*(float)(Math.Cos(a)*Math.Cos(b)),r.Y*(float)Math.Sin(a),r.Z*(float)(Math.Cos(a)*Math.Sin(b)));

        private static void Tri(BlockMesh m,Vector3 a,Vector3 b,Vector3 c,Color color,Vector3 outward)
        {
            Vector3 normal=Vector3.Cross(b-a,c-a);
            if(Vector3.Dot(normal,outward)>0){Vector3 swap=b;b=c;c=swap;normal=-normal;}
            Vector3 n=-Vector3.Normalize(normal);
            byte face=Math.Abs(n.Y)>=Math.Abs(n.X)&&Math.Abs(n.Y)>=Math.Abs(n.Z)?(byte)(n.Y>=0?4:5)
                :Math.Abs(n.X)>Math.Abs(n.Z)?(byte)(n.X>=0?1:3):(byte)(n.Z>=0?0:2);
            float light=.72f+.20f*Math.Max(n.Y,0)+.08f*Math.Max(-n.Z,0);
            Color lit=Color.MultiplyColorOnly(color,light);
            ushort start=(ushort)m.Vertices.Count;
            foreach(Vector3 p in new[]{a,b,c}) m.Vertices.Add(new BlockMeshVertex{Position=p,TextureCoordinates=new Vector2(.5f),Color=lit,Face=face});
            m.Indices.Add(start);m.Indices.Add((ushort)(start+1));m.Indices.Add((ushort)(start+2));
        }
    }

    internal sealed class PlantModel
    {
        private readonly Dictionary<int,BlockMesh> meshes=new Dictionary<int,BlockMesh>();
        private readonly Dictionary<int,BoundingBox[]> boxes=new Dictionary<int,BoundingBox[]>();
        private readonly object sync=new object();
        private Texture2D texture;
        internal void Initialize(int id)
        {
            // Mirror the native/home block lifecycle: GPU resources are created during block loading,
            // never by a chunk worker. Meshes and collision bounds are immutable after publication.
            if(texture==null){texture=new Texture2D(1,1,1,ColorFormat.Rgba8888);texture.SetData(0,new byte[]{255,255,255,255});}
            for(int data=0;data<16;data++) Mesh(id,data);
            if(id==939)
            {
                Mesh(id,AppleTreeRules.Root|7);
                foreach(int[] part in AppleTreeRules.Parts())
                    for(int state=0;state<16;state++) Mesh(id,AppleTreeRules.Encode(part[3],part[0],part[1],part[2])|state);
            }
        }
        private Texture2D Texture => texture??throw new InvalidOperationException("Plant model is not initialized");
        private BlockMesh Mesh(int id,int data)
        {
            lock(sync)
            {
                int key=PlantGeometry.VisualData(id,data);
                BlockMesh mesh;
                if(!meshes.TryGetValue(key,out mesh)){mesh=PlantGeometry.Build(id,key);meshes.Add(key,mesh);}
                return mesh;
            }
        }
        internal BoundingBox[] Boxes(Block block,int value)
        {
            lock(sync) return MakeBoxes(block,value);
        }
        private BoundingBox[] MakeBoxes(Block block,int value)
        {
            int data=PlantGeometry.VisualData(block.BlockIndex,Terrain.ExtractData(value));
            BoundingBox[] bounds;
            if(!boxes.TryGetValue(data,out bounds))
            {
                BlockMesh mesh=Mesh(block.BlockIndex,data);
                Vector3 min=new Vector3(1f),max=Vector3.Zero;
                for(int i=0;i<mesh.Vertices.Count;i++)
                {
                    Vector3 p=mesh.Vertices.Array[i].Position;
                    min=new Vector3(Math.Min(min.X,p.X),Math.Min(min.Y,p.Y),Math.Min(min.Z,p.Z));
                    max=new Vector3(Math.Max(max.X,p.X),Math.Max(max.Y,p.Y),Math.Max(max.Z,p.Z));
                }
                bounds=new[]{new BoundingBox(min,max)};boxes.Add(data,bounds);
            }
            return bounds;
        }
        internal void Generate(Block block, BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            // Loose legacy apple cells use the same size as the native dropped-food renderer.
            Matrix? transform = block.BlockIndex == AppleBlock.Index
                ? Matrix.CreateTranslation(-.5f, 0f, -.5f) * Matrix.CreateScale(.3f) * Matrix.CreateTranslation(.5f, 0f, .5f)
                : (Matrix?)null;
            generator.GenerateShadedMeshVertices(block, x, y, z, Mesh(block.BlockIndex, Terrain.ExtractData(value)),
                Color.White, transform, null, geometry.GetGeometry(Texture).SubsetOpaque);
        }
        internal void Draw(Block block,PrimitivesRenderer3D renderer,int value,Color color,float size,ref Matrix matrix,DrawBlockEnvironmentData environment)
        {
            BlockMesh mesh=Mesh(block.BlockIndex,Terrain.ExtractData(value));
            BoundingBox bounds = Boxes(block, value)[0];
            Vector3 center = (bounds.Min + bounds.Max) * .5f;
            Matrix centered = Matrix.CreateTranslation(-center) * Matrix.CreateScale(size) * matrix;
            BlocksManager.DrawMeshBlock(renderer,mesh,Texture,color,1f,ref centered,environment);
        }
    }
}
