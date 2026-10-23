[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

# Wilderness Explorer X1 - low-poly adventure motorcycle asset generator.
# Replaces the old cuboid-only bike with a structured model built from reusable
# primitives (frustum/cylinder, tube, loft with tapered sections, torus, arc plate).
# Output stays inside the SCNET rendering contract: Y_UP, baked vertices, one
# identity scene node, N independent geometries, opaque PNG palette texture.

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\ATV.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\ATV.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

$geometryCore = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public struct V3
{
    public double X; public double Y; public double Z;
    public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static V3 operator +(V3 a, V3 b) { return new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
    public static V3 operator -(V3 a, V3 b) { return new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
    public static V3 operator *(V3 a, double s) { return new V3(a.X * s, a.Y * s, a.Z * s); }
    public double Dot(V3 o) { return X * o.X + Y * o.Y + Z * o.Z; }
    public V3 Cross(V3 o)
    {
        return new V3(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);
    }
    public double Len() { return Math.Sqrt(Dot(this)); }
    public V3 Norm()
    {
        double l = Len();
        if (l < 1e-12) { return new V3(0, 0, 0); }
        return new V3(X / l, Y / l, Z / l);
    }
}

public struct UvRect
{
    public double U0; public double V0; public double U1; public double V1;
    public UvRect(double u0, double v0, double u1, double v1) { U0 = u0; V0 = v0; U1 = u1; V1 = v1; }
    public double MapU(double t) { return U0 + (U1 - U0) * t; }
    public double MapV(double t) { return V0 + (V1 - V0) * t; }
}

public class Mesh
{
    public List<double> Positions = new List<double>();
    public List<double> Normals = new List<double>();
    public List<double> Uvs = new List<double>();
    public List<int> Indices = new List<int>();
    public int VertexCount = 0;
    public int TriangleCount = 0;

    public int AddVertex(V3 p, V3 n, double u, double v)
    {
        Positions.Add(p.X); Positions.Add(p.Y); Positions.Add(p.Z);
        Normals.Add(n.X); Normals.Add(n.Y); Normals.Add(n.Z);
        Uvs.Add(u); Uvs.Add(v);
        VertexCount++;
        return VertexCount - 1;
    }

    public void AddTri(int a, int b, int c)
    {
        Indices.Add(a); Indices.Add(b); Indices.Add(c);
        TriangleCount++;
    }

    // Flat-shaded triangle; winding auto-corrected so the face normal points
    // away from the supplied interior hint point.
    public void TriAuto(V3 a, V3 b, V3 c, V3 hint, UvRect r, double ua, double va, double ub, double vb, double uc, double vc)
    {
        V3 n = (b - a).Cross(c - a);
        V3 centroid = new V3((a.X + b.X + c.X) / 3.0, (a.Y + b.Y + c.Y) / 3.0, (a.Z + b.Z + c.Z) / 3.0);
        int ia = AddVertex(a, new V3(0, 0, 0), r.MapU(ua), r.MapV(va));
        int ib = AddVertex(b, new V3(0, 0, 0), r.MapU(ub), r.MapV(vb));
        int ic = AddVertex(c, new V3(0, 0, 0), r.MapU(uc), r.MapV(vc));
        if (n.Dot(centroid - hint) < 0)
        {
            int tmp = ib; ib = ic; ic = tmp;
        }
        V3 fn = FaceNormal(a, b, c);
        PatchNormal(ia, fn); PatchNormal(ib, fn); PatchNormal(ic, fn);
        AddTri(ia, ib, ic);
    }

    public void QuadAuto(V3 a, V3 b, V3 c, V3 d, V3 hint, UvRect r, double ua, double va, double ub, double vb, double uc, double vc, double ud, double vd)
    {
        TriAuto(a, b, c, hint, r, ua, va, ub, vb, uc, vc);
        TriAuto(a, c, d, hint, r, ua, va, uc, vc, ud, vd);
    }

    private static V3 FaceNormal(V3 a, V3 b, V3 c)
    {
        V3 n = (b - a).Cross(c - a);
        double l = n.Len();
        if (l < 1e-12) { return new V3(0, 1, 0); }
        return n * (1.0 / l);
    }

    private void PatchNormal(int index, V3 n)
    {
        Normals[index * 3] = n.X;
        Normals[index * 3 + 1] = n.Y;
        Normals[index * 3 + 2] = n.Z;
    }
}

public static class Geo
{
    public static void Basis(V3 axis, out V3 e1, out V3 e2)
    {
        V3 helper = Math.Abs(axis.Y) < 0.9 ? new V3(0, 1, 0) : new V3(1, 0, 0);
        e1 = helper.Cross(axis).Norm();
        e2 = axis.Cross(e1).Norm();
    }

    // Cylinder / cone frustum between two points (wheels hubs, fork tubes, muffler...).
    public static void Frustum(Mesh m, V3 p0, V3 p1, double r0, double r1, int sides, bool cap0, bool cap1, UvRect r)
    {
        V3 axis = (p1 - p0).Norm();
        V3 e1, e2;
        Basis(axis, out e1, out e2);
        V3[] ring0 = new V3[sides];
        V3[] ring1 = new V3[sides];
        for (int i = 0; i < sides; i++)
        {
            double a = 2.0 * Math.PI * i / sides;
            V3 dir = e1 * Math.Cos(a) + e2 * Math.Sin(a);
            ring0[i] = p0 + dir * r0;
            ring1[i] = p1 + dir * r1;
        }
        V3 mid = (p0 + p1) * 0.5;
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            double u0 = (double)i / sides;
            double u1 = (double)(i + 1) / sides;
            m.QuadAuto(ring0[i], ring0[j], ring1[j], ring1[i], mid, r, u0, 0, u1, 0, u1, 1, u0, 1);
        }
        if (cap1) { CapFan(m, ring1, p1, p1 - axis * 0.5, r, false); }
        if (cap0) { CapFan(m, ring0, p0, p0 + axis * 0.5, r, true); }
    }

    private static void CapFan(Mesh m, V3[] ring, V3 center, V3 hint, UvRect r, bool reverse)
    {
        int sides = ring.Length;
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            double a0 = 2.0 * Math.PI * i / sides;
            double a1 = 2.0 * Math.PI * j / sides;
            if (reverse) { m.TriAuto(center, ring[j], ring[i], hint, r, 0.5, 0.5, 0.5 + 0.5 * Math.Cos(a1), 0.5 + 0.5 * Math.Sin(a1), 0.5 + 0.5 * Math.Cos(a0), 0.5 + 0.5 * Math.Sin(a0)); }
            else { m.TriAuto(center, ring[i], ring[j], hint, r, 0.5, 0.5, 0.5 + 0.5 * Math.Cos(a0), 0.5 + 0.5 * Math.Sin(a0), 0.5 + 0.5 * Math.Cos(a1), 0.5 + 0.5 * Math.Sin(a1)); }
        }
    }

    // Pipe along a polyline with parallel-transported rings (frame, exhaust, handlebar).
    public static void Tube(Mesh m, V3[] path, double radius, int sides, bool caps, UvRect r)
    {
        int n = path.Length;
        V3[] dirs = new V3[n];
        for (int i = 0; i < n; i++)
        {
            if (i == 0) { dirs[i] = (path[1] - path[0]).Norm(); }
            else if (i == n - 1) { dirs[i] = (path[n - 1] - path[n - 2]).Norm(); }
            else { dirs[i] = (path[i + 1] - path[i - 1]).Norm(); }
        }
        double[] cum = new double[n];
        for (int i = 1; i < n; i++) { cum[i] = cum[i - 1] + (path[i] - path[i - 1]).Len(); }
        double total = cum[n - 1];
        if (total < 1e-9) { total = 1; }

        V3[][] rings = new V3[n][];
        V3 e1 = new V3(0, 0, 0), e2 = new V3(0, 0, 0);
        for (int i = 0; i < n; i++)
        {
            if (i == 0) { Basis(dirs[0], out e1, out e2); }
            else
            {
                e1 = (e1 - dirs[i] * e1.Dot(dirs[i])).Norm();
                e2 = dirs[i].Cross(e1).Norm();
            }
            rings[i] = new V3[sides];
            for (int k = 0; k < sides; k++)
            {
                double a = 2.0 * Math.PI * k / sides;
                rings[i][k] = path[i] + e1 * (Math.Cos(a) * radius) + e2 * (Math.Sin(a) * radius);
            }
        }
        for (int i = 0; i < n - 1; i++)
        {
            V3 hint = (path[i] + path[i + 1]) * 0.5;
            double v0 = cum[i] / total;
            double v1 = cum[i + 1] / total;
            for (int k = 0; k < sides; k++)
            {
                int k2 = (k + 1) % sides;
                double u0 = (double)k / sides;
                double u1 = (double)(k + 1) / sides;
                m.QuadAuto(rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k], hint, r, u0, v0, u1, v0, u1, v1, u0, v1);
            }
        }
        if (caps)
        {
            CapFan(m, rings[n - 1], path[n - 1], path[n - 1] - dirs[n - 1] * 0.5, r, false);
            CapFan(m, rings[0], path[0], path[0] + dirs[0] * 0.5, r, true);
        }
    }

    // Loft through equal-size closed cross sections (fuel tank, seat, tire carcass...).
    public static void Loft(Mesh m, List<V3[]> sections, bool closedPath, bool capStart, bool capEnd, UvRect r)
    {
        int count = sections.Count;
        int loop = sections[0].Length;
        int bridges = closedPath ? count : count - 1;
        for (int i = 0; i < bridges; i++)
        {
            int i2 = (i + 1) % count;
            V3 c0 = Centroid(sections[i]);
            V3 c1 = Centroid(sections[i2]);
            V3 hint = (c0 + c1) * 0.5;
            double v0 = (double)i / bridges;
            double v1 = (double)(i + 1) / bridges;
            for (int k = 0; k < loop; k++)
            {
                int k2 = (k + 1) % loop;
                double u0 = (double)k / loop;
                double u1 = (double)(k + 1) / loop;
                m.QuadAuto(sections[i][k], sections[i][k2], sections[i2][k2], sections[i2][k], hint, r, u0, v0, u1, v0, u1, v1, u0, v1);
            }
        }
        if (capStart)
        {
            V3 c = Centroid(sections[0]);
            V3 hint = c + (Centroid(sections[1]) - c).Norm() * 0.5;
            CapFan(m, sections[0], c, hint, r, true);
        }
        if (capEnd)
        {
            V3 c = Centroid(sections[count - 1]);
            V3 hint = c + (Centroid(sections[count - 2]) - c).Norm() * 0.5;
            CapFan(m, sections[count - 1], c, hint, r, false);
        }
    }

    public static V3 Centroid(V3[] ring)
    {
        V3 s = new V3(0, 0, 0);
        for (int i = 0; i < ring.Length; i++) { s = s + ring[i]; }
        return s * (1.0 / ring.Length);
    }

    // Torus (shock springs, tire bead rings).
    public static void Torus(Mesh m, V3 center, V3 axis, double majorR, double minorR, int majSeg, int minSeg, UvRect r)
    {
        V3 a = axis.Norm();
        V3 e1, e2;
        Basis(a, out e1, out e2);
        V3[][] grid = new V3[majSeg][];
        V3[] ringCenter = new V3[majSeg];
        for (int i = 0; i < majSeg; i++)
        {
            double phi = 2.0 * Math.PI * i / majSeg;
            V3 radial = e1 * Math.Cos(phi) + e2 * Math.Sin(phi);
            ringCenter[i] = center + radial * majorR;
            grid[i] = new V3[minSeg];
            for (int j = 0; j < minSeg; j++)
            {
                double th = 2.0 * Math.PI * j / minSeg;
                grid[i][j] = ringCenter[i] + radial * (Math.Cos(th) * minorR) + a * (Math.Sin(th) * minorR);
            }
        }
        for (int i = 0; i < majSeg; i++)
        {
            int i2 = (i + 1) % majSeg;
            for (int j = 0; j < minSeg; j++)
            {
                int j2 = (j + 1) % minSeg;
                double u0 = (double)i / majSeg, u1 = (double)(i + 1) / majSeg;
                double v0 = (double)j / minSeg, v1 = (double)(j + 1) / minSeg;
                m.QuadAuto(grid[i][j], grid[i][j2], grid[i2][j2], grid[i2][j], ringCenter[i], r, u0, v0, u1, v0, u1, v1, u0, v1);
            }
        }
    }

    // Curved slab swept along an arc (fenders).
    public static void ArcPlate(Mesh m, V3 center, V3 widthDir, V3 d1, V3 d2, double rIn, double rOut, double a0, double a1, int seg, double width, UvRect r)
    {
        V3 w = widthDir.Norm();
        List<V3[]> sections = new List<V3[]>();
        for (int i = 0; i <= seg; i++)
        {
            double phi = a0 + (a1 - a0) * i / seg;
            V3 radial = d1 * Math.Cos(phi) + d2 * Math.Sin(phi);
            V3 po = center + radial * rOut;
            V3 pi = center + radial * rIn;
            sections.Add(new V3[] {
                po - w * (width * 0.5),
                pi - w * (width * 0.5),
                pi + w * (width * 0.5),
                po + w * (width * 0.5)
            });
        }
        Loft(m, sections, false, true, true, r);
    }

    // Oriented box from center + three half-extent basis vectors.
    public static void Box(Mesh m, V3 c, V3 ex, V3 ey, V3 ez, UvRect r)
    {
        V3 c000 = c - ex - ey - ez, c100 = c + ex - ey - ez, c110 = c + ex + ey - ez, c010 = c - ex + ey - ez;
        V3 c001 = c - ex - ey + ez, c101 = c + ex - ey + ez, c111 = c + ex + ey + ez, c011 = c - ex + ey + ez;
        m.QuadAuto(c001, c101, c111, c011, c, r, 0, 0, 1, 0, 1, 1, 0, 1);
        m.QuadAuto(c000, c010, c110, c100, c, r, 0, 0, 1, 0, 1, 1, 0, 1);
        m.QuadAuto(c100, c110, c111, c101, c, r, 0, 0, 1, 0, 1, 1, 0, 1);
        m.QuadAuto(c000, c001, c011, c010, c, r, 0, 0, 1, 0, 1, 1, 0, 1);
        m.QuadAuto(c010, c011, c111, c110, c, r, 0, 0, 1, 0, 1, 1, 0, 1);
        m.QuadAuto(c000, c100, c101, c001, c, r, 0, 0, 1, 0, 1, 1, 0, 1);
    }

    // Axis-aligned box from center + half extents.
    public static void BoxA(Mesh m, V3 c, double hx, double hy, double hz, UvRect r)
    {
        Box(m, c, new V3(hx, 0, 0), new V3(0, hy, 0), new V3(0, 0, hz), r);
    }
}

