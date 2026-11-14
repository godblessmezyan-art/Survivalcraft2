using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.Architecture
{
    /// <summary>Original vertex-coloured geometry, bounded to the owning cell in every orientation.</summary>
    internal static class ArchitectureGeometry
    {
        internal sealed class Shape
        {
            internal readonly BlockMesh Opaque = new BlockMesh(), Glass = new BlockMesh();
            internal BoundingBox[] Collision, Interaction;
            internal BoundingBox Bounds;
        }
        private static readonly Color Wood = new Color(97, 54, 30), Trim = new Color(151, 91, 43), Dark = new Color(57, 34, 27);
        private static readonly Color Tile = new Color(76, 43, 34), Copper = new Color(173, 114, 52), Stone = new Color(132, 137, 132);
        private static readonly Color Leaf = new Color(62, 112, 54), Pink = new Color(236, 157, 185), Cloth = new Color(227, 215, 184);

        internal static Shape Build(int id, int data)
        {
            if (!ArchitectureRules.Valid(id, data)) throw new ArgumentException("Invalid architectural part DATA");
            var b = new Builder(data);
            int v = ArchitectureRules.Variant(data);
            if (id == ArchitectureRules.Roof) Roof(b, v);
            else if (id == ArchitectureRules.Timber) Timber(b, v);
            else if (id == ArchitectureRules.Window) Window(b, v);
            else if (id == ArchitectureRules.Lantern) Lantern(b, v);
            else if (id == ArchitectureRules.Tea) Tea(b, v);
            else if (id == ArchitectureRules.Garden) Garden(b, v);
            else Telescope(b);
            return b.Finish();
        }
        private static void Roof(Builder b, int v)
        {
            if (v == 0)
            {
                // A 1:1 rise, with thin, overlapping tile courses rather than a solid stair.
                for (int i = 0; i < 8; i++)
                {
                    float z0 = i / 8f, z1 = (i + 1) / 8f;
                    b.Slope(0, 1, z0, z1, .035f + .88f * z0, .035f + .88f * z1, .055f, Tile);
                    for (int j = 0; j < 4; j++)
                        b.Slope(j * .25f + .012f, j * .25f + .032f, z0, z1, .092f + .88f * z0, .092f + .88f * z1, .016f, Dark, false);
                }
            }
            else if (v == 1)
            {
                b.Box(.34f, .02f, 0, .66f, .12f, 1, Wood);
                b.Cylinder(new Vector3(.5f, .21f, .02f), new Vector3(.5f, .21f, .98f), .16f, .16f, Tile, true);
                for (int i = 1; i < 5; i++) b.Box(.33f, .13f, i * .2f - .012f, .67f, .17f, i * .2f + .012f, Trim, false);
            }
            else if (v == 2)
            {
                b.Box(0, .04f, 0, 1, .13f, 1, Tile);
                b.Box(0, .13f, .03f, 1, .17f, .11f, Trim);
                for (int i = 0; i < 4; i++) b.Box(i * .25f + .02f, .13f, .12f, i * .25f + .05f, .15f, .98f, Dark, false);
            }
            else if (v == 3)
            {
                for (int i = 0; i < 8; i++)
                {
                    float z0 = i / 8f, z1 = (i + 1) / 8f;
                    b.Slope(0, 1, z0, z1, .08f + .68f * z0 * z0, .08f + .68f * z1 * z1, .09f, Tile);
                }
                b.Box(0, .76f, .94f, 1, .92f, 1, Trim);
            }
            else
            {
                // Reflections are authored DATA, so both handed corner eaves are available.
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f, c = (i + 1) / 8f;
                    b.Box(a, .05f + .48f * a * a, a, c, .14f + .48f * c * c, 1, Tile);
                    if (i < 7) b.Box(c, .05f + .48f * a * a, a, 1, .14f + .48f * c * c, c, Tile);
                }
                b.Beam(new Vector3(.08f, .1f, .08f), new Vector3(.92f, .69f, .92f), .09f, Trim);
            }
        }
        private static void Timber(Builder b, int v)
        {
            if (v == 0)
            {
                b.Box(.28f, 0, .28f, .72f, .12f, .72f, Stone);
                b.Box(.35f, .12f, .35f, .65f, 1, .65f, Wood);
                b.Box(.33f, .85f, .33f, .67f, .92f, .67f, Trim);
            }
            else if (v == 1)
            {
                b.Box(0, .37f, .34f, 1, .65f, .66f, Wood);
                b.Box(0, .37f, .32f, 1, .42f, .34f, Trim);
                b.Box(.06f, .42f, .32f, .13f, .60f, .34f, Dark, false);
                b.Box(.87f, .42f, .32f, .94f, .60f, .34f, Dark, false);
            }
            else if (v == 2)
            {
                b.Box(.38f, .08f, .36f, .62f, .8f, .64f, Wood);
                for (int i = 0; i < 3; i++) b.Box(.28f - .1f * i, .36f + .17f * i, .18f - .07f * i, .72f + .1f * i, .5f + .17f * i, .82f + .07f * i, i == 1 ? Trim : Wood);
            }
            else if (v == 3) b.Beam(new Vector3(.5f, .1f, .1f), new Vector3(.5f, .9f, .9f), .15f, Wood);
            else
            {
                b.Box(.03f, 0, .43f, .15f, .92f, .57f, Wood);
                b.Box(.85f, 0, .43f, .97f, .92f, .57f, Wood);
                b.Box(0, .18f, .46f, 1, .26f, .54f, Trim);
                b.Box(0, .72f, .43f, 1, .84f, .57f, Wood);
                for (int i = 1; i < 5; i++) b.Box(i / 5f - .018f, .26f, .48f, i / 5f + .018f, .72f, .52f, Wood);
            }
        }
        private static void Window(Builder b, int v)
        {
            if (v < 2)
            {
                b.Box(0, 0, .44f, .065f, 1, .56f, Wood); b.Box(.935f, 0, .44f, 1, 1, .56f, Wood);
                b.Box(.065f, 0, .44f, .935f, .065f, .56f, Wood); b.Box(.065f, .935f, .44f, .935f, 1, .56f, Wood);
                int divisions = v == 0 ? 2 : 4;
                for (int i = 1; i < divisions; i++)
                {
                    float a = i / (float)divisions;
                    b.Box(a - .012f, .065f, .47f, a + .012f, .935f, .53f, Trim);
                    b.Box(.065f, a - .012f, .47f, .935f, a + .012f, .53f, Trim);
                }
                if (v == 0) b.Box(.065f, .065f, .496f, .935f, .935f, .504f, new Color(162, 202, 208, 62), false, true);
            }
            else
            {
                bool right = v == 3 || v == 5, lower = v >= 4;
                float cx = right ? 0 : 1, cy = lower ? 1 : 0;
                float start = lower ? (right ? 270 : 180) : (right ? 0 : 90);
                // A 2x2 moon window has four occupied cells, no hidden companion writes.
                for (int i = 0; i < 12; i++)
                {
                    float a = MathUtils.DegToRad(start + i * 7.5f), c = MathUtils.DegToRad(start + (i + 1) * 7.5f);
                    Vector2 u = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                    Vector2 w = new Vector2((float)Math.Cos(c), (float)Math.Sin(c));
                    Vector2 center = new Vector2(cx, cy);
                    b.Extrude(center + u * .86f, center + w * .86f, center + w * .98f, center + u * .98f, .41f, .59f, Trim);
                    float du = 1f / Math.Max(Math.Abs(u.X), Math.Abs(u.Y)), dw = 1f / Math.Max(Math.Abs(w.X), Math.Abs(w.Y));
                    b.Extrude(center + u * .98f, center + w * .98f, center + w * dw, center + u * du, .455f, .545f, Cloth);
                }
            }
        }
        private static void Lantern(Builder b, int v)
        {
            float bottom = v == 0 ? .22f : .15f;
            if (v == 0)
            {
                for (int i = 0; i < 3; i++) b.Box(.48f, .77f + i * .07f, .48f, .52f, .81f + i * .07f, .52f, Dark, false);
                b.Box(.42f, .97f, .42f, .58f, 1, .58f, Wood);
            }
            else if (v == 1)
            {
                b.Box(.43f, .30f, .89f, .57f, .91f, .99f, Wood);
                b.Box(.46f, .81f, .5f, .54f, .89f, .94f, Dark);
                b.Box(.48f, .71f, .48f, .52f, .84f, .52f, Dark, false);
            }
            else b.Box(.32f, 0, .32f, .68f, .13f, .68f, Stone);
            b.Box(.28f, bottom, .28f, .72f, bottom + .07f, .72f, Wood);
            b.Box(.32f, bottom + .07f, .32f, .68f, bottom + .47f, .68f, new Color(255, 201, 94), false, false, true);
            foreach (float x in new[] { .29f, .66f }) foreach (float z in new[] { .29f, .66f }) b.Box(x, bottom + .07f, z, x + .05f, bottom + .50f, z + .05f, Trim);
            b.Box(.24f, bottom + .5f, .24f, .76f, bottom + .57f, .76f, Wood);
            b.Box(.29f, bottom + .57f, .29f, .71f, bottom + .61f, .71f, Dark);
        }
        private static void Tea(Builder b, int v)
        {
            if (v == 0)
            {
                foreach (float x in new[] { .12f, .78f }) foreach (float z in new[] { .15f, .75f }) b.Box(x, 0, z, x + .1f, .32f, z + .1f, Wood);
                b.Box(.04f, .29f, .08f, .96f, .37f, .92f, Trim);
                b.Box(.08f, .37f, .12f, .92f, .39f, .88f, Wood);
                Service(b, .39f);
            }
            else if (v == 1)
            {
                b.Box(.15f, .01f, .15f, .85f, .08f, .85f, new Color(127, 82, 64));
                b.Box(.18f, .08f, .18f, .82f, .18f, .82f, Cloth);
                b.Box(.23f, .18f, .23f, .77f, .19f, .77f, new Color(203, 191, 161), false);
            }
            else if (v == 2) Service(b, .02f);
            else
            {
                b.Box(0, .92f, .47f, 1, .98f, .53f, Wood, false);
                for (int i = 0; i < 3; i++)
                {
                    float x = i / 3f;
                    b.Box(x + .015f, .30f + .025f * (i & 1), .49f, x + .318f, .92f, .51f, Cloth, false);
                    b.Box(x + .07f, .38f, .478f, x + .26f, .42f, .49f, new Color(144, 157, 165), false);
                }
            }
        }
        private static void Service(Builder b, float y)
        {
            b.Box(.23f, y, .25f, .77f, y + .025f, .75f, Dark, false);
            b.Cylinder(new Vector3(.49f, y + .035f, .48f), new Vector3(.49f, y + .18f, .48f), .10f, .075f, Dark, false);
            b.Cylinder(new Vector3(.49f, y + .18f, .48f), new Vector3(.49f, y + .20f, .48f), .08f, .06f, Copper, false);
            b.Beam(new Vector3(.56f, y + .09f, .48f), new Vector3(.66f, y + .16f, .48f), .042f, Dark, false);
            b.Beam(new Vector3(.39f, y + .06f, .48f), new Vector3(.34f, y + .17f, .48f), .025f, Copper, false);
            b.Beam(new Vector3(.34f, y + .17f, .48f), new Vector3(.41f, y + .19f, .48f), .025f, Copper, false);
            foreach (float x in new[] { .32f, .66f }) b.Cylinder(new Vector3(x, y + .026f, .65f), new Vector3(x, y + .09f, .65f), .038f, .05f, Cloth, false);
        }
        private static void Garden(Builder b, int v)
        {
            if (v == 0)
            {
                b.Beam(new Vector3(.48f, .02f, .5f), new Vector3(.53f, .7f, .5f), .07f, Wood);
                b.Beam(new Vector3(.5f, .34f, .5f), new Vector3(.16f, .72f, .43f), .045f, Wood, false);
                b.Beam(new Vector3(.51f, .48f, .5f), new Vector3(.84f, .88f, .6f), .038f, Wood, false);
                for (int i = 0; i < 18; i++)
                {
                    float x = .12f + (i * 37 % 73) / 100f, z = .15f + (i * 23 % 67) / 100f, y = .56f + (i * 13 % 31) / 100f;
                    b.Box(x, y, z, x + .045f, y + .03f, z + .10f, Pink, false);
                    b.Box(x - .025f, y + .008f, z + .03f, x + .075f, y + .035f, z + .065f, new Color(255, 196, 210), false);
                }
            }
            else if (v == 1)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = .20f + .27f * i;
                    b.Beam(new Vector3(x, .95f, .83f), new Vector3(x + .08f, .08f + .09f * i, .83f), .018f, Wood, false);
                    for (int j = 0; j < 6; j++) b.Box(x - .07f + (j % 2) * .10f, .10f + j * .14f, .77f, x + .01f + (j % 2) * .10f, .20f + j * .14f, .86f, Leaf, false);
                }
            }
            else
            {
                b.Box(.17f, 0, .17f, .83f, .27f, .83f, Stone);
                b.Box(.13f, .23f, .13f, .87f, .31f, .87f, new Color(157, 158, 147));
                b.Box(.20f, .30f, .20f, .80f, .315f, .80f, Dark, false);
                if (v == 2)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float x = .31f + .18f * i, top = .95f - .1f * i;
                        b.Cylinder(new Vector3(x, .31f, .5f), new Vector3(x, top, .5f), .026f, .026f, new Color(99, 140, 55), false);
                        for (float y = .48f; y < top; y += .18f)
                        {
                            b.Box(x - .034f, y, .466f, x + .034f, y + .02f, .534f, new Color(171, 166, 82), false);
                            b.Beam(new Vector3(x, y + .01f, .5f), new Vector3(x - .12f, y + .1f, .42f), .025f, Leaf, false);
                        }
                    }
                }
                else for (int i = 0; i < 7; i++)
                {
                    float x = .26f + (i * 17 % 47) / 100f, z = .28f + (i * 13 % 43) / 100f, y = .48f + (i % 3) * .09f;
                    b.Beam(new Vector3(x, .31f, z), new Vector3(x, y, z), .018f, Leaf, false);
                    b.Box(x - .05f, y, z - .05f, x + .05f, y + .035f, z + .05f, i % 2 == 0 ? Cloth : Pink, false);
                }
            }
        }
        private static void Telescope(Builder b)
        {
            var hub = new Vector3(.5f, .63f, .53f);
            foreach (var foot in new[] { new Vector3(.14f, .03f, .18f), new Vector3(.86f, .03f, .18f), new Vector3(.50f, .03f, .91f) })
                b.Beam(foot, hub, .055f, Wood);
            b.Cylinder(new Vector3(.5f, .60f, .53f), new Vector3(.5f, .77f, .53f), .045f, .045f, Dark, false);
            var a = new Vector3(.50f, .68f, .82f); var c = new Vector3(.50f, .87f, .12f);
            b.Cylinder(a, Vector3.Lerp(a,c,.98f), .065f, .10f, Copper);
            b.Cylinder(Vector3.Lerp(a, c, .76f), Vector3.Lerp(a, c, .82f), .10f, .10f, Dark, false);
            b.Cylinder(Vector3.Lerp(a, c, .98f), c, .085f, .085f, new Color(36, 68, 76), false);
            b.Cylinder(a, Vector3.Lerp(a, c, .08f), .073f, .073f, Dark, false);
        }
        private sealed class Builder
        {
            private readonly Shape shape = new Shape();
            private readonly List<BoundingBox> boxes = new List<BoundingBox>();
            private readonly Matrix transform;
            private Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            internal Builder(int data)
            {
                transform = Matrix.CreateTranslation(-.5f, 0, -.5f) * Matrix.CreateScale((data & 4) != 0 ? -1f : 1f, 1, 1)
                    * Matrix.CreateRotationY((data & 3) * MathUtils.PI / 2) * Matrix.CreateTranslation(.5f, 0, .5f);
            }
            internal Shape Finish()
            {
                shape.Bounds = new BoundingBox(min, max);
                shape.Collision = boxes.ToArray(); shape.Interaction = new[] { shape.Bounds };
                shape.Opaque.GenerateSidesData(); shape.Glass.GenerateSidesData();
                return shape;
            }
            internal void Box(float x0, float y0, float z0, float x1, float y1, float z1, Color color, bool collision = true, bool glass = false, bool glow = false)
                => Solid(new[] { new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y1,z0),new Vector3(x0,y1,z0),new Vector3(x0,y0,z1),new Vector3(x1,y0,z1),new Vector3(x1,y1,z1),new Vector3(x0,y1,z1) },color,collision,glass,glow);
            internal void Slope(float x0, float x1, float z0, float z1, float y0, float y1, float thick, Color color, bool collision = true)
                => Solid(new[] { new Vector3(x0,y0,z0),new Vector3(x1,y0,z0),new Vector3(x1,y0+thick,z0),new Vector3(x0,y0+thick,z0),new Vector3(x0,y1,z1),new Vector3(x1,y1,z1),new Vector3(x1,y1+thick,z1),new Vector3(x0,y1+thick,z1) },color,collision,false,false);
            internal void Extrude(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float z0, float z1, Color color)
                => Solid(new[] { new Vector3(a.X,a.Y,z0),new Vector3(b.X,b.Y,z0),new Vector3(c.X,c.Y,z0),new Vector3(d.X,d.Y,z0),new Vector3(a.X,a.Y,z1),new Vector3(b.X,b.Y,z1),new Vector3(c.X,c.Y,z1),new Vector3(d.X,d.Y,z1) },color,true,false,false);
            internal void Beam(Vector3 a, Vector3 b, float width, Color color, bool collision = true)
            {
                Vector3 axis = (b - a) / (b - a).Length();
                Vector3 u = Vector3.Cross(axis, Math.Abs(axis.Y) > .9f ? Vector3.UnitX : Vector3.UnitY); u *= width / (2 * u.Length());
                Vector3 w = Vector3.Cross(axis, u); w *= width / (2 * w.Length());
                Solid(new[] { a-u-w,a+u-w,a+u+w,a-u+w,b-u-w,b+u-w,b+u+w,b-u+w }, color, collision, false, false);
            }
            internal void Cylinder(Vector3 a, Vector3 b, float r0, float r1, Color color, bool collision = true)
            {
                Vector3 axis = (b-a)/(b-a).Length(), u = Vector3.Cross(axis, Math.Abs(axis.Y)>.9f?Vector3.UnitX:Vector3.UnitY); u /= u.Length();
                Vector3 w = Vector3.Cross(axis,u), lo = new Vector3(float.MaxValue), hi = new Vector3(float.MinValue);
                for (int i=0;i<12;i++)
                {
                    float p=i*(2*MathUtils.PI)/12, q=(i+1)*(2*MathUtils.PI)/12;
                    Vector3 du=u*(float)Math.Cos(p)+w*(float)Math.Sin(p), dv=u*(float)Math.Cos(q)+w*(float)Math.Sin(q);
                    Vector3 p0=a+du*r0,p1=a+dv*r0,p2=b+dv*r1,p3=b+du*r1;
                    Quad(p0,p1,p2,p3,du+dv,color,false,false);
                    Triangle(a,p1,p0,-axis,color); Triangle(b,p3,p2,axis,color);
                    foreach(var point in new[]{p0,p1,p2,p3}){var t=Vector3.Transform(point,transform);lo=Vector3.Min(lo,t);hi=Vector3.Max(hi,t);}
                }
                if(collision)boxes.Add(new BoundingBox(lo,hi));
            }
            private void Solid(Vector3[] p, Color color, bool collision, bool glass, bool glow)
            {
                Vector3 center=Vector3.Zero,lo=new Vector3(float.MaxValue),hi=new Vector3(float.MinValue);
                foreach(var point in p){center+=point;var t=Vector3.Transform(point,transform);lo=Vector3.Min(lo,t);hi=Vector3.Max(hi,t);}center/=8;
                int[][] faces={new[]{4,5,6,7},new[]{1,2,6,5},new[]{0,1,2,3},new[]{0,3,7,4},new[]{3,2,6,7},new[]{0,1,5,4}};
                foreach(var f in faces){Vector3 normal=(p[f[0]]+p[f[1]]+p[f[2]]+p[f[3]])*.25f-center;Quad(p[f[0]],p[f[1]],p[f[2]],p[f[3]],normal,color,glass,glow);}
                if(collision)boxes.Add(new BoundingBox(lo,hi));
            }
            private void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal,Color color)
            {
                // Clockwise front faces, matching the actual native terrain mesh convention.
                var mesh=shape.Opaque;Vector3 n=Vector3.TransformNormal(normal,transform);
                a=Vector3.Transform(a,transform);b=Vector3.Transform(b,transform);c=Vector3.Transform(c,transform);
                bool reverse=Vector3.Dot(Vector3.Cross(b-a,c-a),n)>0;
                n=Vector3.Cross(b-a,c-a)*(reverse?1:-1);
                int start=mesh.Vertices.Count;Add(mesh,a,n,color,false);Add(mesh,b,n,color,false);Add(mesh,c,n,color,false);
                mesh.Indices.Add((ushort)start);mesh.Indices.Add((ushort)(start+(reverse?2:1)));mesh.Indices.Add((ushort)(start+(reverse?1:2)));
            }
            private void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,Color color,bool glass,bool glow)
            {
                var mesh=glass?shape.Glass:shape.Opaque;Vector3 n=Vector3.TransformNormal(normal,transform);
                a=Vector3.Transform(a,transform);b=Vector3.Transform(b,transform);c=Vector3.Transform(c,transform);d=Vector3.Transform(d,transform);
                bool reverse=Vector3.Dot(Vector3.Cross(b-a,c-a),n)>0;
                n=Vector3.Cross(b-a,c-a)*(reverse?1:-1);
                int start=mesh.Vertices.Count;Add(mesh,a,n,color,glow);Add(mesh,b,n,color,glow);Add(mesh,c,n,color,glow);Add(mesh,d,n,color,glow);
                foreach(int i in reverse?new[]{0,2,1,2,0,3}:new[]{0,1,2,2,3,0})mesh.Indices.Add((ushort)(start+i));
            }
            private void Add(BlockMesh mesh,Vector3 p,Vector3 n,Color color,bool glow)
            {
                min=Vector3.Min(min,p);max=Vector3.Max(max,p);
                int face=Math.Abs(n.Y)>=Math.Abs(n.X)&&Math.Abs(n.Y)>=Math.Abs(n.Z)?(n.Y>0?4:5):Math.Abs(n.X)>Math.Abs(n.Z)?(n.X>0?1:3):(n.Z>0?0:2);
                float shade=glow?1:face==5?.62f:face==1||face==3?.83f:face==0?.92f:1;
                mesh.Vertices.Add(new BlockMeshVertex{Position=p,TextureCoordinates=new Vector2(.5f),Color=Color.MultiplyColorOnly(color,shade),Face=(byte)face,IsEmissive=glow});
            }
        }
    }
}
