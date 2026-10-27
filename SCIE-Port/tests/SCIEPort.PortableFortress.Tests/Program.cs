using System;
using System.Collections.Generic;
using System.Linq;
using SCIEPort.Features.PortableFortress;

internal static class Program
{
    private static int s_count;

    private static int Main()
    {
        EnvelopeAndSegments();
        OpeningsStayOpen();
        RotationLaw();
        FootprintAndAnchor();
        PreflightStatuses();
        BodyOverlapGate();
        TraversalPlan();
        RecoveryGate();
        Console.WriteLine("Portable fortress rule tests passed: " + s_count);
        return 0;
    }

    private static void EnvelopeAndSegments()
    {
        IReadOnlyList<FortressSegment> segments = PortableFortressGeometry.Segments;
        Assert(segments.Count > 20 && segments.Count <= FortressTransactionLimits.MaxSegmentsPerInstance,
            "segment count within budget: " + segments.Count);
        Assert(PortableFortressGeometry.Envelope.SizeX == 11f, "envelope is 11 wide");
        Assert(PortableFortressGeometry.Envelope.SizeZ == 13f, "envelope is 13 long");
        Assert(PortableFortressGeometry.Envelope.SizeY == 9f, "envelope is 9 high");

        foreach (FortressSegment segment in segments)
        {
            FortressBox box = segment.Box;
            Assert(box.MinX >= -PortableFortressGeometry.HalfWidth - 0.001f
                && box.MaxX <= PortableFortressGeometry.HalfWidth + 0.001f
                && box.MinZ >= -PortableFortressGeometry.HalfLength - 0.001f
                && box.MaxZ <= PortableFortressGeometry.HalfLength + 0.001f
                && box.MinY >= -0.001f
                && box.MaxY <= PortableFortressGeometry.EnvelopeHeight + 0.001f,
                "segment stays inside the envelope: " + segment.Name + " " + box);

            float hx = box.SizeX * 0.5f;
            float hy = box.SizeY * 0.5f;
            float hz = box.SizeZ * 0.5f;
            double diagonal = Math.Sqrt(hx * hx + hy * hy + hz * hz);
            Assert(diagonal <= PortableFortressGeometry.MaxSegmentHalfDiagonal,
                "segment is small enough for body lookup: " + segment.Name + " " + diagonal);
        }

        Assert(segments.Any(s => s.Zone == FortressZone.Floor), "floor present");
        Assert(segments.Any(s => s.Zone == FortressZone.Deck), "deck present");
        Assert(segments.Any(s => s.Zone == FortressZone.Roof), "roof present");
        Assert(segments.Any(s => s.Zone == FortressZone.Stair), "stair present");
        Assert(segments.Count(s => s.Zone == FortressZone.Stair) == PortableFortressGeometry.StairStepCount,
            "eight stair treads");
        Assert(segments.Any(s => s.Zone == FortressZone.Glazing), "glazing proxies present");
        foreach (FortressSegment pane in segments.Where(s => s.Zone == FortressZone.Glazing))
        {
            if (!pane.Name.StartsWith("Glazing", StringComparison.Ordinal))
            {
                continue; // the splayed nose corner uses a block proxy, not a pane
            }

            float thin = Math.Min(pane.Box.SizeX, pane.Box.SizeZ);
            Assert(thin <= 0.09f, "glazing pane stays thin enough to see through: " + pane.Name + " " + thin);
        }

        // The level-1 walking surface must sit at 0.36, never a full cell: a full-cell floor
        // would leave the player floating above the visual foundation top.
        foreach (FortressSegment floor in segments.Where(s => s.Zone == FortressZone.Floor))
        {
            Assert(Math.Abs(floor.Box.MaxY - PortableFortressGeometry.FloorTop) < 0.001f, "floor top is 0.36: " + floor.Name);
            Assert(Math.Abs(floor.Box.MinY) < 0.001f, "floor starts at the deployment plane: " + floor.Name);
        }

        // No proxy may sit below the deployment plane.
        Assert(segments.All(s => s.Box.MinY >= -0.001f), "nothing below the deployment plane");
    }

