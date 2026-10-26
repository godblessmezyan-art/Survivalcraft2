using System;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>
    /// Deterministic geometry and damage rules for the Skywrath V4 lightning system.
    /// No engine dependency: the renderer and the authoritative damage code both call
    /// into this type, and the offline test project compiles it directly.
    /// </summary>
    internal static class LightningRules
    {
        /// <summary>Hard cap on path points per bolt (main trunk and branches alike).</summary>
        public const int MaxPathPoints = 32;

        /// <summary>Floats per path point (x, y, z).</summary>
        public const int PathStride = 3;

        /// <summary>Floats a bolt path buffer must provide.</summary>
        public const int PathFloats = MaxPathPoints * PathStride;

        /// <summary>Largest number of bolts alive at once for the backdrop storm.</summary>
        public const int MaxBackdropBolts = 18;

        /// <summary>Largest number of simultaneous near bolts.</summary>
        public const int MaxNearBolts = 7;

        // ------------------------------------------------------------------
        // Deterministic randomness. Engine.Random and System.Random are both
        // avoided on purpose: every client must be able to reproduce the same
        // storm geometry from the broadcast cast id.
        // ------------------------------------------------------------------

        /// <summary>Multiplicative mix used to derive independent streams.</summary>
        public static uint Mix(uint a, uint b)
        {
            unchecked
            {
                uint hash = a * 0x9E3779B1u ^ (b + 0x85EBCA6Bu + (a << 6) + (a >> 2));
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        /// <summary>Advances a xorshift32 stream.</summary>
        public static uint NextState(uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state == 0u ? 0x9E3779B9u : state;
            }
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public static float NextFloat(uint state)
        {
            return (NextState(state) & 0xFFFFFFu) / 16777216f;
        }

        /// <summary>Uniform float in [minimum, maximum).</summary>
        public static float Range(float minimum, float maximum, uint state)
        {
            return minimum + (maximum - minimum) * NextFloat(state);
        }

        /// <summary>Signed unit value in [-1, 1).</summary>
        public static float Signed(uint state)
        {
            return NextFloat(state) * 2f - 1f;
        }

        /// <summary>Smooth one dimensional value noise in [0, 1].</summary>
        public static float ValueNoise(uint seed, float coordinate)
        {
            if (coordinate < 0f)
            {
                coordinate = 0f;
            }

            int cell = (int)Math.Floor(coordinate);
            float fraction = coordinate - cell;
            float smooth = fraction * fraction * (3f - 2f * fraction);
            float a = (Mix(seed, (uint)cell) & 0xFFFFu) / 65535f;
            float b = (Mix(seed, (uint)(cell + 1)) & 0xFFFFu) / 65535f;
            return a + (b - a) * smooth;
        }

        // ------------------------------------------------------------------
        // Bolt path construction: a continuous, recursively perturbed polyline
        // with zero displacement at both anchors. Not a set of independent dots.
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds a jagged polyline from the start point to the end point
        /// into <paramref name="destination"/> (x, y, z interleaved).
        /// Displacement is coherent value noise on two perpendicular axes, so the result is
        /// jagged but continuous, and identical for identical seeds.
        /// </summary>
        public static void BuildPath(
            float startX, float startY, float startZ,
            float endX, float endY, float endZ,
            int segments,
            float amplitude,
            uint seed,
            float[] destination,
            out int pointCount)
        {
            if (destination == null || destination.Length < PathFloats)
            {
                throw new ArgumentException("Lightning path buffer is too small.", nameof(destination));
            }

            segments = segments < 2 ? 2 : (segments > MaxPathPoints - 1 ? MaxPathPoints - 1 : segments);
            pointCount = segments + 1;

            float dx = endX - startX;
            float dy = endY - startY;
            float dz = endZ - startZ;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 0.0001f)
            {
                destination[0] = startX;
                destination[1] = startY;
                destination[2] = startZ;
                destination[3] = endX;
                destination[4] = endY;
                destination[5] = endZ;
                pointCount = 2;
                return;
            }

            float ux = dx / length;
            float uy = dy / length;
            float uz = dz / length;

            // Stable basis: never parallel to the bolt axis.
            float refX = 0f;
            float refY = 1f;
            float refZ = 0f;
            if (Math.Abs(uy) > 0.92f)
            {
                refX = 1f;
                refY = 0f;
            }

            float sideX = uy * refZ - uz * refY;
            float sideY = uz * refX - ux * refZ;
            float sideZ = ux * refY - uy * refX;
            float sideLength = (float)Math.Sqrt(sideX * sideX + sideY * sideY + sideZ * sideZ);
            sideX /= sideLength;
            sideY /= sideLength;
            sideZ /= sideLength;

            float upX = sideY * uz - sideZ * uy;
            float upY = sideZ * ux - sideX * uz;
            float upZ = sideX * uy - sideY * ux;

            uint sideSeed = Mix(seed, 0x51ED270Bu);
            uint upSeed = Mix(seed, 0xC2B2AE35u);
            uint jitterSeed = Mix(seed, 0x27D4EB2Fu);

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)segments;
                float envelope = (float)Math.Sin(Math.PI * t);
                envelope = (float)Math.Pow(envelope, 0.7f);

                float coarseSide = ValueNoise(sideSeed, t * 3.1f) * 2f - 1f;
                float coarseUp = ValueNoise(upSeed, t * 3.1f) * 2f - 1f;
                float fineSide = ValueNoise(Mix(sideSeed, 0x68E31DA4u), t * 11.7f) * 2f - 1f;
                float fineUp = ValueNoise(Mix(upSeed, 0xB5297A4Du), t * 11.7f) * 2f - 1f;
                float kink = ValueNoise(jitterSeed, t * 23.3f) * 2f - 1f;

                float displacementSide = (coarseSide * 0.68f + fineSide * 0.32f) * amplitude * envelope;
                float displacementUp = (coarseUp * 0.68f + fineUp * 0.32f) * amplitude * envelope;
                float kinkAmount = kink * amplitude * 0.16f * envelope;

                int offset = i * PathStride;
                destination[offset] = startX + dx * t + sideX * displacementSide + upX * displacementUp + ux * kinkAmount;
                destination[offset + 1] = startY + dy * t + sideY * displacementSide + upY * displacementUp + uy * kinkAmount;
                destination[offset + 2] = startZ + dz * t + sideZ * displacementSide + upZ * displacementUp + uz * kinkAmount;
            }

            // Both anchors stay exact: the bolt must start at the sky point and land on target.
            destination[0] = startX;
            destination[1] = startY;
            destination[2] = startZ;
            int last = (pointCount - 1) * PathStride;
            destination[last] = endX;
            destination[last + 1] = endY;
            destination[last + 2] = endZ;
        }

        /// <summary>Width multiplier along a bolt: thick at the strike point, thinner at the far end.</summary>
        public static float Taper(float t, float rootScale, float tipScale)
        {
            float clamped = t < 0f ? 0f : (t > 1f ? 1f : t);
            return rootScale + (tipScale - rootScale) * clamped;
        }

        /// <summary>
        /// Normalised brightness over a bolt's life: quick attack, short hold, natural decay.
        /// Returns 0 outside [0, life].
        /// </summary>
        public static float LifeAlpha(double age, double life, float attack, float release)
        {
            if (life <= 0.0 || age < 0.0 || age >= life)
            {
                return 0f;
            }

            double attackTime = life * (attack <= 0f ? 0.02f : attack);
            double releaseTime = life * (release <= 0f ? 0.35f : release);
            if (age < attackTime)
            {
                return (float)(age / attackTime);
            }

            double holdEnd = life - releaseTime;
            if (age <= holdEnd)
            {
                return 1f;
            }

            double remaining = (life - age) / Math.Max(releaseTime, 0.0001);
            return (float)Math.Max(0.0, remaining);
        }

        // ------------------------------------------------------------------
        // Damage tables. Distances are in blocks from the impact centre.
        // ------------------------------------------------------------------

        /// <summary>Thunderous Judgment: 110 inside four blocks, decaying to 70 at nine.</summary>
        public static float JudgmentDamage(float distance)
        {
            if (distance <= 4f)
            {
                return 110f;
            }

            if (distance >= 9f)
            {
                return 0f;
            }

            return 110f + (70f - 110f) * ((distance - 4f) / 5f);
        }

        /// <summary>Myriad Tribulation: 220 at the core, 120 mid ring, 70 outer ring.</summary>
        public static float TribulationDamage(float distance)
        {
            if (distance <= 2.5f)
            {
                return 220f;
            }

            if (distance <= 5f)
            {
                return 220f + (120f - 220f) * ((distance - 2.5f) / 2.5f);
            }

            if (distance <= 8f)
            {
                return 120f + (70f - 120f) * ((distance - 5f) / 3f);
            }

            return 0f;
        }

        /// <summary>One aftershock tick; four ticks are scheduled after the main strike.</summary>
        public const float AftershockDamage = 18f;

        /// <summary>Radius of the main strike, in blocks.</summary>
        public const float TribulationRadius = 8f;

        /// <summary>Radius of Thunderous Judgment, in blocks.</summary>
        public const float JudgmentRadius = 9f;

        // ------------------------------------------------------------------
        // Backdrop storm schedule. Pure function of the cast seed so the same
        // cast renders the same show on every client, frame drops included.
        // ------------------------------------------------------------------

        /// <summary>Scheduled background strike slot.</summary>
        public struct StormStrike
        {
            /// <summary>Seconds after the cast start.</summary>
            public float Time;
            /// <summary>Horizontal offset angle from the frozen forward direction, radians.</summary>
            public float Angle;
            /// <summary>Horizontal distance from the caster, blocks.</summary>
            public float Distance;
            /// <summary>Vertical start height above the caster, blocks.</summary>
            public float Height;
            /// <summary>True for the coarse near layer, false for the dense far layer.</summary>
            public bool Near;
            /// <summary>True for the heavy layer that reads as a full sized bolt, not a backdrop tick.</summary>
            public bool Great;
            /// <summary>Geometry seed for this strike.</summary>
            public uint Seed;
        }

        /// <summary>One density window of the 12 second storm. Counts are per window.</summary>
        public struct StormPhase
        {
            /// <summary>Creates a density window.</summary>
            public StormPhase(float start, float end, int nearCount, int farCount)
            {
                Start = start;
                End = end;
                NearCount = nearCount;
                FarCount = farCount;
            }

            /// <summary>Seconds after the cast start when this window opens.</summary>
            public float Start;
            /// <summary>Seconds after the cast start when this window closes.</summary>
            public float End;
            /// <summary>Coarse, branching strikes in this window.</summary>
            public int NearCount;
            /// <summary>Dense, thin strikes in this window.</summary>
            public int FarCount;
        }

        /// <summary>
        /// V5 schedule: the same total window split into density phases so the storm builds,
        /// hushes before the main bolt and then peaks at the impact. Deterministic per cast id.
        /// </summary>
        public static StormStrike[] BuildStormSchedule(uint castSeed, StormPhase[] phases)
        {
            if (phases == null || phases.Length == 0)
            {
                return new StormStrike[0];
            }

            int total = 0;
            for (int i = 0; i < phases.Length; i++)
            {
                total += Math.Max(0, phases[i].NearCount) + Math.Max(0, phases[i].FarCount);
            }

            StormStrike[] strikes = new StormStrike[total];
            int cursor = 0;
            for (int p = 0; p < phases.Length; p++)
            {
                StormPhase phase = phases[p];
                float start = phase.Start;
                float end = phase.End < phase.Start + 0.05f ? phase.Start + 0.05f : phase.End;
                int near = Math.Max(0, phase.NearCount);
                int far = Math.Max(0, phase.FarCount);
                uint phaseSeed = Mix(castSeed, (uint)(0x900 + p * 37));

                for (int i = 0; i < near; i++)
                {
                    uint state = Mix(phaseSeed, (uint)(0x1000 + i));
                    float time = start + (end - start) * ((i + NextFloat(state)) / Math.Max(1, near));
                    strikes[cursor++] = new StormStrike
                    {
                        Time = time,
                        Angle = Signed(Mix(state, 1u)) * (86f * (float)Math.PI / 360f),
                        Distance = Range(15f, 25f, Mix(state, 2u)),
                        Height = Range(26f, 40f, Mix(state, 3u)),
                        Near = true,
                        Seed = Mix(phaseSeed, (uint)(0x2000 + i))
                    };
                }

                for (int i = 0; i < far; i++)
                {
                    uint state = Mix(phaseSeed, (uint)(0x3000 + i));
                    float time = start + (end - start) * ((i + NextFloat(state)) / Math.Max(1, far));
                    strikes[cursor++] = new StormStrike
                    {
                        Time = time,
                        Angle = Signed(Mix(state, 4u)) * (49f * (float)Math.PI / 180f),
                        Distance = Range(25f, 55f, Mix(state, 5u)),
                        Height = Range(34f, 55f, Mix(state, 6u)),
                        Near = false,
                        Seed = Mix(phaseSeed, (uint)(0x4000 + i))
                    };
                }
            }

            SortByTime(strikes);
            return strikes;
        }

        /// <summary>
        /// Builds the heavy-bolt schedule: the layer that must actually look like Mjolnir's lightning
        /// rather than a distant flicker. Great bolts are spread evenly across the window with a
        /// per-cast jitter so two casts back to back do not replay the same rhythm.
        /// </summary>
        public static StormStrike[] BuildGreatBoltSchedule(
            uint castSeed,
            float firstTime,
            float lastTime,
            int count,
            float minDistance,
            float maxDistance)
        {
            if (count <= 0 || lastTime <= firstTime)
            {
                return new StormStrike[0];
            }

            StormStrike[] strikes = new StormStrike[count];
            float span = lastTime - firstTime;
            for (int i = 0; i < count; i++)
            {
                uint state = Mix(castSeed, (uint)(0x7000 + i * 61));
                float slot = (i + 0.25f + 0.5f * NextFloat(state)) / count;
                strikes[i] = new StormStrike
                {
                    Time = firstTime + span * slot,
                    Angle = Signed(Mix(state, 8u)) * (34f * (float)Math.PI / 180f),
                    Distance = Range(minDistance, maxDistance, Mix(state, 9u)),
                    Height = Range(30f, 46f, Mix(state, 10u)),
                    Near = true,
                    Great = true,
                    Seed = Mix(state, 11u)
                };
            }

            SortByTime(strikes);
            return strikes;
        }

        /// <summary>Builds the deterministic backdrop schedule for one cast.</summary>
        public static StormStrike[] BuildStormSchedule(uint castSeed, float startTime, float endTime, int nearCount, int farCount)
        {
            nearCount = nearCount < 0 ? 0 : nearCount;
            farCount = farCount < 0 ? 0 : farCount;
            StormStrike[] strikes = new StormStrike[nearCount + farCount];
            uint state = Mix(castSeed, 0x1F123BB5u);
            int index = 0;

            for (int i = 0; i < nearCount; i++)
            {
                state = NextState(state);
                float window = (endTime - startTime) * 0.72f;
                float time = startTime + window * (i + NextFloat(state) * 0.85f) / Math.Max(1, nearCount);
                state = NextState(state);
                float angle = Signed(state) * (86f * (float)Math.PI / 360f);
                state = NextState(state);
                float distance = Range(15f, 25f, state);
                state = NextState(state);
                strikes[index++] = new StormStrike
                {
                    Time = time,
                    Angle = angle,
                    Distance = distance,
                    Height = Range(26f, 38f, state),
                    Near = true,
                    Seed = Mix(castSeed, (uint)(0x100 + i))
                };
            }

            for (int i = 0; i < farCount; i++)
            {
                state = NextState(state);
                float window = (endTime - startTime) * 0.86f;
                float time = startTime + window * (i + NextFloat(state) * 0.9f) / Math.Max(1, farCount);
                state = NextState(state);
                float angle = Signed(state) * (49f * (float)Math.PI / 180f);
                state = NextState(state);
                float distance = Range(25f, 55f, state);
                state = NextState(state);
                strikes[index++] = new StormStrike
                {
                    Time = time,
                    Angle = angle,
                    Distance = distance,
                    Height = Range(34f, 52f, state),
                    Near = false,
                    Seed = Mix(castSeed, (uint)(0x400 + i))
                };
            }

            SortByTime(strikes);
            return strikes;
        }

        /// <summary>Insertion sort by strike time; schedules are small and this stays allocation free.</summary>
        private static void SortByTime(StormStrike[] strikes)
        {
            for (int i = 1; i < strikes.Length; i++)
            {
                StormStrike value = strikes[i];
                int j = i - 1;
                while (j >= 0 && strikes[j].Time > value.Time)
                {
                    strikes[j + 1] = strikes[j];
                    j--;
                }

                strikes[j + 1] = value;
            }
        }

        /// <summary>Counts how many scheduled strikes are due at a given elapsed time.</summary>
        public static int DueStrikes(StormStrike[] schedule, float previousElapsed, float elapsed)
        {
            if (schedule == null)
            {
                return 0;
            }

            int due = 0;
            for (int i = 0; i < schedule.Length; i++)
            {
                if (schedule[i].Time > elapsed)
                {
                    break;
                }

                if (schedule[i].Time > previousElapsed)
                {
                    due++;
                }
            }

            return due;
        }
    }
}
