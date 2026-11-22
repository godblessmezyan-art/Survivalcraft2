# Generates the deterministic 512x512 Scenery texture atlas (16x16 grid, 32x32 tiles).
# Run from SCIE-Port: powershell -ExecutionPolicy Bypass -File scripts/New-SceneryAssets.ps1
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\SCIEPort\Assets\Textures\Scenery.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class SceneryAtlasGenerator
{
    private const int AtlasSize = 512;
    private const int TileSize = 32;

    private static uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)(x + 4096)) * 16777619u;
            h = (h ^ (uint)(y + 4096)) * 16777619u;
            h = (h ^ (uint)salt) * 16777619u;
            h ^= h >> 15;
            return h;
        }
    }

    private static int N(int x, int y, int salt)
    {
        return (int)(Hash(x, y, salt) % 1000u);
    }

    private static int Clamp(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return value;
    }

    private static void Set(Bitmap bitmap, int slot, int px, int py, int r, int g, int b)
    {
        int tileX = slot % 16;
        int tileY = slot / 16;
        bitmap.SetPixel(tileX * TileSize + px, tileY * TileSize + py,
            Color.FromArgb(255, Clamp(r), Clamp(g), Clamp(b)));
    }

    public static void Generate(string path)
    {
        using (Bitmap bitmap = new Bitmap(AtlasSize, AtlasSize, PixelFormat.Format24bppRgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.FromArgb(255, 24, 24, 24));
            }

            // Slot 0: weathered ship hull side.
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    int n = N(x, y, 11);
                    int plank = y / 8;
                    int r = 82 - plank * 3 + (n % 19) - 9;
                    int g = 55 - plank * 2 + (n % 13) - 6;
                    int b = 34 + n % 9;
                    if (y % 8 == 0) { r = 40; g = 27; b = 17; }
                    if ((x + plank * 9) % 17 == 0 && y % 8 != 0) { r = 48; g = 33; b = 20; }
                    if (x % 11 == 3 && y % 8 == 4) { r = 130; g = 104; b = 64; }
                    if (n < 90) { r = 48 + n % 20; g = 84 + n % 24; b = 40 + n % 12; }
                    Set(bitmap, 0, x, y, r, g, b);
                }
            }

            // Slot 1: hull deck, lighter planks running lengthwise.
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    int n = N(x, y, 23);
                    int plank = x / 8;
                    int r = 132 - plank * 4 + (n % 17) - 8;
                    int g = 99 - plank * 3 + (n % 13) - 6;
                    int b = 62 + n % 11;
                    if (x % 8 == 0) { r = 74; g = 53; b = 33; }
                    if ((y + plank * 7) % 16 == 0 && x % 8 != 0) { r = 86; g = 63; b = 39; }
                    Set(bitmap, 1, x, y, r, g, b);
                }
            }

            // Slots 2-4: envelope canvas in cream, teal and crimson.
            int[][] canvas = new int[][]
            {
                new int[] { 226, 214, 186 },
                new int[] { 62, 122, 138 },
                new int[] { 142, 54, 58 }
            };
            for (int slot = 2; slot <= 4; slot++)
            {
                int[] basis = canvas[slot - 2];
                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        int n = N(x, y, 40 + slot);
                        int weave = ((x + y) % 4) < 2 ? 10 : -7;
                        int r = basis[0] + weave + (n % 11) - 5;
                        int g = basis[1] + weave + (n % 9) - 4;
                        int b = basis[2] + weave + (n % 9) - 4;
                        if (x % 16 == 0) { r -= 32; g -= 32; b -= 30; }
                        if (y % 16 == 0) { r -= 18; g -= 18; b -= 18; }
                        Set(bitmap, slot, x, y, r, g, b);
                    }
                }
            }

            // Slot 5: cloudstone.
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    int low = N(x / 4, y / 4, 61);
                    int n = N(x, y, 62);
                    int r = 206 + low % 22 - 11;
                    int g = 214 + low % 18 - 9;
                    int b = 224 + low % 16 - 8;
                    if (n < 45) { r -= 18; g -= 16; b -= 12; }
                    if (n > 960) { r += 10; g += 10; b += 8; }
                    Set(bitmap, 5, x, y, r, g, b);
                }
            }

            // Slots 6-7: crystal facets, cyan and violet.
            int[][] crystals = new int[][]
            {
                new int[] { 44, 196, 214 },
                new int[] { 158, 104, 224 }
            };
            for (int slot = 6; slot <= 7; slot++)
            {
                int[] basis = crystals[slot - 6];
                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        int facetX = x / 8;
                        int facetY = y / 8;
                        int n = N(facetX, facetY, 70 + slot);
                        bool edge = x % 8 == 0 || y % 8 == 0;
                        double factor = 0.55 + (n % 55) / 100.0;
                        if (edge) factor = 1.28;
                        if (x % 8 == 1) factor += 0.3;
                        Set(bitmap, slot, x, y,
                            (int)(basis[0] * factor),
                            (int)(basis[1] * factor),
                            (int)(basis[2] * factor));
                    }
                }
            }

            // Slot 8: brass fittings.
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    int n = N(x, y, 88);
                    int r = 172 + n % 28 - 14;
                    int g = 132 + n % 22 - 11;
                    int b = 52 + n % 14;
                    if (y % 9 == 0) { r -= 42; g -= 34; b -= 18; }
                    if (x % 10 == 4) { r += 34; g += 28; b += 12; }
                    if (x % 16 == 8 && y % 16 == 8) { r = 246; g = 224; b = 138; }
                    Set(bitmap, 8, x, y, r, g, b);
                }
            }

            // Slot 9: torn sail cloth with a patch and holes.
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    int n = N(x, y, 99);
                    int r = 214 + n % 15 - 7;
                    int g = 202 + n % 13 - 6;
                    int b = 172 + n % 13 - 6;
                    if ((x + y) % 5 == 0) { r -= 14; g -= 12; b -= 10; }
                    if (x >= 6 && x <= 17 && y >= 6 && y <= 17) { r = 166; g = 142; b = 106; }
                    if ((x == 6 || x == 17) && y >= 6 && y <= 17) { r = 108; g = 88; b = 62; }
                    if (n < 55 && (x > 20 || y > 21)) { r = 54; g = 48; b = 42; }
                    Set(bitmap, 9, x, y, r, g, b);
                }
            }

            string directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
[SceneryAtlasGenerator]::Generate($OutputPath)
Write-Host "Wrote scenery atlas: $OutputPath"
