namespace SurvivalcraftRuins.Core;

/// <summary>
/// Seed-stable mixing independent of .NET's randomized string hashing, so the
/// same world seed always yields the same structure layout across sessions.
/// </summary>
public static class DeterministicHash
{
    public static uint Mix(uint a, uint b)
    {
        unchecked
        {
            uint h = a * 0x9E3779B1u ^ b;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            h *= 0xC2B2AE3Du;
            h ^= h >> 16;
            return h;
        }
    }

    public static uint Mix(int seed, int salt, int x, int z)
    {
        unchecked
        {
            uint h = Mix((uint)seed, (uint)salt);
            h = Mix(h, (uint)x);
            h = Mix(h, (uint)z);
            return h;
        }
    }

    /// <summary>Uniform float in [0,1) from a hash.</summary>
    public static float ToUnitFloat(uint h)
    {
        return (h >> 8) * (1f / 16777216f);
    }

    /// <summary>Stable non-cryptographic hash for strings (FNV-1a).</summary>
    public static int StringSalt(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char c in s)
            {
                h = (h ^ c) * 16777619u;
            }
            return (int)h;
        }
    }
}
