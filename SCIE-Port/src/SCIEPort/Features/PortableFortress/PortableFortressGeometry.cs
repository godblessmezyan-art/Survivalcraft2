using System;
using System.Collections.Generic;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>
    /// Axis-aligned box in fortress-local space. Local units are game cells: one unit
    /// equals one block. Front is local -Z, rear (entrance) is local +Z, origin is the
    /// foundation centre at ground level.
    /// </summary>
    public readonly struct FortressBox
    {
        /// <summary>Creates a box from its extremes.</summary>
        public FortressBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            if (maxX < minX || maxY < minY || maxZ < minZ)
            {
                throw new ArgumentException("Fortress box extremes are inverted.");
            }

            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        /// <summary>Minimum X.</summary>
        public float MinX { get; }
        /// <summary>Minimum Y.</summary>
        public float MinY { get; }
        /// <summary>Minimum Z.</summary>
        public float MinZ { get; }
        /// <summary>Maximum X.</summary>
        public float MaxX { get; }
        /// <summary>Maximum Y.</summary>
        public float MaxY { get; }
        /// <summary>Maximum Z.</summary>
        public float MaxZ { get; }

        /// <summary>Centre of the box.</summary>
        public float CentreX => (MinX + MaxX) * 0.5f;
        /// <summary>Centre of the box.</summary>
        public float CentreY => (MinY + MaxY) * 0.5f;
        /// <summary>Centre of the box.</summary>
        public float CentreZ => (MinZ + MaxZ) * 0.5f;
        /// <summary>Extent along X.</summary>
        public float SizeX => MaxX - MinX;
        /// <summary>Extent along Y.</summary>
        public float SizeY => MaxY - MinY;
        /// <summary>Extent along Z.</summary>
        public float SizeZ => MaxZ - MinZ;

        /// <summary>True when the point lies inside the box, edges included.</summary>
        public bool Contains(float x, float y, float z)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
        }

        /// <summary>True when this box intersects another box.</summary>
        public bool Intersects(FortressBox other)
        {
            return MinX <= other.MaxX && MaxX >= other.MinX
                && MinY <= other.MaxY && MaxY >= other.MinY
                && MinZ <= other.MaxZ && MaxZ >= other.MinZ;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[{0:0.###},{1:0.###},{2:0.###}]-[{3:0.###},{4:0.###},{5:0.###}]",
                MinX, MinY, MinZ, MaxX, MaxY, MaxZ);
        }
    }

    /// <summary>Coarse role of a collision segment, used for reporting and tests.</summary>
    public enum FortressZone
    {
        /// <summary>Level-1 walking surface.</summary>
        Floor,
        /// <summary>Exterior shell.</summary>
        Wall,
        /// <summary>Interior partition.</summary>
        Partition,
        /// <summary>Level-2 walking surface.</summary>
        Deck,
        /// <summary>Roof walking surface.</summary>
        Roof,
        /// <summary>Roof guard rail.</summary>
        Rail,
        /// <summary>Stair tread.</summary>
        Stair,
        /// <summary>
        /// Glazing pane. These proxies exist only to stop a body walking out through an
        /// aperture; they are deliberately excluded from the "opening stays open" checks
        /// because the window itself must stay see-through.
        /// </summary>
        Glazing
    }

    /// <summary>One body-backed collision proxy owned by a fortress instance.</summary>
    public sealed class FortressSegment
    {
        /// <summary>Creates a segment.</summary>
        public FortressSegment(string name, FortressZone zone, FortressBox box)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Zone = zone;
            Box = box;
        }

        /// <summary>Stable name; segments are addressed by index in the instance record.</summary>
        public string Name { get; }
        /// <summary>Coarse role.</summary>
        public FortressZone Zone { get; }
        /// <summary>Local-space box.</summary>
        public FortressBox Box { get; }
    }

    /// <summary>
    /// Horizontal quarter-turn of a deployed fortress. The value names the world
    /// direction the fortress front (local -Z) points at.
    /// </summary>
    public enum FortressFacing
    {
        /// <summary>Front faces world -Z. Model yaw 0.</summary>
        NegativeZ = 0,
        /// <summary>Front faces world +X. Model yaw 90 degrees.</summary>
        PositiveX = 1,
        /// <summary>Front faces world +Z. Model yaw 180 degrees.</summary>
        PositiveZ = 2,
        /// <summary>Front faces world -X. Model yaw 270 degrees.</summary>
        NegativeX = 3
    }

    /// <summary>
    /// Frozen fortress geometry and the derived collision / traversal plans.
    /// Every number here mirrors <c>docs/portable-fortress/LAYOUT.md</c> and the part list
    /// of <c>scripts/New-PortableFortressModel.ps1</c>; changing one without the other is
    /// a defect. This type has no game or engine dependency so it can be unit tested.
    /// </summary>
    public static class PortableFortressGeometry
    {
        /// <summary>Envelope half extent along local X (11 cells wide).</summary>
        public const float HalfWidth = 5.5f;
        /// <summary>Envelope half extent along local Z (13 cells long).</summary>
        public const float HalfLength = 6.5f;
        /// <summary>Envelope height (9 cells).</summary>
        public const float EnvelopeHeight = 9f;
        /// <summary>Level-1 walking surface; the foundation top, not a full cell.</summary>
        public const float FloorTop = 0.36f;
        /// <summary>Top of the level-2 deck slab.</summary>
        public const float DeckTop = 3.84f;
        /// <summary>Bottom of the level-2 deck slab.</summary>
        public const float DeckBottom = 3.60f;
        /// <summary>Top of the roof deck.</summary>
        public const float RoofTop = 7.41f;
        /// <summary>Bottom of the roof deck.</summary>
        public const float RoofBottom = 7.15f;
        /// <summary>Top of the roof guard rail.</summary>
        public const float RailTop = 8.55f;
        /// <summary>Number of stair treads in the main stair.</summary>
        public const int StairStepCount = 8;
        /// <summary>
        /// Rise of one stair tread. Deliberately <c>(DeckTop - FloorTop) / StairStepCount</c> so
        /// the last tread top equals the level-2 deck top exactly: a tread that overshoots the deck
        /// leaves a body lip the player cannot walk over.
        /// </summary>
        public const float StairRise = 0.435f;
        /// <summary>Run of one stair tread.</summary>
        public const float StairRun = 0.50f;
        /// <summary>Local X of the stair west edge.</summary>
        public const float StairMinX = -4.30f;
        /// <summary>Local X of the stair east edge.</summary>
        public const float StairMaxX = -2.20f;
        /// <summary>Local Z of the stair front edge.</summary>
        public const float StairMinZ = -4.00f;
        /// <summary>Local Z of the stair rear edge (stairwell opening).</summary>
        public const float StairMaxZ = 0.25f;
        /// <summary>Local X of the rear door opening west edge.</summary>
        public const float DoorMinX = -1.20f;
        /// <summary>Local X of the rear door opening east edge.</summary>
        public const float DoorMaxX = 1.20f;
        /// <summary>Local Z of the rear door plane.</summary>
        public const float DoorZ = 6.35f;
        /// <summary>Inner face of the rear wall; the level-1 floor proxy stops here.</summary>
        public const float DoorInnerZ = 6.20f;
        /// <summary>Outer face of the rear wall.</summary>
        public const float RearFaceZ = 6.50f;
        /// <summary>Half width of the doorway, including the walking clearance either side.</summary>
        public const float DoorHalfClearanceX = 1.45f;
        /// <summary>Front edge of the entrance sill strip used by the bounded rise assist.</summary>
        public const float EntrySillMinZ = 5.45f;
        /// <summary>Rear edge of the entrance sill strip; must stay on the hidden threshold cells.</summary>
        public const float EntrySillMaxZ = RearFaceZ;
        /// <summary>Top of the window sill on the observation deck.</summary>
        public const float WindowSillTop = 4.705f;
        /// <summary>Bottom of the window header on the observation deck.</summary>
        public const float WindowHeadBottom = 6.445f;
        /// <summary>Local X of the roof access column west edge.</summary>
        public const float ShaftMinX = 2.325f;
        /// <summary>Local X of the roof access column east edge.</summary>
        public const float ShaftMaxX = 3.975f;
        /// <summary>Local Z of the roof access column front edge.</summary>
        public const float ShaftMinZ = 2.725f;
        /// <summary>Local Z of the roof access column rear edge.</summary>
        public const float ShaftMaxZ = 4.375f;
        /// <summary>Highest surface a player can stand on.</summary>
        public const float HighestWalkingSurface = RoofTop;

        /// <summary>Outer envelope of the deployed fortress.</summary>
        public static readonly FortressBox Envelope =
            new FortressBox(-HalfWidth, 0f, -HalfLength, HalfWidth, EnvelopeHeight, HalfLength);

        private static readonly FortressSegment[] s_segments = BuildSegments();

        /// <summary>All body-backed collision proxies, in stable index order.</summary>
        public static IReadOnlyList<FortressSegment> Segments => s_segments;

        /// <summary>
        /// Largest allowed half-diagonal of a proxy. <c>ComponentBody</c> looks up other
        /// bodies with <c>SubsystemBodies.FindBodiesAroundPoint(..., radius: 4f)</c> around
        /// the mover, so an oversized proxy would not be found near its own ends and the
        /// player would fall through it.
        /// </summary>
        public const float MaxSegmentHalfDiagonal = 4f;

        /// <summary>Rotates a local box by the given quarter turn, keeping it axis aligned.</summary>
        public static FortressBox Rotate(FortressBox box, FortressFacing facing)
        {
            float x0 = box.MinX;
            float x1 = box.MaxX;
            float z0 = box.MinZ;
            float z1 = box.MaxZ;
            float rx0;
            float rz0;
            float rx1;
            float rz1;
            switch (facing)
            {
                case FortressFacing.PositiveX:
                    rx0 = z0; rz0 = -x1;
                    rx1 = z1; rz1 = -x0;
                    break;
                case FortressFacing.PositiveZ:
                    rx0 = -x1; rz0 = -z1;
                    rx1 = -x0; rz1 = -z0;
                    break;
                case FortressFacing.NegativeX:
                    rx0 = -z1; rz0 = x0;
                    rx1 = -z0; rz1 = x1;
                    break;
                default:
                    return box;
            }

            return new FortressBox(
                Math.Min(rx0, rx1), box.MinY, Math.Min(rz0, rz1),
                Math.Max(rx0, rx1), box.MaxY, Math.Max(rz0, rz1));
        }

        /// <summary>Rotates a local horizontal offset into world space.</summary>
        public static void RotateOffset(float localX, float localZ, FortressFacing facing, out float worldX, out float worldZ)
        {
            switch (facing)
            {
                case FortressFacing.PositiveX:
                    worldX = localZ;
                    worldZ = -localX;
                    break;
                case FortressFacing.PositiveZ:
                    worldX = -localX;
                    worldZ = -localZ;
                    break;
                case FortressFacing.NegativeX:
                    worldX = -localZ;
                    worldZ = localX;
                    break;
                default:
                    worldX = localX;
                    worldZ = localZ;
                    break;
            }
        }

        /// <summary>Rotates a world horizontal offset back into fortress-local space.</summary>
        public static void ToLocalOffset(float worldX, float worldZ, FortressFacing facing, out float localX, out float localZ)
        {
            switch (facing)
            {
                case FortressFacing.PositiveX:
                    localX = -worldZ;
                    localZ = worldX;
                    break;
                case FortressFacing.PositiveZ:
                    localX = -worldX;
                    localZ = -worldZ;
                    break;
                case FortressFacing.NegativeX:
                    localX = worldZ;
                    localZ = -worldX;
                    break;
                default:
                    localX = worldX;
                    localZ = worldZ;
                    break;
            }
        }

        /// <summary>Model yaw, in radians, for a facing.</summary>
        public static float Yaw(FortressFacing facing)
        {
            return (int)facing * (float)(Math.PI * 0.5);
        }

        /// <summary>
        /// Snaps a horizontal look direction to the nearest quarter turn. The fortress
        /// front (local -Z) points away from the player, so the entrance faces the player.
        /// </summary>
        public static FortressFacing FacingFromLook(float lookX, float lookZ)
        {
            if (Math.Abs(lookX) > Math.Abs(lookZ))
            {
                return lookX >= 0f ? FortressFacing.PositiveX : FortressFacing.NegativeX;
            }

            return lookZ >= 0f ? FortressFacing.PositiveZ : FortressFacing.NegativeZ;
        }

        /// <summary>
        /// Local cell offsets covered by the footprint, as (dx, dz) pairs relative to the
        /// anchor cell. Already rotated into world axes.
        /// </summary>
        public static IEnumerable<(int DX, int DZ)> FootprintCells(FortressFacing facing)
        {
            for (int lx = -5; lx <= 5; lx++)
            {
                for (int lz = -6; lz <= 6; lz++)
                {
                    RotateOffset(lx, lz, facing, out float wx, out float wz);
                    yield return ((int)Math.Round(wx), (int)Math.Round(wz));
                }
            }
        }

        /// <summary>Number of cells in the footprint (11 x 13).</summary>
        public const int FootprintCellCount = 143;

        /// <summary>Number of vertical cells that must be clear above the foundation.</summary>
        public const int ClearanceCellCount = 9;

        /// <summary>Top surface height of a stair tread; tread index is zero based.</summary>
        public static float StairTreadTop(int tread)
        {
            int clamped = tread < 0 ? 0 : (tread >= StairStepCount ? StairStepCount - 1 : tread);
            return FloorTop + StairRise * (clamped + 1);
        }

        /// <summary>Stair tread index under a local Z coordinate, or -1 when outside the stair.</summary>
        public static int StairTreadAt(float localZ)
        {
            if (localZ < StairMinZ || localZ > StairMaxZ)
            {
                return -1;
            }

            int tread = (int)Math.Floor((localZ - StairMinZ) / StairRun);
            if (tread < 0)
            {
                tread = 0;
            }

            if (tread >= StairStepCount)
            {
                tread = StairStepCount - 1;
            }

            return tread;
        }

        private static FortressSegment[] BuildSegments()
        {
            var list = new List<FortressSegment>(96);

            // Level-1 floor: foundation top at Y=0.36, tiled so every tile is small enough
            // for SubsystemBodies to find it from anywhere on its own surface.
            //
            // The rear edge stops at the inner face of the rear wall. It used to run to the
            // envelope edge, which put a 0.36-high *body* lip right across the doorway; body
            // colliders are never smooth-risen (ComponentBody.HandleAxisCollision compares the
            // smoothed terrain push against the unsmoothed body push and the larger one wins),
            // so the doorway was a wall the player had to jump. The doorway floor is now carried
            // by the hidden threshold terrain cells instead, which do feed HandleSmoothRise.
            AddTiles(list, FortressZone.Floor, "Floor", 0f, FloorTop,
                new[] { -5.5f, -1.9f, 1.9f, 5.5f },
                new[] { -6.5f, -3.2f, 0.1f, 3.4f, DoorInnerZ });

            // Level-2 deck: level with the stairwell opening x[-4.3,-2.2] z[-4,0.25] removed.
            AddTiles(list, FortressZone.Deck, "Deck", DeckBottom, DeckTop,
                new[] { -4.4f, -2.2f, 1.1f, 4.4f },
                new[] { -5.95f, -4.0f, 0.25f, 4.95f },
                (x0, z0) => !(x0 >= -4.4f && x0 < -2.2f && z0 >= -4.0f && z0 < 0.25f));

            // Roof deck: hatch x[2.3,4] z[2.7,4.4] stays open.
            AddTiles(list, FortressZone.Roof, "Roof", RoofBottom, RoofTop,
                new[] { -4.275f, -1.0f, 2.3f, 3.975f },
                new[] { -5.9f, -1.5f, 2.7f, 4.4f, 5.0f },
                (x0, z0) => !(x0 >= 2.3f && z0 >= 2.7f && z0 < 4.4f));

            // Lower shell (3.10 tall, 0.30 thick). Long walls are split so a proxy's centre
            // stays within the body lookup radius of its own ends.
            AddWallRun(list, "LowerRearWallWest", -5.5f, -1.2f, 6.20f, 6.50f, 0f, 3.10f);
            AddWallRun(list, "LowerRearWallEast", 1.2f, 5.5f, 6.20f, 6.50f, 0f, 3.10f);
            AddWallRun(list, "LowerRearDoorHeader", -1.2f, 1.2f, 6.20f, 6.50f, 2.75f, 3.10f);
            AddWallRun(list, "LowerSideWallWest", -5.50f, -5.20f, -4.80f, 4.80f, 0f, 3.10f);
            AddWallRun(list, "LowerSideWallEast", 5.20f, 5.50f, -4.80f, 4.80f, 0f, 3.10f);
            AddWallRun(list, "LowerFrontWall", -3.80f, 3.80f, -6.50f, -6.20f, 0f, 3.10f);
            AddWallRun(list, "LowerFrontChamferWest", -5.50f, -3.60f, -6.50f, -4.60f, 0f, 3.10f);
            AddWallRun(list, "LowerFrontChamferEast", 3.60f, 5.50f, -6.50f, -4.60f, 0f, 3.10f);
            AddWallRun(list, "LowerRearChamferWest", -5.50f, -3.60f, 4.60f, 6.50f, 0f, 3.10f);
            AddWallRun(list, "LowerRearChamferEast", 3.60f, 5.50f, 4.60f, 6.50f, 0f, 3.10f);

            // Interior partitions that must remain solid for the airlock and heating room.
            AddWallRun(list, "AirlockWallWest", -1.53f, -1.37f, 3.85f, 6.20f, 0f, 3.10f);
            AddWallRun(list, "AirlockWallEast", 1.37f, 1.53f, 3.85f, 6.20f, 0f, 3.10f);
            AddWallRun(list, "HeatingPartition", -3.08f, -2.92f, 1.20f, 4.30f, 0f, 3.10f);

            // Main stair: eight treads, each its own proxy so the walk assist can address them.
            for (int tread = 0; tread < StairStepCount; tread++)
            {
                float z0 = StairMinZ + tread * StairRun;
                list.Add(new FortressSegment(
                    "StairTread" + tread,
                    FortressZone.Stair,
                    new FortressBox(StairMinX, FloorTop, z0, StairMaxX, FloorTop + StairRise * (tread + 1), z0 + StairRun)));
            }

            // Upper shell: sill and header only. Window apertures are deliberately left open
            // so the observation deck keeps its real sight lines and cannot be sealed.
            AddWallRun(list, "UpperSillWest", -4.375f, -4.125f, -4.80f, 4.80f, 3.855f, 4.705f);
            AddWallRun(list, "UpperSillEast", 4.125f, 4.375f, -4.80f, 4.80f, 3.855f, 4.705f);
            AddWallRun(list, "UpperHeaderWest", -4.375f, -4.125f, -4.80f, 4.80f, 6.445f, 7.195f);
            AddWallRun(list, "UpperHeaderEast", 4.125f, 4.375f, -4.80f, 4.80f, 6.445f, 7.195f);
            AddWallRun(list, "UpperRearWallWest", -4.30f, -1.20f, 4.825f, 5.075f, 3.855f, 7.195f);
            AddWallRun(list, "UpperRearWallEast", 1.20f, 4.30f, 4.825f, 5.075f, 3.855f, 7.195f);
            AddWallRun(list, "UpperRearHeader", -1.20f, 1.20f, 4.825f, 5.075f, 6.445f, 7.195f);
            AddWallRun(list, "NoseSillFront", -3.20f, 3.20f, -6.47f, -6.23f, 3.885f, 4.710f);
            AddWallRun(list, "NoseHeaderFront", -3.20f, 3.20f, -6.47f, -6.23f, 6.445f, 7.195f);
            AddWallRun(list, "NoseSillWest", -5.10f, -3.20f, -6.47f, -4.35f, 3.885f, 4.710f);
            AddWallRun(list, "NoseSillEast", 3.20f, 5.10f, -6.47f, -4.35f, 3.885f, 4.710f);
            AddWallRun(list, "NoseHeaderWest", -5.10f, -3.20f, -6.47f, -4.35f, 6.445f, 7.195f);
            AddWallRun(list, "NoseHeaderEast", 3.20f, 5.10f, -6.47f, -4.35f, 6.445f, 7.195f);

            // Glazing. These proxies carry no visual; they only stop a body from walking out
            // through an aperture. The pane is drawn separately with alpha blending because
            // ComponentModel is submitted to the opaque / alpha-tested shader only.
            AddGlassPane(list, "GlazingFront", -3.20f, 3.20f, WindowSillTop, WindowHeadBottom, -6.39f, -6.31f);
            for (int side = -1; side <= 1; side += 2)
            {
                float paneX0 = side < 0 ? -4.29f : 4.21f;
                float paneX1 = side < 0 ? -4.21f : 4.29f;
                AddGlassPane(list, "GlazingSide" + (side < 0 ? "West" : "East") + "Front",
                    paneX0, paneX1, WindowSillTop, WindowHeadBottom, -4.80f, 0f);
                AddGlassPane(list, "GlazingSide" + (side < 0 ? "West" : "East") + "Rear",
                    paneX0, paneX1, WindowSillTop, WindowHeadBottom, 0f, 4.80f);
                // The nose is a 42 degree splay, which an axis-aligned proxy cannot follow.
                // One block per quadrant band is used instead: it is inside the glazed corner,
                // so it never blocks the deck, and it stops a body walking out of the splay.
                AddGlassPane(list, "NoseGuard" + (side < 0 ? "West" : "East"),
                    side < 0 ? -5.10f : 3.20f, side < 0 ? -3.20f : 5.10f,
                    WindowSillTop, WindowHeadBottom, -6.47f, -4.35f);
            }

            // Roof guard rail. Open on the hatch side, matching the reserved circulation point.
            AddWallRun(list, "RailFront", -4.18f, 4.18f, -5.63f, -5.47f, 8.39f, RailTop);
            AddWallRun(list, "RailRearWest", -4.18f, -1.13f, 4.47f, 4.63f, 8.39f, RailTop);
            AddWallRun(list, "RailWest", -4.18f, -4.02f, -5.63f, 4.63f, 8.39f, RailTop);
            AddWallRun(list, "RailEast", 4.02f, 4.18f, -5.63f, 4.63f, 8.39f, RailTop);

            return list.ToArray();
        }

        private delegate bool TileFilter(float minX, float minZ);

        /// <summary>
        /// Adds one collision-only glazing pane. Kept as a separate helper so the pane thickness
        /// and the observation-deck sill/header are always taken from the shared constants.
        /// </summary>
        private static void AddGlassPane(
            List<FortressSegment> list,
            string name,
            float minX,
            float maxX,
            float minY,
            float maxY,
            float minZ,
            float maxZ)
        {
            // Split along Z when a side run would otherwise exceed the body lookup budget.
            float run = maxZ - minZ;
            int parts = Math.Max(1, (int)Math.Ceiling(run / 6.8f));
            for (int i = 0; i < parts; i++)
            {
                float a = minZ + run * i / parts;
                float b = minZ + run * (i + 1) / parts;
                string partName = parts == 1 ? name : name + i;
                list.Add(new FortressSegment(
                    partName,
                    FortressZone.Glazing,
                    new FortressBox(minX, minY, a, maxX, maxY, b)));
            }
        }

        private static void AddTiles(
            List<FortressSegment> list,
            FortressZone zone,
            string prefix,
            float minY,
            float maxY,
            float[] xEdges,
            float[] zEdges,
            TileFilter filter = null)
        {
            for (int xi = 0; xi + 1 < xEdges.Length; xi++)
            {
                for (int zi = 0; zi + 1 < zEdges.Length; zi++)
                {
                    float x0 = xEdges[xi];
                    float x1 = xEdges[xi + 1];
                    float z0 = zEdges[zi];
                    float z1 = zEdges[zi + 1];
                    if (filter != null && !filter(x0, z0))
                    {
                        continue;
                    }

                    list.Add(new FortressSegment(
                        prefix + xi + "-" + zi,
                        zone,
                        new FortressBox(x0, minY, z0, x1, maxY, z1)));
                }
            }
        }

        /// <summary>
        /// Adds one wall run, splitting it along its longer horizontal axis so no proxy
        /// exceeds <see cref="MaxSegmentHalfDiagonal"/> from centre to corner.
        /// </summary>
        private static void AddWallRun(
            List<FortressSegment> list,
            string name,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float minY,
            float maxY)
        {
            float sizeX = maxX - minX;
            float sizeZ = maxZ - minZ;
            float sizeY = maxY - minY;
            bool alongZ = sizeZ >= sizeX;
            float run = alongZ ? sizeZ : sizeX;
            float thick = alongZ ? sizeX : sizeZ;
            int parts = Math.Max(1, (int)Math.Ceiling(run / 7.0f));
            for (int i = 0; i < parts; i++)
            {
                float a = run * i / parts;
                float b = run * (i + 1) / parts;
                string partName = parts == 1 ? name : name + i;
                FortressBox box = alongZ
                    ? new FortressBox(minX, minY, minZ + a, minX + thick, maxY, minZ + b)
                    : new FortressBox(minX + a, minY, minZ, minX + b, maxY, minZ + thick);
                if (sizeY <= 0f)
                {
                    throw new InvalidOperationException("Wall run height must be positive: " + name);
                }

                list.Add(new FortressSegment(partName, FortressZone.Wall, box));
            }
        }
    }
}