public static class X1Builder
{
    private static UvRect Region(int col, int row)
    {
        double u0 = (col * 16 + 1.0) / 64.0;
        double u1 = (col * 16 + 15.0) / 64.0;
        double v0 = (row * 16 + 1.0) / 64.0;
        double v1 = (row * 16 + 15.0) / 64.0;
        return new UvRect(u0, v0, u1, v1);
    }

    private static readonly UvRect Orange = Region(0, 0);
    private static readonly UvRect OrangeDecal = Region(1, 0);
    private static readonly UvRect Carbon = Region(2, 0);
    private static readonly UvRect Rubber = Region(3, 0);
    private static readonly UvRect Silver = Region(0, 1);
    private static readonly UvRect Steel = Region(1, 1);
    private static readonly UvRect Seat = Region(2, 1);
    private static readonly UvRect Chrome = Region(3, 1);
    private static readonly UvRect Lens = Region(0, 2);
    private static readonly UvRect Red = Region(1, 2);
    private static readonly UvRect Amber = Region(2, 2);
    private static readonly UvRect Fork = Region(3, 2);
    private static readonly UvRect Grip = Region(0, 3);
    private static readonly UvRect Rim = Region(1, 3);
    private static readonly UvRect Panel = Region(2, 3);

    private static V3 V(double x, double y, double z) { return new V3(x, y, z); }