    private static void OpeningsStayOpen()
    {
        IReadOnlyList<FortressSegment> segments = PortableFortressGeometry.Segments;

        // Rear entrance: 2.4 wide x 2.75 high above the 0.36 foundation top.
        Assert(!segments.Any(s => Overlaps(s.Box, -1.19f, 0.37f, 6.21f, 1.19f, 2.74f, 6.49f)),
            "rear door opening is not sealed");

        // Regression: the floor must never reach into the doorway again. A 0.36-high body lip
        // across the door cannot be smooth-risen (body pushes are never smoothed), so the player
        // was forced to jump. The doorway floor is the hidden threshold terrain instead.
        Assert(!segments.Any(s => s.Zone == FortressZone.Floor && s.Box.MaxZ > PortableFortressGeometry.DoorInnerZ + 0.001f),
            "floor stops at the inner face of the rear wall");
        Assert(segments.Any(s => s.Zone == FortressZone.Floor && Math.Abs(s.Box.MaxZ - PortableFortressGeometry.DoorInnerZ) < 0.001f),
            "floor still reaches the rear wall");

        // Observation apertures: the band between sill and header on both side walls.
        Assert(!segments.Any(s => ClearOfGlazing(s) && Overlaps(s.Box, 4.0f, 4.75f, -4.8f, 4.5f, 6.4f, 4.8f)),
            "east window band is not sealed");
        Assert(!segments.Any(s => ClearOfGlazing(s) && Overlaps(s.Box, -4.5f, 4.75f, -4.8f, -4.0f, 6.4f, 4.8f)),
            "west window band is not sealed");
        Assert(!segments.Any(s => ClearOfGlazing(s) && Overlaps(s.Box, -3.2f, 4.75f, -6.5f, 3.2f, 6.4f, -6.2f)),
            "front observation aperture is not sealed");

        // Stairwell: no deck may cover x[-4.3,-2.2] z[-4,0.25].
        Assert(!segments.Any(s => s.Zone == FortressZone.Deck && Overlaps(s.Box, -4.29f, 3.5f, -3.99f, -2.21f, 3.9f, 0.24f)),
            "stairwell opening is not decked over");

        // Roof hatch: no roof may cover x[2.3,4] z[2.7,4.4].
        Assert(!segments.Any(s => s.Zone == FortressZone.Roof && Overlaps(s.Box, 2.35f, 7.1f, 2.75f, 3.95f, 7.45f, 4.35f)),
            "roof hatch is not decked over");

        // Roof access column must stay passable end to end.
        Assert(!segments.Any(s => s.Zone != FortressZone.Deck
                && Overlaps(s.Box, 2.4f, 3.9f, 2.8f, 3.9f, 7.1f, 4.3f)),
            "roof access column is not blocked");
    }

    private static void RotationLaw()
    {
        FortressBox probe = new FortressBox(-5.5f, 0f, -6.5f, 5.5f, 9f, 6.5f);
        FortressBox turned = PortableFortressGeometry.Rotate(probe, FortressFacing.PositiveX);
        Assert(Math.Abs(turned.SizeX - 13f) < 0.001f, "quarter turn swaps X extent");
        Assert(Math.Abs(turned.SizeZ - 11f) < 0.001f, "quarter turn swaps Z extent");
        Assert(Math.Abs(turned.MinY) < 0.001f && Math.Abs(turned.MaxY - 9f) < 0.001f, "quarter turn keeps Y");

        foreach (FortressFacing facing in new[] { FortressFacing.NegativeZ, FortressFacing.PositiveX, FortressFacing.PositiveZ, FortressFacing.NegativeX })
        {
            FortressBox once = PortableFortressGeometry.Rotate(probe, facing);
            Assert(Math.Abs(once.SizeX * once.SizeZ - 143f) < 0.01f, "rotated footprint keeps its area: " + facing);
            PortableFortressGeometry.ToLocalOffset(0f, -1f, facing, out float lx, out float lz);
            PortableFortressGeometry.RotateOffset(lx, lz, facing, out float wx, out float wz);
            Assert(Math.Abs(wx) < 0.001f && Math.Abs(wz + 1f) < 0.001f, "rotation round trip: " + facing);
        }

        // Model front (local -Z) must point the way the player looks.
        Assert(PortableFortressGeometry.FacingFromLook(0f, -1f) == FortressFacing.NegativeZ, "look -Z");
        Assert(PortableFortressGeometry.FacingFromLook(1f, 0f) == FortressFacing.PositiveX, "look +X");
        Assert(PortableFortressGeometry.FacingFromLook(0f, 1f) == FortressFacing.PositiveZ, "look +Z");
        Assert(PortableFortressGeometry.FacingFromLook(-1f, 0f) == FortressFacing.NegativeX, "look -X");
        Assert(Math.Abs(PortableFortressGeometry.Yaw(FortressFacing.PositiveX) - (float)(Math.PI / 2)) < 0.0001f, "yaw 90");
    }

