namespace SCIEPort.Features.Architecture
{
    /// <summary>Original, single-cell architectural parts. No inventories or companion cells.</summary>
    internal static class ArchitectureRules
    {
        internal const int Roof = 1014, Timber = 1015, Window = 1016, Lantern = 1017, Tea = 1018, Garden = 1019, Telescope = 1020;
        internal static readonly string[] Types = { "AncientRoofBlock", "AncientTimberBlock", "AncientWindowBlock", "AncientLanternBlock", "AncientTeaBlock", "AncientGardenBlock", "AncientTelescopeBlock" };
        private static readonly string[][] Names = {
            new[] { "叠瓦坡面", "屋脊", "平檐", "翘檐", "转角檐", "封边坡面" },
            new[] { "木柱", "横梁", "檐下托木", "木斜撑", "木栏" },
            new[] { "格栅玻璃窗", "木格窗", "月窗左上", "月窗右上", "月窗左下", "月窗右下" },
            new[] { "吊灯笼", "壁灯笼", "座灯笼" },
            new[] { "茶桌", "坐垫", "茶具", "短帘" },
            new[] { "樱花枝", "垂藤", "盆竹", "花盆" },
            new[] { "望远镜" }
        };
        internal static bool Owns(int id) => id >= Roof && id <= Telescope;
        internal static int Id(string type)
        {
            for (int i = 0; i < Types.Length; i++) if (Types[i] == type) return Roof + i;
            return -1;
        }
        internal static int Variants(int id) => Owns(id) ? Names[id - Roof].Length : 0;
        internal static int Variant(int data) => (data >> 3) & 7;
        internal static bool Valid(int id, int data) => Owns(id) && data >= 0 && data < 64 && Variant(data) < Variants(id);
        internal static int Normalize(int id, int data) => Valid(id, data) ? data : 0;
        internal static string Name(int id, int data) => Names[id - Roof][Variant(Normalize(id, data))];
        // Rotation follows native positive Y rotation; blueprint rotation is its inverse.
        internal static int Rotate(int data, int steps) => (data & ~3) | (((data & 3) - steps % 4 + 4) & 3);
        internal static int Mirror(int data) => ((data & ~3) | ((-data) & 3)) ^ 4;
    }
}
