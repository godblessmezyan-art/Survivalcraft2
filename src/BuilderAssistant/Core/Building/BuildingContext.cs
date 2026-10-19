using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// Shared mutable state passed to every <c>...Generator</c> component while one
    /// building is being composed. It owns the <see cref="BlueprintBuilder"/> the
    /// components write into, the <see cref="BuildingSpec"/> being realized, a single
    /// seeded <see cref="DeterministicRandom"/> (so randomness is ordered and
    /// reproducible), and the few cross-component decisions that must agree: where the
    /// front door ended up, and which column is the stairwell (so the floor plan, the
    /// stair, the openings and the validator all reference the SAME facts).
    ///
    /// Components only ever touch relative coordinates; the blueprint origin and world
    /// anchor are added later by the unchanged planner. This is what keeps generation
    /// terrain-independent and safe.
    /// </summary>
    internal sealed class BuildingContext
    {
        public readonly BuildingSpec Spec;
        public readonly BlueprintBuilder Builder;
        private DeterministicRandom _rng;

        // Footprint bounds (relative). Min corner is always 0,0.
        public int X0 { get { return 0; } }
        public int X1 { get { return Spec.Width - 1; } }
        public int Z0 { get { return 0; } }
        public int Z1 { get { return Spec.Length - 1; } }

        // --- Cross-component facts, filled in as components run. ---

        // Front door. Set by OpeningGenerator; read by StairGenerator (keep the entry
        // clear of the stairwell) and BuildingValidator (the entrance must be passable).
        public bool HasDoor;
        public int DoorX;
        public int DoorY;   // bottom cell Y (ground floor wall base)
        public int DoorZ;   // wall line (front: Z0)
        public int DoorFacingRot; // authored rotation (2 = faces +Z, into the house)

        // Stairwell column (X) used to connect floors; read by the validator to confirm
        // a real vertical link exists between storeys.
        public bool HasStairwell;
        public int StairwellX;

        public BuildingContext(BuildingSpec spec)
        {
            Spec = spec;
            Builder = new BlueprintBuilder();
            _rng = new DeterministicRandom(spec.Seed);
        }

        public DeterministicRandom Rng { get { return _rng; } }

        /// <summary>Wall-course Y range for a storey: [start..end] inclusive.</summary>
        public int WallBaseY(int story) { return Spec.FloorSlabY(story) + 1; }
        public int WallTopY(int story) { return Spec.FloorSlabY(story) + Spec.WallHeight; }

        /// <summary>True if (x,z) is strictly inside the exterior walls (not on them).</summary>
        public bool IsInteriorColumn(int x, int z)
        {
            return x > X0 && x < X1 && z > Z0 && z < Z1;
        }

        // --- Oriented write helpers so components never re-derive family from a key. ---

        public void SetCube(int x, int y, int z, string materialKey)
        {
            Builder.Set(x, y, z, materialKey);
        }

        public void SetStairs(int x, int y, int z, string materialKey, int rot)
        {
            Builder.SetOriented(x, y, z, materialKey, BlockFamily.Stairs, rot & 0x7);
        }

        public void SetSlabBottom(int x, int y, int z, string materialKey)
        {
            Builder.SetOriented(x, y, z, materialKey, BlockFamily.Slab, 0);
        }

        public void SetSlabTop(int x, int y, int z, string materialKey)
        {
            Builder.SetOriented(x, y, z, materialKey, BlockFamily.Slab, 1);
        }

        public void SetFence(int x, int y, int z, string materialKey, int armMask)
        {
            Builder.SetOriented(x, y, z, materialKey, BlockFamily.Fence, armMask & 0xF);
        }

        public void SetDoor(int x, int y, int z, string materialKey, int rot)
        {
            Builder.SetOriented(x, y, z, materialKey, BlockFamily.Door, rot & 0x3);
        }
    }
}