    private static void FootprintAndAnchor()
    {
        foreach (FortressFacing facing in new[] { FortressFacing.NegativeZ, FortressFacing.PositiveX, FortressFacing.PositiveZ, FortressFacing.NegativeX })
        {
            HashSet<(int, int)> cells = new HashSet<(int, int)>(PortableFortressGeometry.FootprintCells(facing));
            Assert(cells.Count == PortableFortressGeometry.FootprintCellCount,
                "footprint is 143 unique cells: " + facing + " " + cells.Count);

            FortressDeploymentRules.AnchorCell(10, 20, facing, out int ax, out int az);
            Assert(Math.Abs(ax - 10) + Math.Abs(az - 20) == 7, "anchor is seven cells ahead: " + facing);

            List<(int X, int Z)> thresholds = new List<(int X, int Z)>();
            FortressDeploymentRules.ThresholdCellList(ax, az, facing, thresholds);
            Assert(thresholds.Count == FortressDeploymentRules.ThresholdCells, "three threshold cells");
            Assert(thresholds.All(t => cells.Contains((t.X - ax, t.Z - az))), "threshold cells are inside the footprint row");
            Assert(thresholds.Select(t => (t.X, t.Z)).Distinct().Count() == 3, "threshold cells are distinct");
        }

        // The door row is the rear-most footprint row: the anchor plus six cells backwards.
        FortressDeploymentRules.AnchorCell(0, 0, FortressFacing.NegativeZ, out int anchorX, out int anchorZ);
        Assert(anchorX == 0 && anchorZ == -7, "looking -Z puts the origin seven cells ahead");
        Assert(PortableFortressGeometry.FootprintCells(FortressFacing.NegativeZ).Contains((0, 6)), "rear row exists");
    }

    private static void PreflightStatuses()
    {
        const int anchorX = 100;
        const int anchorZ = 200;
        const int baseY = 64;

        FakeSite site = new FakeSite(anchorX, anchorZ, baseY);
        site.BuildFlatGround(FortressFacing.NegativeZ);

        FortressDeployResult ready = FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, false, false);
        Assert(ready.IsReady, "flat clear ground deploys: " + ready.Describe());

