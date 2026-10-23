[CmdletBinding()]
param(
    [string]$ProjectRoot = $(if ($PSScriptRoot) { Split-Path -Parent $PSScriptRoot } else { '.' }),
    [string]$OutDir = ''
)

# Offline orthographic preview renderer for the ATV/X1 DAE.
# Produces white-model and textured sheets from six directions so model shape
# can be reviewed without launching the game. Painter's algorithm with
# backface culling; good enough for acceptance previews, not a game render.

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrEmpty($OutDir)) {
    $OutDir = Join-Path $ProjectRoot 'artifacts\previews\atv-x1'
}
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\ATV.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\ATV.png'
if (-not (Test-Path $OutDir)) { [void][IO.Directory]::CreateDirectory($OutDir) }

$rendererSource = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Xml;

public static class AtvPreview
{
    private class Tri
    {
        public double[] P = new double[9];
        public double[] N = new double[9];
        public double[] T = new double[6];
    }

    private static List<Tri> LoadTriangles(string daePath)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(daePath);
        List<Tri> tris = new List<Tri>();
        XmlNodeList geometries = doc.SelectNodes("//*[local-name()='geometry']");
        foreach (XmlNode geometry in geometries)
        {
            double[] pos = ReadFloats(geometry, "positions-array");
            double[] nrm = ReadFloats(geometry, "normals-array");
            double[] uv = ReadFloats(geometry, "uv-array");
            XmlNode p = geometry.SelectSingleNode(".//*[local-name()='triangles']/*[local-name()='p']");
            string[] tokens = p.InnerText.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 8 < tokens.Length; i += 9)
            {
                Tri t = new Tri();
                for (int k = 0; k < 3; k++)
                {
                    int vi = int.Parse(tokens[i + k * 3], CultureInfo.InvariantCulture);
                    int ni = int.Parse(tokens[i + k * 3 + 1], CultureInfo.InvariantCulture);
                    int ti = int.Parse(tokens[i + k * 3 + 2], CultureInfo.InvariantCulture);
                    t.P[k * 3] = pos[vi * 3]; t.P[k * 3 + 1] = pos[vi * 3 + 1]; t.P[k * 3 + 2] = pos[vi * 3 + 2];
                    t.N[k * 3] = nrm[ni * 3]; t.N[k * 3 + 1] = nrm[ni * 3 + 1]; t.N[k * 3 + 2] = nrm[ni * 3 + 2];
                    t.T[k * 2] = uv[ti * 2]; t.T[k * 2 + 1] = uv[ti * 2 + 1];
                }
                tris.Add(t);
            }
        }
        return tris;
    }

    private static double[] ReadFloats(XmlNode geometry, string idSuffix)
    {
        XmlNode node = geometry.SelectSingleNode(".//*[local-name()='float_array'][contains(@id,'" + idSuffix + "')]");
        string[] tokens = node.InnerText.Split(new char[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        double[] values = new double[tokens.Length];
        for (int i = 0; i < tokens.Length; i++) { values[i] = double.Parse(tokens[i], CultureInfo.InvariantCulture); }
        return values;
    }

    private static double Dot3(double ax, double ay, double az, double bx, double by, double bz)
    {
        return ax * bx + ay * by + az * bz;
    }

    public static void RenderAll(string daePath, string texturePath, string outDir, int width, int height)
    {
        List<Tri> tris = LoadTriangles(daePath);
        Bitmap texture = new Bitmap(texturePath);
        int texW = texture.Width;
        int texH = texture.Height;
        int[] texels = new int[texW * texH];
        BitmapData texData = texture.LockBits(new Rectangle(0, 0, texW, texH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] texBuf = new byte[Math.Abs(texData.Stride) * texH];
        System.Runtime.InteropServices.Marshal.Copy(texData.Scan0, texBuf, 0, texBuf.Length);
        texture.UnlockBits(texData);
        for (int y = 0; y < texH; y++)
        {
            for (int x = 0; x < texW; x++)
            {
                int idx = y * Math.Abs(texData.Stride) + x * 4;
                texels[y * texW + x] = (texBuf[idx + 2] << 16) | (texBuf[idx + 1] << 8) | texBuf[idx];
            }
        }
        texture.Dispose();
        double[][] views = new double[][] {
            new double[] { 0, 0, 1, 0, 1, 0 },    // front (camera at -Z)
            new double[] { 0, 0, -1, 0, 1, 0 },   // rear
            new double[] { 1, 0, 0, 0, 1, 0 },    // left side (camera at -X)
            new double[] { -1, 0, 0, 0, 1, 0 },   // right side
            new double[] { 0.7071, 0, 0.7071, 0, 1, 0 },   // front-left 45
            new double[] { -0.7071, 0, -0.7071, 0, 1, 0 }, // rear-right 45
            new double[] { 0, -1, 0, 0, 0, 1 }    // top
        };
        string[] names = new string[] { "front", "rear", "left", "right", "front45", "rear45", "top" };
        for (int v = 0; v < views.Length; v++)
        {
            RenderView(tris, texels, texW, texH, views[v], names[v], outDir, width, height, false);
            RenderView(tris, texels, texW, texH, views[v], names[v], outDir, width, height, true);
        }
    }

    private static void RenderView(List<Tri> tris, int[] texels, int texW, int texH, double[] view, string name, string outDir, int width, int height, bool white)
    {
        double dx = view[0], dy = view[1], dz = view[2];
        double ux = view[3], uy = view[4], uz = view[5];
        // right = cross(dir, up)
        double rx = dy * uz - dz * uy;
        double ry = dz * ux - dx * uz;
        double rz = dx * uy - dy * ux;
        double rl = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        rx /= rl; ry /= rl; rz /= rl;

        double minSX = double.MaxValue, maxSX = double.MinValue;
        double minSY = double.MaxValue, maxSY = double.MinValue;
        for (int i = 0; i < tris.Count; i++)
        {
            double[] p = tris[i].P;
            for (int k = 0; k < 3; k++)
            {
                double sx = Dot3(p[k * 3], p[k * 3 + 1], p[k * 3 + 2], rx, ry, rz);
                double sy = Dot3(p[k * 3], p[k * 3 + 1], p[k * 3 + 2], ux, uy, uz);
                if (sx < minSX) minSX = sx;
                if (sx > maxSX) maxSX = sx;
                if (sy < minSY) minSY = sy;
                if (sy > maxSY) maxSY = sy;
            }
        }
        double scale = Math.Min((width - 80) / (maxSX - minSX), (height - 80) / (maxSY - minSY));
        double cx = (minSX + maxSX) * 0.5;
        double cy = (minSY + maxSY) * 0.5;

        double lx = -0.45, ly = 0.80, lz = -0.45;
        double ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
        lx /= ll; ly /= ll; lz /= ll;

        List<object[]> order = new List<object[]>();
        for (int i = 0; i < tris.Count; i++)
        {
            Tri t = tris[i];
            double nx = (t.N[0] + t.N[3] + t.N[6]) / 3.0;
            double ny = (t.N[1] + t.N[4] + t.N[7]) / 3.0;
            double nz = (t.N[2] + t.N[5] + t.N[8]) / 3.0;
            if (Dot3(nx, ny, nz, dx, dy, dz) >= 0) { continue; } // backface
            double depth = (Dot3(t.P[0], t.P[1], t.P[2], dx, dy, dz)
                + Dot3(t.P[3], t.P[4], t.P[5], dx, dy, dz)
                + Dot3(t.P[6], t.P[7], t.P[8], dx, dy, dz)) / 3.0;
            order.Add(new object[] { depth, t, nx, ny, nz });
        }
        order.Sort(delegate(object[] a, object[] b) { return ((double)b[0]).CompareTo((double)a[0]); });

        Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] buffer = new byte[Math.Abs(data.Stride) * height];
        Color bgColor = white ? Color.FromArgb(255, 236, 236, 238) : Color.FromArgb(255, 52, 56, 60);
        for (int i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = bgColor.B; buffer[i + 1] = bgColor.G; buffer[i + 2] = bgColor.R; buffer[i + 3] = 255;
        }
        foreach (object[] item in order)
        {
            Tri t = (Tri)item[1];
            double nx = (double)item[2], ny = (double)item[3], nz = (double)item[4];
            double[] sx = new double[3];
            double[] sy = new double[3];
            for (int k = 0; k < 3; k++)
            {
                double px = Dot3(t.P[k * 3], t.P[k * 3 + 1], t.P[k * 3 + 2], rx, ry, rz);
                double py = Dot3(t.P[k * 3], t.P[k * 3 + 1], t.P[k * 3 + 2], ux, uy, uz);
                sx[k] = width * 0.5 + (px - cx) * scale;
                sy[k] = height * 0.5 - (py - cy) * scale;
            }
            double[] uv = new double[] { t.T[0], t.T[1], t.T[2], t.T[3], t.T[4], t.T[5] };
            double area = (sx[1] - sx[0]) * (sy[2] - sy[0]) - (sx[2] - sx[0]) * (sy[1] - sy[0]);
            if (area < 0)
            {
                double tmp = sx[1]; sx[1] = sx[2]; sx[2] = tmp;
                tmp = sy[1]; sy[1] = sy[2]; sy[2] = tmp;
                tmp = uv[2]; uv[2] = uv[4]; uv[4] = tmp;
                tmp = uv[3]; uv[3] = uv[5]; uv[5] = tmp;
                area = -area;
            }
            if (area < 1e-9) { continue; }
            int x0 = (int)Math.Floor(Math.Min(sx[0], Math.Min(sx[1], sx[2])));
            int x1 = (int)Math.Ceiling(Math.Max(sx[0], Math.Max(sx[1], sx[2])));
            int y0 = (int)Math.Floor(Math.Min(sy[0], Math.Min(sy[1], sy[2])));
            int y1 = (int)Math.Ceiling(Math.Max(sy[0], Math.Max(sy[1], sy[2])));
            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 >= width) x1 = width - 1;
            if (y1 >= height) y1 = height - 1;
            for (int y = y0; y <= y1; y++)
            {
                double py = y + 0.5;
                int row = y * Math.Abs(data.Stride);
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5;
                    double w0 = ((sx[1] - px) * (sy[2] - py) - (sx[2] - px) * (sy[1] - py)) / area;
                    double w1 = ((sx[2] - px) * (sy[0] - py) - (sx[0] - px) * (sy[2] - py)) / area;
                    double w2 = 1.0 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) { continue; }
                    double u = uv[0] * w0 + uv[2] * w1 + uv[4] * w2;
                    double vv = uv[1] * w0 + uv[3] * w1 + uv[5] * w2;
                    int cr, cg, cb;
                    if (white)
                    {
                        cr = 226; cg = 226; cb = 230;
                    }
                    else
                    {
                        int tx = (int)Math.Floor(u * texW);
                        int ty = (int)Math.Floor(vv * texH);
                        if (tx < 0) tx = 0;
                        if (tx >= texW) tx = texW - 1;
                        if (ty < 0) ty = 0;
                        if (ty >= texH) ty = texH - 1;
                        int tex = texels[ty * texW + tx];
                        cr = (tex >> 16) & 255; cg = (tex >> 8) & 255; cb = tex & 255;
                    }
                    double shade = 0.42 + 0.58 * Math.Max(0.0, Dot3(nx, ny, nz, lx, ly, lz));
                    int idx = row + x * 4;
                    buffer[idx] = (byte)Clamp(cb * shade);
                    buffer[idx + 1] = (byte)Clamp(cg * shade);
                    buffer[idx + 2] = (byte)Clamp(cr * shade);
                    buffer[idx + 3] = 255;
                }
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        bmp.UnlockBits(data);
        string suffix = white ? "white" : "color";
        bmp.Save(System.IO.Path.Combine(outDir, name + "_" + suffix + ".png"), ImageFormat.Png);
        bmp.Dispose();
    }

    private static int Clamp(double v)
    {
        int i = (int)Math.Round(v);
        if (i < 0) i = 0;
        if (i > 255) i = 255;
        return i;
    }
}
'@

Add-Type -TypeDefinition $rendererSource -ReferencedAssemblies System.Drawing, System.Xml

[AtvPreview]::RenderAll($modelPath, $texturePath, $OutDir, 1000, 750)
Write-Host ("Previews written to {0}" -f $OutDir)
