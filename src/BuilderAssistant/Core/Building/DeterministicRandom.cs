namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// A tiny, self-contained deterministic PRNG (xorshift32). We deliberately do NOT
    /// use <see cref="System.Random"/>: its seeded sequence is not guaranteed bit-identical
    /// across .NET Framework 4.7.2 and .NET 8, and the whole point of the seeded
    /// generator is that the SAME (spec + seed) produces the EXACT SAME blueprint on the
    /// Windows editor, in the offline net8 test harness, and on the Android client. A
    /// pure integer xorshift gives that cross-runtime reproducibility for free.
    ///
    /// It is intentionally simple (not statistically uniform); building layout only needs
    /// stable, well-spread choices, not cryptographic or high-quality sampling.
    /// </summary>
    internal struct DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            // Any 32-bit seed is fine; xorshift only forbids an all-zero state.
            unchecked
            {
                _state = (uint)seed;
            }
            if (_state == 0u)
            {
                _state = 0x9E3779B9u; // golden-ratio constant; guarantees a non-zero state
            }
        }

        /// <summary>Advance once and return the raw 32-bit value.</summary>
        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Return a value in [0, exclusiveMax). Non-positive ranges yield 0.</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                return 0;
            }
            return (int)(NextUInt() % (uint)exclusiveMax);
        }

        /// <summary>Return a value in [min, maxInclusive]. If min &gt; max, returns min.</summary>
        public int Range(int min, int maxInclusive)
        {
            if (maxInclusive <= min)
            {
                return min;
            }
            return min + NextInt(maxInclusive - min + 1);
        }

        /// <summary>True with the given integer probability out of 100.</summary>
        public bool Chance(int percent)
        {
            if (percent <= 0)
            {
                return false;
            }
            if (percent >= 100)
            {
                return true;
            }
            return NextInt(100) < percent;
        }
    }
}