        site.BuildFlatGround(FortressFacing.NegativeZ);
        site.Occupy(anchorX + 2, baseY + 1, anchorZ);
        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, false, false).Status == FortressDeployStatus.Occupied,
            "non-air cell in the volume refuses deployment");

        site.BuildFlatGround(FortressFacing.NegativeZ);
        site.Occupy(anchorX, baseY + 8, anchorZ - 6);
        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, false, false).Status == FortressDeployStatus.Occupied,
            "top cell of the volume is checked");

        site.BuildFlatGround(FortressFacing.NegativeZ);
        site.RemoveGround(anchorX - 4, baseY - 1, anchorZ + 2);
        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, false, false).Status == FortressDeployStatus.UnsupportedGround,
            "a hole under the footprint refuses deployment");

        site.BuildFlatGround(FortressFacing.NegativeZ);
        site.HideChunk(anchorX + 5, anchorZ);
        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, false, false).Status == FortressDeployStatus.ChunkNotReady,
            "unloaded chunk refuses deployment");

        site.BuildFlatGround(FortressFacing.NegativeZ);
        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, false, false, false).Status == FortressDeployStatus.ModelUnavailable,
            "missing model refuses deployment before any write");

        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, false, true).Status == FortressDeployStatus.Duplicate,
            "duplicate instance refuses deployment");

        Assert(FortressDeploymentRules.Evaluate(anchorX, 250, anchorZ, FortressFacing.NegativeZ, site, true, false, false).Status == FortressDeployStatus.BadHeight,
            "deploying too high refuses");

        Assert(FortressDeploymentRules.Evaluate(anchorX, baseY, anchorZ, FortressFacing.NegativeZ, site, true, true, false).Status == FortressDeployStatus.BodyInTheWay,
            "player inside the footprint refuses deployment");
    }

    private static void BodyOverlapGate()
    {
        FortressRecord record = new FortressRecord
        {
            Id = "f1",
            Facing = FortressFacing.NegativeZ,
            BaseY = 64,
            AnchorX = 0,
            AnchorZ = 0,
            Revision = 1,
            State = FortressState.Deployed
        };

        Assert(FortressDeploymentRules.BodyOverlaps(0, 64, 0, FortressFacing.NegativeZ, 0.5f, 64f, 0.5f, 0.3f, 1.8f, 0.25f),
            "a body standing on the origin is inside");
        Assert(!FortressDeploymentRules.BodyOverlaps(0, 64, 0, FortressFacing.NegativeZ, 0.5f, 64f, 7.8f, 0.3f, 1.8f, 0.25f),
            "a body behind the rear wall is outside");
        Assert(FortressDeploymentRules.BodyOverlaps(0, 64, 0, FortressFacing.NegativeZ, 0.5f, 64f, 7.0f, 0.3f, 1.8f, 0.25f),
            "the safety margin catches a body at the door");

        FortressDeploymentRules.WorldToLocal(record, 0.5f, 7.0f, out float lx, out float lz);
        Assert(Math.Abs(lx) < 0.001f && Math.Abs(lz - 6.5f) < 0.001f, "rear wall maps to local z 6.5");
    }

    private static void TraversalPlan()
    {
        float previous = PortableFortressGeometry.FloorTop;
        for (int tread = 0; tread < PortableFortressGeometry.StairStepCount; tread++)
        {
            float top = PortableFortressGeometry.StairTreadTop(tread);
            Assert(top > previous, "treads rise monotonically");
            Assert(top - previous <= 0.46f, "tread rise matches the model");
            previous = top;
        }

        Assert(Math.Abs(PortableFortressGeometry.StairTreadTop(0) - (PortableFortressGeometry.FloorTop + PortableFortressGeometry.StairRise)) < 0.001f, "first tread top is one rise above the floor");
        Assert(PortableFortressGeometry.StairTreadAt(-4.0f) == 0, "stair front is tread 0");
        Assert(PortableFortressGeometry.StairTreadAt(-3.51f) == 0, "tread 0 covers the first run");
        Assert(PortableFortressGeometry.StairTreadAt(-3.49f) == 1, "tread 1 follows");
        Assert(PortableFortressGeometry.StairTreadAt(0.30f) == -1, "outside the stair run");

        Assert(FortressTraversalRules.TryGetTargetHeight(-3.25f, -3.75f, 0.36f, out float stairTarget), "stair target found");
        Assert(Math.Abs(stairTarget - PortableFortressGeometry.StairTreadTop(0)) < 0.001f, "stair target at the first tread");

        Assert(FortressTraversalRules.TryGetTargetHeight(3.15f, 4.375f, 3.84f, out float shaftBase), "shaft target found");
        Assert(Math.Abs(shaftBase - PortableFortressGeometry.DeckTop) < 0.01f, "shaft starts at the deck");
        Assert(FortressTraversalRules.TryGetTargetHeight(3.15f, 2.725f, 7.41f, out float shaftTop), "shaft top found");
        Assert(Math.Abs(shaftTop - PortableFortressGeometry.RoofTop) < 0.01f, "shaft ends at the roof");
        Assert(!FortressTraversalRules.TryGetTargetHeight(0f, 0f, 0.36f, out _), "open floor is not a path");

        // Entrance sill: a bounded 0.36 lift that only exists on the threshold cells.
        Assert(FortressTraversalRules.TryGetTargetHeight(0f, PortableFortressGeometry.EntrySillMaxZ - 0.1f, 0f, out float sill), "entrance sill target found");
        Assert(Math.Abs(sill - PortableFortressGeometry.FloorTop) < 0.001f, "entrance sill is the foundation top");
        Assert(!FortressTraversalRules.TryGetTargetHeight(0f, PortableFortressGeometry.EntrySillMaxZ + 0.4f, 0f, out _), "sill does not reach outside the envelope");
        Assert(!FortressTraversalRules.TryGetTargetHeight(2.6f, 6.0f, 0f, out _), "sill is limited to the doorway width");
        Assert(!FortressTraversalRules.TryGetTargetHeight(0f, 6.0f, 0.90f, out _), "sill is skipped once the player is above it");

        Assert(FortressTraversalRules.TryGetAssistedHeight(0.36f, 0f, 0.81f, 0.1f, out float lifted), "assist engages on a tread");
        Assert(Math.Abs(PortableFortressGeometry.StairTreadTop(PortableFortressGeometry.StairStepCount - 1) - PortableFortressGeometry.DeckTop) < 0.001f,
            "the last tread is flush with the level-2 deck");
        Assert(lifted > 0.36f && lifted <= 0.81f, "assist raises towards the tread");
        Assert(FortressTraversalRules.TryGetAssistedHeight(0.36f, 0f, 0.81f, 0.0166f, out float smallLift), "assist respects dt");
        Assert(smallLift - 0.36f <= FortressTraversalRules.MaxRiseSpeed * 0.0166f + 0.0001f, "assist rate is clamped");
        Assert(!FortressTraversalRules.TryGetAssistedHeight(0.36f, 1.5f, 0.81f, 0.1f, out _), "jumping is not assisted");
        Assert(!FortressTraversalRules.TryGetAssistedHeight(3.0f, 0f, 0.81f, 0.1f, out _), "far above target is not assisted");
        Assert(!FortressTraversalRules.TryGetAssistedHeight(3.84f, 0f, 0.81f, 0.1f, out _), "never pulls a player down");
    }

    private static void RecoveryGate()
    {
        FortressRecord deployed = new FortressRecord
        {
            Id = "f1",
            Facing = FortressFacing.NegativeZ,
            BaseY = 64,
            AnchorX = 0,
            AnchorZ = 0,
            Revision = 1,
            State = FortressState.Deployed
        };
        Assert(FortressRecoveryRules.Evaluate(deployed, false) == FortressRecoveryStatus.Reserved, "recovery is reserved, not faked");
        Assert(FortressRecoveryRules.Evaluate(deployed, true) == FortressRecoveryStatus.Busy, "recovery is refused during deployment");
        FortressRecord faulted = new FortressRecord { Id = "f2", Revision = 1, State = FortressState.Faulted };
        Assert(FortressRecoveryRules.Evaluate(faulted, false) == FortressRecoveryStatus.Faulted, "faulted instance refuses recovery");
        Assert(FortressRecoveryRules.Evaluate(null, false) == FortressRecoveryStatus.Unknown, "unknown core refuses recovery");
        Assert(FortressTransactionLimits.MaxTerrainCellsPerDeployment == 4, "terrain budget stays tiny");
        Assert(FortressTransactionLimits.SegmentsPerTick < FortressTransactionLimits.MaxSegmentsPerInstance, "deployment is batched");
    }

    private static bool ClearOfGlazing(FortressSegment segment)
    {
        return segment.Zone != FortressZone.Deck && segment.Zone != FortressZone.Glazing;
    }

    private static bool Overlaps(FortressBox box, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        return box.Intersects(new FortressBox(minX, minY, minZ, maxX, maxY, maxZ));
    }

    private static void Assert(bool condition, string message)
    {
        s_count++;
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }

    /// <summary>Terrain stand-in: a flat plate at baseY-1 with an empty volume above it.</summary>
    private sealed class FakeSite : IFortressSite
    {
        private readonly HashSet<(int, int, int)> m_solid = new HashSet<(int, int, int)>();
        private readonly HashSet<(int, int, int)> m_air = new HashSet<(int, int, int)>();
        private readonly HashSet<(int, int)> m_hiddenChunks = new HashSet<(int, int)>();
        private readonly int m_anchorX;
        private readonly int m_anchorZ;
        private readonly int m_baseY;

        public FakeSite(int anchorX, int anchorZ, int baseY)
        {
            m_anchorX = anchorX;
            m_anchorZ = anchorZ;
            m_baseY = baseY;
        }

        public void BuildFlatGround(FortressFacing facing)
        {
            m_solid.Clear();
            m_air.Clear();
            m_hiddenChunks.Clear();
            foreach ((int dx, int dz) in PortableFortressGeometry.FootprintCells(facing))
            {
                int x = m_anchorX + dx;
                int z = m_anchorZ + dz;
                m_solid.Add((x, m_baseY - 1, z));
                for (int y = m_baseY; y < m_baseY + FortressDeploymentRules.ClearanceHeight; y++)
                {
                    m_air.Add((x, y, z));
                }
            }
        }

        public void Occupy(int x, int y, int z)
        {
            m_air.Remove((x, y, z));
        }

        public void RemoveGround(int x, int y, int z)
        {
            m_solid.Remove((x, y, z));
        }

        public void HideChunk(int x, int z)
        {
            m_hiddenChunks.Add((x, z));
        }

        public bool IsChunkReady(int x, int y, int z)
        {
            return !m_hiddenChunks.Contains((x, z));
        }

        public int GetContents(int x, int y, int z)
        {
            return m_air.Contains((x, y, z)) ? 0 : 1;
        }

        public bool IsSolid(int x, int y, int z)
        {
            return m_solid.Contains((x, y, z));
        }
    }
}
