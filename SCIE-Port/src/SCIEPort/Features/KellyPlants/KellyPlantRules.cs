namespace SCIEPort.Features.KellyPlants
{
    /// <summary>
    /// Pure rules shared by Kelly plant runtime code and offline tests.
    /// They intentionally have no dependency on the game assemblies.
    /// </summary>
    public static class KellyPlantRules
    {
        /// <summary>Perennial berry stages. Cloudberry keeps legacy DATA0 ripe; wildberry keeps bit0 ripe.</summary>
        public static int NextBerry(int data,bool cloudberry,bool fruitSeason,bool suitable,bool step)
        {
            if(!fruitSeason) return cloudberry?(data&~3)|1:data&~15;
            if(!suitable||!step) return data;
            if(cloudberry)
            {
                int stage=data&3;
                return stage==0?data:(data&~3)|((stage+1)&3);
            }
            if((data&1)!=0) return data;
            int age=(data>>1)&7;
            return (data&~15)|(age>=3?1:(age+1)<<1);
        }
        /// <summary>Creates a deterministic, world-specific random seed for one terrain chunk.</summary>
        public static int GetChunkSeed(int worldSeed, int chunkX, int chunkZ)
        {
            unchecked
            {
                int seed = worldSeed;
                seed ^= chunkX * 73428767;
                seed = (seed << 13) | (int)((uint)seed >> 19);
                seed ^= chunkZ * 912931;
                return seed;
            }
        }

        /// <summary>Calculates the poll-pass interval for one soybean growth step.</summary>
        public static int GetSoybeanGrowthInterval(bool hydrated, int nitrogen, int temperature)
        {
            int interval = 6;
            if (hydrated)
            {
                interval -= 2;
            }
            if (nitrogen > 0)
            {
                interval -= 2;
            }
            if (temperature <= 4)
            {
                interval += 6;
            }
            return interval < 1 ? 1 : interval;
        }

        /// <summary>Only an upward use on a soil block may create a soybean crop.</summary>
        public static bool CanPlaceSoybean(bool isTopFace, bool isSoil)
        {
            return isTopFace && isSoil;
        }

        /// <summary>
        /// Existing loaded chunks may contain player structures. Post-rain spawning
        /// is intentionally disabled until a durable natural-terrain provenance API
        /// is available on the target runtime.
        /// </summary>
        public static bool AllowPostRainSpawning
        {
            get { return false; }
        }
    }
}