    private static void Wheel(Mesh m, double cz, double cy, double width, double rTire)
    {
        int sides = 16;
        double rIn = rTire - 0.085;
        List<V3[]> tire = new List<V3[]>();
        for (int i = 0; i < sides; i++)
        {
            double a = 2.0 * Math.PI * i / sides;
            double ry = Math.Cos(a);
            double rz = Math.Sin(a);
            tire.Add(new V3[] {
                V(-width * 0.5, cy + ry * rTire, cz + rz * rTire),
                V(-width * 0.5, cy + ry * rIn, cz + rz * rIn),
                V(width * 0.5, cy + ry * rIn, cz + rz * rIn),
                V(width * 0.5, cy + ry * rTire, cz + rz * rTire)
            });
        }
        Geo.Loft(m, tire, true, false, false, Rubber);

        // Off-road tread lugs: two staggered rows of radial blocks.
        for (int i = 0; i < sides; i++)
        {
            double a = 2.0 * Math.PI * (i + 0.5) / sides;
            V3 radial = V(0, Math.Cos(a), Math.Sin(a));
            V3 tangent = V(0, -Math.Sin(a), Math.Cos(a));
            double off = (i % 2 == 0) ? -width * 0.21 : width * 0.21;
            V3 c = V(off, cy, cz) + radial * (rTire + 0.008);
            Geo.Box(m, c, V(width * 0.17, 0, 0), tangent * 0.030, radial * 0.020, Rubber);
        }

        // Rim band + hub + spokes + axle.
        List<V3[]> rim = new List<V3[]>();
        double rRimO = rIn + 0.005;
        double rRimI = rIn - 0.035;
        for (int i = 0; i < sides; i++)
        {
            double a = 2.0 * Math.PI * i / sides;
            double ry = Math.Cos(a);
            double rz = Math.Sin(a);
            rim.Add(new V3[] {
                V(-width * 0.42, cy + ry * rRimO, cz + rz * rRimO),
                V(-width * 0.42, cy + ry * rRimI, cz + rz * rRimI),
                V(width * 0.42, cy + ry * rRimI, cz + rz * rRimI),
                V(width * 0.42, cy + ry * rRimO, cz + rz * rRimO)
            });
        }
        Geo.Loft(m, rim, true, false, false, Rim);
        for (int s = 0; s < 6; s++)
        {
            double a = 2.0 * Math.PI * s / 6;
            V3 radial = V(0, Math.Cos(a), Math.Sin(a));
            V3 tangent = V(0, -Math.Sin(a), Math.Cos(a));
            V3 c = V(0, cy, cz) + radial * ((rRimI + 0.115) * 0.5);
            Geo.Box(m, c, V(width * 0.10, 0, 0), radial * ((rRimI - 0.115) * 0.5 + 0.01), tangent * 0.022, Rim);
        }
        Geo.Frustum(m, V(-width * 0.30, cy, cz), V(width * 0.30, cy, cz), 0.085, 0.085, 12, true, true, Steel);
        Geo.Frustum(m, V(-width * 0.5 - 0.03, cy, cz), V(width * 0.5 + 0.03, cy, cz), 0.024, 0.024, 8, true, true, Steel);
    }

