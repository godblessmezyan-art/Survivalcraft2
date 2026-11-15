namespace SCIEPort.Features.Creatures
{
    /// <summary>Policy shared by the native adapter and offline tests.</summary>
    internal static class WildlifeRules
    {
        internal static readonly string[] Names = { "SCIEPortFox", "SCIEPortRabbit", "SCIEPortChicken", "SCIEPortCat" };
        internal static readonly int[] Caps = { 16, 40, 32, 16 };
        internal static readonly int[] GroupMax = { 1, 2, 3, 1 };
        internal static readonly float[] Weights = { .14f, .45f, .35f, .12f };

        internal static bool Habitat(int species, int temperature, int humidity, int height, bool naturalGround, bool open)
        {
            if (species < 0 || species > 3 || !naturalGround || !open || height < 2 || height > 120
                || temperature < 0 || temperature > 15 || humidity < 0 || humidity > 15) return false;
            switch (species)
            {
                case 0: return temperature >= 3 && temperature <= 12 && humidity >= 6;
                case 1: return temperature >= 3 && temperature <= 13 && humidity >= 3 && humidity <= 12;
                case 2: return temperature >= 6 && humidity >= 5 && height <= 100;
                default: return temperature >= 5 && temperature <= 13 && humidity >= 4 && humidity <= 12 && height <= 100;
            }
        }

        internal static int SpawnCount(int species, int alive, int frameSpawns, int requested)
        {
            if (species < 0 || species > 3 || alive < 0 || frameSpawns < 0 || requested < 1) return 0;
            int available = System.Math.Min(Caps[species] - alive, 2 - frameSpawns);
            return System.Math.Max(0, System.Math.Min(available, System.Math.Min(requested, GroupMax[species])));
        }
    }
}
