using System;
using System.Collections.Generic;
using System.Globalization;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>Outcome of a deployment preflight.</summary>
    public enum FortressDeployStatus
    {
        /// <summary>The site passes every check.</summary>
        Ready = 0,
        /// <summary>One or more terrain chunks needed by the footprint are not loaded.</summary>
        ChunkNotReady = 1,
        /// <summary>A footprint column has no solid ground directly under the deployment plane.</summary>
        UnsupportedGround = 2,
        /// <summary>A footprint cell inside the fortress volume is not empty.</summary>
        Occupied = 3,
        /// <summary>A player or creature stands inside the footprint.</summary>
        BodyInTheWay = 4,
        /// <summary>The fortress model could not be loaded.</summary>
        ModelUnavailable = 5,
        /// <summary>The deployment plane is outside the supported world height.</summary>
        BadHeight = 6,
        /// <summary>An instance with the same identity is already registered.</summary>
        Duplicate = 7
    }

    /// <summary>Result of a deployment preflight.</summary>
    public sealed class FortressDeployResult
    {
        private FortressDeployResult(FortressDeployStatus status, int x, int y, int z)
        {
            Status = status;
            CellX = x;
            CellY = y;
            CellZ = z;
        }

        /// <summary>Overall status.</summary>
        public FortressDeployStatus Status { get; }
        /// <summary>First offending cell, when the status is cell specific.</summary>
        public int CellX { get; }
        /// <summary>First offending cell, when the status is cell specific.</summary>
        public int CellY { get; }
        /// <summary>First offending cell, when the status is cell specific.</summary>
        public int CellZ { get; }
        /// <summary>True when the site is deployable.</summary>
        public bool IsReady => Status == FortressDeployStatus.Ready;

        /// <summary>Creates a result.</summary>
        public static FortressDeployResult Create(FortressDeployStatus status, int x = 0, int y = 0, int z = 0)
        {
            return new FortressDeployResult(status, x, y, z);
        }

        /// <summary>Short operator readable description including the offending cell.</summary>
        public string Describe()
        {
            if (Status == FortressDeployStatus.Ready)
            {
                return "可部署";
            }

            string where = string.Format(CultureInfo.InvariantCulture, " ({0},{1},{2})", CellX, CellY, CellZ);
            switch (Status)
            {
                case FortressDeployStatus.ChunkNotReady:
                    return "区块尚未加载完成" + where;
                case FortressDeployStatus.UnsupportedGround:
                    return "脚下没有完整实地" + where;
                case FortressDeployStatus.Occupied:
                    return "占地内有非空气方块" + where;
                case FortressDeployStatus.BodyInTheWay:
                    return "占地内有玩家或生物";
                case FortressDeployStatus.ModelUnavailable:
                    return "堡垒模型未加载";
                case FortressDeployStatus.BadHeight:
                    return "此处高度不足以展开堡垒";
                case FortressDeployStatus.Duplicate:
                    return "该堡垒实例已存在";
                default:
                    return "未知状态";
            }
        }
    }

    /// <summary>
    /// Read-only terrain view used by the preflight. Implemented by the runtime on top of
    /// <c>SubsystemTerrain</c>; implemented by tests as a plain array so the rules can be
    /// proven without the game.
    /// </summary>
    public interface IFortressSite
    {
        /// <summary>True when the chunk containing the cell is loaded far enough to trust its contents.</summary>
        bool IsChunkReady(int x, int y, int z);

        /// <summary>Block content id; 0 means air.</summary>
        int GetContents(int x, int y, int z);

        /// <summary>True when the block at the cell provides a stable collision surface.</summary>
        bool IsSolid(int x, int y, int z);
    }

    /// <summary>Lifecycle state of one fortress instance.</summary>
    public enum FortressState
    {
        /// <summary>Preflight passed; pieces are being created.</summary>
        Deploying = 0,
        /// <summary>Every piece exists and the instance is authoritative.</summary>
        Deployed = 1,
        /// <summary>A step failed; the instance refuses further work until repaired.</summary>
        Faulted = 2
    }

    /// <summary>Persisted identity and placement of one fortress instance.</summary>
    public sealed class FortressRecord
    {
        /// <summary>Stable instance id, generated once at deployment.</summary>
        public string Id { get; set; }
        /// <summary>Quarter turn.</summary>
        public FortressFacing Facing { get; set; }
        /// <summary>World Y of the deployment plane (the ground surface the fortress stands on).</summary>
        public int BaseY { get; set; }
        /// <summary>World cell X of the fortress origin.</summary>
        public int AnchorX { get; set; }
        /// <summary>World cell Z of the fortress origin.</summary>
        public int AnchorZ { get; set; }
        /// <summary>World cell of the control core block.</summary>
        public int CoreX { get; set; }
        /// <summary>World cell of the control core block.</summary>
        public int CoreY { get; set; }
        /// <summary>World cell of the control core block.</summary>
        public int CoreZ { get; set; }
        /// <summary>Monotonic deployment revision, used to make retries idempotent.</summary>
        public int Revision { get; set; }
        /// <summary>Current lifecycle state.</summary>
        public FortressState State { get; set; }

        /// <summary>World-space position of the fortress origin.</summary>
        public void Origin(out float x, out float y, out float z)
        {
            x = AnchorX + 0.5f;
            y = BaseY;
            z = AnchorZ + 0.5f;
        }

        /// <summary>True when the record has a usable identity.</summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Id) && Revision > 0;
        }
    }

    /// <summary>One terrain cell captured before the fortress wrote it.</summary>
    public readonly struct FortressCellWrite
    {
        /// <summary>Creates a journal entry.</summary>
        public FortressCellWrite(int x, int y, int z, int previousValue, int newValue)
        {
            X = x;
            Y = y;
            Z = z;
            PreviousValue = previousValue;
            NewValue = newValue;
        }

        /// <summary>Cell X.</summary>
        public int X { get; }
        /// <summary>Cell Y.</summary>
        public int Y { get; }
        /// <summary>Cell Z.</summary>
        public int Z { get; }
        /// <summary>Block value before the fortress touched the cell.</summary>
        public int PreviousValue { get; }
        /// <summary>Block value the fortress installed.</summary>
        public int NewValue { get; }
    }

    /// <summary>
    /// Placement, preflight and traversal rules. No engine dependency: the runtime supplies
    /// terrain samples, tests supply arrays.
    /// </summary>
    public static class FortressDeploymentRules
    {
        /// <summary>Cells from the clicked cell to the fortress origin along the front axis.</summary>
        public const int AnchorOffsetCells = 7;

        /// <summary>Cells around the door that receive a hidden 0.36-high threshold block.</summary>
        public const int ThresholdCells = 3;

        /// <summary>World cell of the fortress origin for a clicked ground cell.</summary>
        public static void AnchorCell(int clickX, int clickZ, FortressFacing facing, out int anchorX, out int anchorZ)
        {
            PortableFortressGeometry.RotateOffset(0f, -AnchorOffsetCells, facing, out float ox, out float oz);
            anchorX = clickX + (int)Math.Round(ox);
            anchorZ = clickZ + (int)Math.Round(oz);
        }

        /// <summary>
        /// Threshold cells sit on the rear-most footprint row across the door opening. They are
        /// the only terrain the fortress installs besides the core, and they exist because a
        /// body collider cannot be stepped onto: only terrain columns feed
        /// <c>ComponentBody.HandleSmoothRise</c>.
        /// </summary>
        public static void ThresholdCellList(int anchorX, int anchorZ, FortressFacing facing, List<(int X, int Z)> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            for (int across = -1; across <= 1; across++)
            {
                PortableFortressGeometry.RotateOffset(across, 6f, facing, out float ox, out float oz);
                result.Add((anchorX + (int)Math.Round(ox), anchorZ + (int)Math.Round(oz)));
            }
        }

        /// <summary>
        /// Full preflight. Checks chunk readiness, ground support, clearance of the whole
        /// 11 x 13 x 9 volume, world height and instance duplication. Overwriting player
        /// builds is never allowed: any non-air cell in the volume refuses the deployment.
        /// </summary>
        public static FortressDeployResult Evaluate(
            int anchorX,
            int baseY,
            int anchorZ,
            FortressFacing facing,
            IFortressSite site,
            bool modelAvailable,
            bool bodyInTheWay,
            bool duplicateInstance)
        {
            if (site == null)
            {
                throw new ArgumentNullException(nameof(site));
            }

            if (!modelAvailable)
            {
                return FortressDeployResult.Create(FortressDeployStatus.ModelUnavailable);
            }

            if (duplicateInstance)
            {
                return FortressDeployResult.Create(FortressDeployStatus.Duplicate);
            }

            int top = baseY + ClearanceHeight;
            if (baseY < 2 || top >= 255)
            {
                return FortressDeployResult.Create(FortressDeployStatus.BadHeight, anchorX, baseY, anchorZ);
            }

            foreach ((int dx, int dz) in PortableFortressGeometry.FootprintCells(facing))
            {
                int x = anchorX + dx;
                int z = anchorZ + dz;
                if (!site.IsChunkReady(x, baseY, z) || !site.IsChunkReady(x, baseY - 1, z))
                {
                    return FortressDeployResult.Create(FortressDeployStatus.ChunkNotReady, x, baseY, z);
                }

                if (!site.IsSolid(x, baseY - 1, z))
                {
                    return FortressDeployResult.Create(FortressDeployStatus.UnsupportedGround, x, baseY - 1, z);
                }

                for (int y = baseY; y < top; y++)
                {
                    if (site.GetContents(x, y, z) != 0)
                    {
                        return FortressDeployResult.Create(FortressDeployStatus.Occupied, x, y, z);
                    }
                }
            }

            if (bodyInTheWay)
            {
                return FortressDeployResult.Create(FortressDeployStatus.BodyInTheWay, anchorX, baseY, anchorZ);
            }

            return FortressDeployResult.Create(FortressDeployStatus.Ready);
        }

        /// <summary>Vertical cells that must be clear above the deployment plane.</summary>
        public const int ClearanceHeight = 9;

        /// <summary>
        /// True when a body box at the given world position would overlap the fortress volume
        /// once inflated by <paramref name="margin"/>.
        /// </summary>
        public static bool BodyOverlaps(
            int anchorX,
            int baseY,
            int anchorZ,
            FortressFacing facing,
            float bodyX,
            float bodyY,
            float bodyZ,
            float halfWidth,
            float height,
            float margin)
        {
            PortableFortressGeometry.ToLocalOffset(
                bodyX - (anchorX + 0.5f),
                bodyZ - (anchorZ + 0.5f),
                facing,
                out float lx,
                out float lz);
            if (lx < -PortableFortressGeometry.HalfWidth - margin - halfWidth
                || lx > PortableFortressGeometry.HalfWidth + margin + halfWidth)
            {
                return false;
            }

            if (lz < -PortableFortressGeometry.HalfLength - margin - halfWidth
                || lz > PortableFortressGeometry.HalfLength + margin + halfWidth)
            {
                return false;
            }

            return bodyY + height > baseY - margin && bodyY < baseY + PortableFortressGeometry.EnvelopeHeight + margin;
        }

        /// <summary>Converts a world point into fortress-local coordinates.</summary>
        public static void WorldToLocal(
            FortressRecord record,
            float worldX,
            float worldZ,
            out float localX,
            out float localZ)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            PortableFortressGeometry.ToLocalOffset(
                worldX - (record.AnchorX + 0.5f),
                worldZ - (record.AnchorZ + 0.5f),
                record.Facing,
                out localX,
                out localZ);
        }
    }

    /// <summary>
    /// Vertical circulation inside the fortress. The engine only feeds terrain columns into
    /// <c>ComponentBody.HandleSmoothRise</c>, so a body-backed stair tread can never be walked
    /// onto. The fortress therefore carries its own bounded rise for the two body-backed
    /// vertical paths; every other surface is either terrain or unreachable.
    /// </summary>
    public static class FortressTraversalRules
    {
        /// <summary>Maximum rise speed, matched to the native smooth-rise clamp of 4.5 m/s.</summary>
        public const float MaxRiseSpeed = 4.5f;

        /// <summary>How far below a target surface the assist still engages.</summary>
        public const float EngageBelow = 0.55f;

        /// <summary>How far above a target surface the assist still engages.</summary>
        public const float EngageAbove = 0.30f;

        /// <summary>Vertical speed above which the player counts as jumping and is not assisted.</summary>
        public const float JumpSpeedCutoff = 0.80f;

        /// <summary>Height of the walking surface at a local point, or null when the point is off the paths.</summary>
        public static bool TryGetTargetHeight(float localX, float localZ, float feetY, out float targetY)
        {
            targetY = 0f;

            bool onStair = localX >= PortableFortressGeometry.StairMinX - 0.02f
                && localX <= PortableFortressGeometry.StairMaxX + 0.02f
                && localZ >= PortableFortressGeometry.StairMinZ - 0.02f
                && localZ <= PortableFortressGeometry.StairMaxZ + 0.02f;
            if (onStair)
            {
                int tread = PortableFortressGeometry.StairTreadAt(localZ);
                if (tread < 0)
                {
                    return false;
                }

                targetY = PortableFortressGeometry.StairTreadTop(tread);
                return true;
            }

            bool onShaft = localX >= PortableFortressGeometry.ShaftMinX
                && localX <= PortableFortressGeometry.ShaftMaxX
                && localZ >= PortableFortressGeometry.ShaftMinZ
                && localZ <= PortableFortressGeometry.ShaftMaxZ;
            if (onShaft)
            {
                // The reserved shaft is a compact ramp between the level-2 deck and the roof:
                // rise from the deck at the rear edge to the hatch at the front edge.
                float t = (PortableFortressGeometry.ShaftMaxZ - localZ)
                    / (PortableFortressGeometry.ShaftMaxZ - PortableFortressGeometry.ShaftMinZ);
                t = t < 0f ? 0f : (t > 1f ? 1f : t);
                targetY = PortableFortressGeometry.DeckTop
                    + t * (PortableFortressGeometry.RoofTop - PortableFortressGeometry.DeckTop);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Decides whether the rise assist should move the player this tick and how far.
        /// Only lifts; never pulls a falling or jumping player down, so gravity and jumps
        /// keep working inside the fortress.
        /// </summary>
        public static bool TryGetAssistedHeight(
            float feetY,
            float velocityY,
            float targetY,
            float dt,
            out float newY)
        {
            newY = feetY;
            if (targetY <= feetY + 0.02f)
            {
                return false;
            }

            if (feetY < targetY - EngageBelow || feetY > targetY + EngageAbove)
            {
                return false;
            }

            if (Math.Abs(velocityY) > JumpSpeedCutoff)
            {
                return false;
            }

            float step = MaxRiseSpeed * (dt <= 0f ? 0.0166f : dt);
            newY = Math.Min(targetY, feetY + step);
            return newY > feetY;
        }
    }

    /// <summary>Recovery (pack-up) interface state. Teardown is reserved, never faked.</summary>
    public enum FortressRecoveryStatus
    {
        /// <summary>Recovery is not implemented in this build.</summary>
        Reserved = 0,
        /// <summary>A deployment is still in flight.</summary>
        Busy = 1,
        /// <summary>The instance is faulted and must be inspected first.</summary>
        Faulted = 2,
        /// <summary>Unknown core or instance.</summary>
        Unknown = 3
    }

    /// <summary>Recovery gate. Deliberately refuses to remove anything in this build.</summary>
    public static class FortressRecoveryRules
    {
        /// <summary>
        /// Evaluates a recovery request. A real teardown needs an inventory flush, occupancy
        /// checks and a captured terrain journal; until those exist the core must refuse
        /// rather than half-remove a fortress and lose the player's items.
        /// </summary>
        public static FortressRecoveryStatus Evaluate(FortressRecord record, bool deploymentInFlight)
        {
            if (record == null || !record.IsValid())
            {
                return FortressRecoveryStatus.Unknown;
            }

            if (deploymentInFlight || record.State == FortressState.Deploying)
            {
                return FortressRecoveryStatus.Busy;
            }

            if (record.State == FortressState.Faulted)
            {
                return FortressRecoveryStatus.Faulted;
            }

            return FortressRecoveryStatus.Reserved;
        }
    }

    /// <summary>Named guard rails for the deployment transaction.</summary>
    public static class FortressTransactionLimits
    {
        /// <summary>Terrain cells written by one deployment (three thresholds plus the core).</summary>
        public const int MaxTerrainCellsPerDeployment = 4;

        /// <summary>Proxy entities created per simulation tick.</summary>
        public const int SegmentsPerTick = 8;

        /// <summary>Hard cap on proxies for one instance; refuses rather than truncating.</summary>
        public const int MaxSegmentsPerInstance = PortableFortressMaxSegments;

        private const int PortableFortressMaxSegments = 80;
    }
}