    public static Dictionary<string, Mesh> Build()
    {
        Dictionary<string, Mesh> parts = new Dictionary<string, Mesh>();

        double fz = -0.92, rz = 0.90;
        double frTire = 0.34, rrTire = 0.36;
        double fcy = frTire + 0.030, rcy = rrTire + 0.030;

        Mesh fw = new Mesh();
        Wheel(fw, fz, fcy, 0.16, frTire);
        parts.Add("BikeFrontWheel", fw);

        Mesh rw = new Mesh();
        Wheel(rw, rz, rcy, 0.20, rrTire);
        parts.Add("BikeRearWheel", rw);

        // Front fork: twin tilted tubes + sliders + axle + triple clamps.
        Mesh fork = new Mesh();
        double[] fx = new double[] { -0.10, 0.10 };
        for (int i = 0; i < 2; i++)
        {
            Geo.Frustum(fork, V(fx[i], 1.06, -0.68), V(fx[i], 0.72, -0.80), 0.026, 0.026, 8, true, true, Fork);
            Geo.Frustum(fork, V(fx[i], 0.74, -0.80), V(fx[i], fcy, fz), 0.038, 0.038, 8, true, true, Fork);
        }
        Geo.Frustum(fork, V(-0.15, fcy, fz), V(0.15, fcy, fz), 0.024, 0.024, 8, true, true, Steel);
        Geo.BoxA(fork, V(0, 1.03, -0.70), 0.13, 0.020, 0.045, Carbon);
        parts.Add("BikeFrontFork", fork);

        // Handlebar with risers and grips.
        Mesh bar = new Mesh();
        Geo.Tube(bar, new V3[] { V(-0.34, 1.10, -0.66), V(-0.16, 1.12, -0.70), V(0, 1.13, -0.72), V(0.16, 1.12, -0.70), V(0.34, 1.10, -0.66) }, 0.020, 8, true, Carbon);
        Geo.Frustum(bar, V(-0.07, 1.02, -0.70), V(-0.07, 1.115, -0.715), 0.018, 0.018, 8, true, true, Carbon);
        Geo.Frustum(bar, V(0.07, 1.02, -0.70), V(0.07, 1.115, -0.715), 0.018, 0.018, 8, true, true, Carbon);
        Geo.Frustum(bar, V(-0.23, 1.111, -0.682), V(-0.365, 1.097, -0.655), 0.030, 0.030, 8, true, true, Grip);
        Geo.Frustum(bar, V(0.23, 1.111, -0.682), V(0.365, 1.097, -0.655), 0.030, 0.030, 8, true, true, Grip);
        Geo.BoxA(bar, V(0, 1.165, -0.70), 0.075, 0.022, 0.045, Carbon);
        parts.Add("BikeHandlebar", bar);

        // Headlight: square LED pod with lens.
        Mesh light = new Mesh();
        Geo.BoxA(light, V(0, 0.97, -0.845), 0.095, 0.095, 0.030, Carbon);
        Geo.BoxA(light, V(0, 0.97, -0.795), 0.030, 0.030, 0.055, Carbon);
        Geo.BoxA(light, V(0, 0.97, -0.878), 0.070, 0.070, 0.012, Lens);
        Geo.BoxA(light, V(-0.115, 0.97, -0.845), 0.018, 0.030, 0.022, Amber);
        Geo.BoxA(light, V(0.115, 0.97, -0.845), 0.018, 0.030, 0.022, Amber);
        parts.Add("BikeHeadlight", light);

        // Tubular frame: spine, downtube, cradle, diagonal, side tubes, subframe.
        Mesh frame = new Mesh();
        Geo.Tube(frame, new V3[] { V(0, 0.96, -0.74), V(0, 0.90, -0.30), V(0, 0.84, 0.10), V(0, 0.80, 0.42) }, 0.028, 6, true, Carbon);
        Geo.Tube(frame, new V3[] { V(0, 0.96, -0.74), V(0, 0.62, -0.52), V(0, 0.50, -0.18), V(0, 0.50, 0.18) }, 0.028, 6, true, Carbon);
        Geo.Tube(frame, new V3[] { V(0, 0.50, 0.18), V(0, 0.62, 0.48), V(0, 0.78, 0.60) }, 0.026, 6, true, Carbon);
        Geo.Tube(frame, new V3[] { V(0, 0.52, 0.00), V(0, 0.82, 0.28) }, 0.022, 6, true, Carbon);
        double[] sx = new double[] { -0.11, 0.11 };
        for (int i = 0; i < 2; i++)
        {
            Geo.Tube(frame, new V3[] { V(sx[i], 0.52, -0.30), V(sx[i], 0.48, 0.10), V(sx[i], 0.56, 0.42) }, 0.022, 6, true, Carbon);
            Geo.Tube(frame, new V3[] { V(sx[i] * 0.82, 0.78, 0.50), V(sx[i] * 0.82, 0.86, 0.86), V(sx[i] * 0.82, 0.90, 1.10) }, 0.016, 6, true, Carbon);
        }
        Geo.Tube(frame, new V3[] { V(-0.09, 0.90, 1.02), V(0.09, 0.90, 1.02) }, 0.014, 6, true, Carbon);
        Geo.Tube(frame, new V3[] { V(-0.09, 0.92, 1.16), V(0.09, 0.92, 1.16) }, 0.014, 6, true, Carbon);
        parts.Add("BikeFrame", frame);

        // Engine: crankcase, finned cylinder, head, skid plate.
        Mesh engine = new Mesh();
        Geo.BoxA(engine, V(0, 0.50, -0.02), 0.115, 0.10, 0.15, Steel);
        Geo.Frustum(engine, V(0, 0.58, -0.11), V(0, 0.74, -0.14), 0.075, 0.062, 12, true, true, Steel);
        Geo.Frustum(engine, V(0, 0.615, -0.117), V(0, 0.630, -0.120), 0.098, 0.098, 12, true, true, Steel);
        Geo.Frustum(engine, V(0, 0.660, -0.125), V(0, 0.675, -0.128), 0.094, 0.094, 12, true, true, Steel);
        Geo.Frustum(engine, V(0, 0.705, -0.133), V(0, 0.720, -0.136), 0.090, 0.090, 12, true, true, Steel);
        Geo.BoxA(engine, V(0, 0.765, -0.15), 0.058, 0.020, 0.068, Silver);
        Geo.BoxA(engine, V(0, 0.40, -0.14), 0.10, 0.045, 0.06, Carbon);
        Geo.BoxA(engine, V(-0.128, 0.52, -0.02), 0.012, 0.075, 0.115, Steel);
        Geo.BoxA(engine, V(0.128, 0.52, -0.02), 0.012, 0.075, 0.115, Steel);
        parts.Add("BikeEngine", engine);

        // Faceted wedge fuel tank: wide front, narrow rear.
        Mesh tank = new Mesh();
        double[] tz = new double[] { -0.66, -0.50, -0.32, -0.16, -0.06 };
        double[] tw = new double[] { 0.14, 0.175, 0.17, 0.125, 0.08 };
        double[] tyt = new double[] { 0.965, 0.99, 0.98, 0.94, 0.90 };
        double[] tyb = new double[] { 0.72, 0.72, 0.73, 0.75, 0.77 };
        List<V3[]> tankSections = new List<V3[]>();
        for (int i = 0; i < tz.Length; i++)
        {
            double w = tw[i], yt = tyt[i], yb = tyb[i], ym = (yt + yb) * 0.5;
            tankSections.Add(new V3[] {
                V(-w, ym, tz[i]), V(-w * 0.65, yt, tz[i]), V(w * 0.65, yt, tz[i]),
                V(w, ym, tz[i]), V(w * 0.65, yb, tz[i]), V(-w * 0.65, yb, tz[i])
            });
        }
        Geo.Loft(tank, tankSections, false, true, true, OrangeDecal);
        parts.Add("BikeFuelTank", tank);

        // Continuous two-rider seat, slightly rising tail.
        Mesh seat = new Mesh();
        double[] sz = new double[] { -0.10, 0.08, 0.30, 0.52, 0.74 };
        double[] sw = new double[] { 0.08, 0.105, 0.12, 0.125, 0.105 };
        double[] syt = new double[] { 0.87, 0.86, 0.86, 0.895, 0.925 };
        double[] syb = new double[] { 0.74, 0.73, 0.73, 0.75, 0.78 };
        List<V3[]> seatSections = new List<V3[]>();
        for (int i = 0; i < sz.Length; i++)
        {
            double w = sw[i], yt = syt[i], yb = syb[i], ym = (yt + yb) * 0.5;
            seatSections.Add(new V3[] {
                V(-w, ym, sz[i]), V(-w * 0.7, yt, sz[i]), V(w * 0.7, yt, sz[i]),
                V(w, ym, sz[i]), V(w * 0.7, yb, sz[i]), V(-w * 0.7, yb, sz[i])
            });
        }
        Geo.Loft(seat, seatSections, false, true, true, Seat);
        parts.Add("BikeSeat", seat);

        // Side covers.
        Mesh panels = new Mesh();
        Geo.BoxA(panels, V(-0.125, 0.74, 0.34), 0.012, 0.07, 0.15, Panel);
        Geo.BoxA(panels, V(0.125, 0.74, 0.34), 0.012, 0.07, 0.15, Panel);
        parts.Add("BikeSidePanels", panels);

        // Exhaust: header along right side, upswept muffler.
        Mesh exhaust = new Mesh();
        Geo.Tube(exhaust, new V3[] { V(0.10, 0.58, -0.10), V(0.15, 0.46, 0.02), V(0.17, 0.42, 0.30), V(0.17, 0.44, 0.62) }, 0.030, 8, true, Steel);
        Geo.Frustum(exhaust, V(0.17, 0.45, 0.60), V(0.20, 0.56, 1.02), 0.055, 0.048, 10, true, true, Chrome);
        Geo.Frustum(exhaust, V(0.20, 0.56, 1.02), V(0.21, 0.60, 1.12), 0.030, 0.030, 8, true, true, Steel);
        parts.Add("BikeExhaust", exhaust);

        // Swingarm + pivot + twin shocks with orange springs.
        Mesh swing = new Mesh();
        double[] ax = new double[] { -0.105, 0.105 };
        for (int i = 0; i < 2; i++)
        {
            Geo.Tube(swing, new V3[] { V(ax[i], 0.42, 0.42), V(ax[i] + (ax[i] > 0 ? 0.01 : -0.01), 0.38, 0.66), V(ax[i] + (ax[i] > 0 ? 0.01 : -0.01), rcy, rz) }, 0.026, 6, true, Carbon);
        }
        Geo.BoxA(swing, V(0, 0.40, 0.50), 0.10, 0.020, 0.020, Carbon);
        Geo.Frustum(swing, V(-0.13, 0.42, 0.42), V(0.13, 0.42, 0.42), 0.030, 0.030, 8, true, true, Steel);
        double[] shx = new double[] { -0.08, 0.08 };
        for (int i = 0; i < 2; i++)
        {
            V3 top = V(shx[i], 0.74, 0.52);
            V3 bot = V(shx[i] + (shx[i] > 0 ? 0.02 : -0.02), 0.46, 0.78);
            Geo.Frustum(swing, bot, top, 0.020, 0.016, 8, true, true, Steel);
            V3 axis = (top - bot).Norm();
            for (int k = 1; k <= 3; k++)
            {
                V3 c = bot + (top - bot) * (k / 4.0);
                Geo.Torus(swing, c, axis, 0.034, 0.010, 8, 5, Orange);
            }
        }
        parts.Add("BikeSwingarm", swing);

        // Fenders: short front arc, rear mud guard, tail plate + light.
        Mesh fender = new Mesh();
        Geo.ArcPlate(fender, V(0, fcy, fz), V(1, 0, 0), V(0, 1, 0), V(0, 0, -1), 0.40, 0.43, -0.55, 1.00, 8, 0.22, Orange);
        Geo.ArcPlate(fender, V(0, rcy, rz), V(1, 0, 0), V(0, 1, 0), V(0, 0, 1), 0.41, 0.435, -0.35, 0.80, 7, 0.20, Orange);
        Geo.BoxA(fender, V(0, 0.88, 1.02), 0.11, 0.015, 0.16, Orange);
        Geo.BoxA(fender, V(0, 0.86, 1.20), 0.06, 0.040, 0.015, Red);
        parts.Add("BikeFenders", fender);

        return parts;
    }
}
'@

Add-Type -TypeDefinition $geometryCore -Language CSharp

$parts = [X1Builder]::Build()

function Format-Number([double]$value) {
    return $value.ToString('0.######', $invariant)
}

$geometryBuilder = New-Object System.Text.StringBuilder
$instanceBuilder = New-Object System.Text.StringBuilder
$totalTriangles = 0
$minX = [double]::MaxValue; $maxX = [double]::MinValue
$minY = [double]::MaxValue; $maxY = [double]::MinValue
$minZ = [double]::MaxValue; $maxZ = [double]::MinValue

$partOrder = @(
    'BikeFrontWheel', 'BikeRearWheel', 'BikeFrontFork', 'BikeHandlebar', 'BikeHeadlight',
    'BikeFrame', 'BikeEngine', 'BikeFuelTank', 'BikeSeat', 'BikeSidePanels',
    'BikeExhaust', 'BikeSwingarm', 'BikeFenders'
)
if ($partOrder.Count -ne $parts.Count) { throw "Part order list covers $($partOrder.Count) names but builder returned $($parts.Count) parts." }

foreach ($name in $partOrder) {
    if (-not $parts.ContainsKey($name)) { throw "Builder did not produce part $name." }
    $mesh = $parts[$name]
    $totalTriangles += $mesh.TriangleCount
    for ($i = 0; $i -lt $mesh.Positions.Count; $i += 3) {
        $px = $mesh.Positions[$i]; $py = $mesh.Positions[$i + 1]; $pz = $mesh.Positions[$i + 2]
        if ($px -lt $minX) { $minX = $px }; if ($px -gt $maxX) { $maxX = $px }
        if ($py -lt $minY) { $minY = $py }; if ($py -gt $maxY) { $maxY = $py }
        if ($pz -lt $minZ) { $minZ = $pz }; if ($pz -gt $maxZ) { $maxZ = $pz }
    }
    $posText = New-Object System.Text.StringBuilder
    $nrmText = New-Object System.Text.StringBuilder
    $uvText = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $mesh.Positions.Count; $i += 3) {
        [void]$posText.Append((Format-Number $mesh.Positions[$i])); [void]$posText.Append(' ')
        [void]$posText.Append((Format-Number $mesh.Positions[$i + 1])); [void]$posText.Append(' ')
        [void]$posText.Append((Format-Number $mesh.Positions[$i + 2])); [void]$posText.Append(' ')
        [void]$nrmText.Append((Format-Number $mesh.Normals[$i])); [void]$nrmText.Append(' ')
        [void]$nrmText.Append((Format-Number $mesh.Normals[$i + 1])); [void]$nrmText.Append(' ')
        [void]$nrmText.Append((Format-Number $mesh.Normals[$i + 2])); [void]$nrmText.Append(' ')
    }
    for ($i = 0; $i -lt $mesh.Uvs.Count; $i += 2) {
        [void]$uvText.Append((Format-Number $mesh.Uvs[$i])); [void]$uvText.Append(' ')
        [void]$uvText.Append((Format-Number $mesh.Uvs[$i + 1])); [void]$uvText.Append(' ')
    }
    $idxText = New-Object System.Text.StringBuilder
    foreach ($index in $mesh.Indices) {
        [void]$idxText.Append($index); [void]$idxText.Append(' ')
        [void]$idxText.Append($index); [void]$idxText.Append(' ')
        [void]$idxText.Append($index); [void]$idxText.Append(' ')
    }
    $vertexCount = $mesh.VertexCount
    $triangleCount = $mesh.TriangleCount
    [void]$geometryBuilder.Append(@"
    <geometry id="$name-mesh" name="$name">
      <mesh>
        <source id="$name-positions"><float_array id="$name-positions-array" count="$($vertexCount * 3)">$posText</float_array><technique_common><accessor source="#$name-positions-array" count="$vertexCount" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
        <source id="$name-normals"><float_array id="$name-normals-array" count="$($vertexCount * 3)">$nrmText</float_array><technique_common><accessor source="#$name-normals-array" count="$vertexCount" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
        <source id="$name-uv"><float_array id="$name-uv-array" count="$($vertexCount * 2)">$uvText</float_array><technique_common><accessor source="#$name-uv-array" count="$vertexCount" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
        <vertices id="$name-vertices"><input semantic="POSITION" source="#$name-positions"/></vertices>
        <triangles count="$triangleCount"><input semantic="VERTEX" source="#$name-vertices" offset="0"/><input semantic="NORMAL" source="#$name-normals" offset="1"/><input semantic="TEXCOORD" source="#$name-uv" offset="2" set="0"/><p>$idxText</p></triangles>
      </mesh>
    </geometry>

"@)
    [void]$instanceBuilder.Append("        <instance_geometry url=`"#$name-mesh`" name=`"$name`"/>`n")
    Write-Host ("part {0}: {1} verts, {2} tris" -f $name, $vertexCount, $triangleCount)
}

$document = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Wilderness Explorer X1 low-poly adventure motorcycle; baked Y_UP, one render bone. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-AtvAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
$geometryBuilder  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
      <node id="ATV" name="ATV" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$instanceBuilder      </node>
  </visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
[IO.File]::WriteAllText($modelPath, $document, [Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 64, 64, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([Drawing.Color]::FromArgb(255, 70, 74, 78))
    $brushes = @{}
    function Set-CellPixel($x, $y, $r, $g, $b) {
        $bitmap.SetPixel($x, $y, [Drawing.Color]::FromArgb(255, $r, $g, $b))
    }
    function Fill-Region($col, $row, $r, $g, $b) {
        for ($x = $col * 16; $x -lt $col * 16 + 16; $x++) {
            for ($y = $row * 16; $y -lt $row * 16 + 16; $y++) { Set-CellPixel $x $y $r $g $b }
        }
    }
    # 0,0 orange body with light/shade bands
    Fill-Region 0 0 224 116 26
    for ($x = 0; $x -lt 16; $x++) {
        for ($y = 0; $y -lt 4; $y++) { Set-CellPixel $x $y 246 152 58 }
        for ($y = 12; $y -lt 16; $y++) { Set-CellPixel $x $y 176 84 16 }
    }
    # 1,0 orange decal with silver stripe
    Fill-Region 1 0 224 116 26
    for ($x = 16; $x -lt 32; $x++) {
        for ($y = 0; $y -lt 4; $y++) { Set-CellPixel $x $y 246 152 58 }
        for ($y = 6; $y -lt 9; $y++) { Set-CellPixel $x $y 208 214 218 }
        for ($y = 12; $y -lt 16; $y++) { Set-CellPixel $x $y 176 84 16 }
    }
    # 2,0 carbon frame
    Fill-Region 2 0 35 37 36
    for ($x = 32; $x -lt 48; $x += 2) {
        for ($y = 32; $y -lt 48; $y++) { Set-CellPixel $x $y 45 47 46 }
    }
    # 3,0 rubber with tread bands
    Fill-Region 3 0 26 26 28
    for ($x = 48; $x -lt 64; $x += 4) {
        for ($i = 0; $i -lt 2; $i++) {
            for ($y = 0; $y -lt 16; $y++) { Set-CellPixel ($x + $i) $y 42 42 46 }
        }
    }
    # 0,1 brushed silver
    Fill-Region 0 1 154 160 164
    for ($y = 16; $y -lt 32; $y += 3) {
        for ($x = 0; $x -lt 16; $x++) { Set-CellPixel $x $y 176 182 186 }
    }
    # 1,1 dark steel
    Fill-Region 1 1 90 94 98
    for ($y = 16; $y -lt 32; $y += 4) {
        for ($x = 16; $x -lt 32; $x++) { Set-CellPixel $x $y 106 110 114 }
    }
    # 2,1 seat grip
    Fill-Region 2 1 52 47 42
    for ($x = 34; $x -lt 48; $x += 3) {
        for ($y = 18; $y -lt 32; $y += 3) { Set-CellPixel $x $y 74 68 60 }
    }
    # 3,1 chrome bands
    for ($x = 48; $x -lt 64; $x++) {
        for ($y = 16; $y -lt 32; $y++) {
            $band = [int](($x - 48) / 4) % 3
            if ($band -eq 0) { Set-CellPixel $x $y 184 190 194 }
            elseif ($band -eq 1) { Set-CellPixel $x $y 214 220 224 }
            else { Set-CellPixel $x $y 140 146 150 }
        }
    }
    # 0,2 headlight lens
    Fill-Region 0 2 60 60 62
    for ($x = 4; $x -lt 12; $x++) {
        for ($y = 36; $y -lt 44; $y++) { Set-CellPixel $x $y 250 240 210 }
    }
    for ($x = 2; $x -lt 14; $x++) {
        for ($y = 34; $y -lt 46; $y++) {
            if ($x -eq 2 -or $x -eq 13 -or $y -eq 34 -or $y -eq 45) { Set-CellPixel $x $y 230 200 140 }
        }
    }
    # 1,2 tail red
    Fill-Region 1 2 194 42 32
    for ($x = 20; $x -lt 28; $x++) {
        for ($y = 36; $y -lt 44; $y++) { Set-CellPixel $x $y 232 74 58 }
    }
    # 2,2 amber
    Fill-Region 2 2 232 160 32
    for ($x = 36; $x -lt 44; $x++) {
        for ($y = 36; $y -lt 44; $y++) { Set-CellPixel $x $y 250 190 80 }
    }
    # 3,2 fork silver dark
    Fill-Region 3 2 126 132 136
    for ($y = 48; $y -lt 64; $y += 4) {
        for ($x = 48; $x -lt 64; $x++) { Set-CellPixel $x $y 146 152 156 }
    }
    # 0,3 grip rubber rings
    Fill-Region 0 3 46 46 48
    for ($x = 0; $x -lt 16; $x += 4) {
        for ($y = 48; $y -lt 64; $y++) { Set-CellPixel $x $y 62 62 64 }
    }
    # 1,3 rim with spoke cross
    Fill-Region 1 3 168 174 178
    for ($i = 0; $i -lt 16; $i++) {
        Set-CellPixel (16 + $i) (48 + [int]($i / 2)) 120 126 130
        Set-CellPixel (16 + $i) (63 - [int]($i / 2)) 120 126 130
    }
    # 2,3 dark orange panel
    Fill-Region 2 3 186 92 22
    for ($x = 32; $x -lt 48; $x++) {
        for ($y = 48; $y -lt 52; $y++) { Set-CellPixel $x $y 210 118 40 }
        for ($y = 60; $y -lt 64; $y++) { Set-CellPixel $x $y 148 68 12 }
    }
    $graphics.Dispose()
    $bitmap.Save($texturePath, [Drawing.Imaging.ImageFormat]::Png)
}
finally { $bitmap.Dispose() }

Write-Host ("Generated: {0}" -f $modelPath)
Write-Host ("Generated: {0}" -f $texturePath)
Write-Host ("parts={0} triangles={1}" -f $parts.Count, $totalTriangles)
Write-Host ("bounds X [{0}, {1}] Y [{2}, {3}] Z [{4}, {5}]" -f `
    (Format-Number $minX), (Format-Number $maxX), (Format-Number $minY), (Format-Number $maxY), (Format-Number $minZ), (Format-Number $maxZ))
