using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Workbench
{
    /// <summary>
    /// A game-free, terrain-independent description of the FOOTPRINT a build would occupy
    /// at a chosen anchor, together with the read-only safety verdict the planner produced
    /// for that exact blueprint+anchor. This is the Phase-1 preview: it is derived from the
    /// SAME blueprint cells the build will write, so the preview area can never diverge from
    /// the final build area by construction (and a test asserts that equality). It carries
    /// NO world mutation - producing it only ever reads terrain through <c>BuildPlanner</c>.
    ///
    /// The model is intentionally data-only so the future on-device renderer (a native
    /// outline/gizmo once the Android render API is verified, or today the toast fallback)
    /// can present it without the Core knowing anything about the game.
    /// </summary>
    internal sealed class BuildPreviewModel
    {
        public readonly CellPos Origin;
        public readonly int SizeX;
        public readonly int SizeY;
        public readonly int SizeZ;

        // Absolute footprint rectangle (X/Z ground plane), independent of which cells are solid.
        public readonly int MinX;
        public readonly int MaxX;
        public readonly int MinZ;
        public readonly int MaxZ;

        /// <summary>The four ground-level corners of the footprint rectangle.</summary>
        public readonly CellPos[] Corners;
        /// <summary>How many distinct cells form the perimeter outline of the footprint.</summary>
        public readonly int BorderCellCount;
        /// <summary>Footprint ground area in cells (Width x Length), the "面积" shown to the player.</summary>
        public readonly int FootprintArea;

        public readonly int HeightMinY;
        public readonly int HeightMaxY;

        // Main-door orientation (the "主门朝向"). -1 when the structure has no door.
        public readonly int DoorFacingRot;
        public readonly string DoorFacingLabel;
        public readonly bool HasDoor;

        // Read-only planner verdict for THIS blueprint at THIS anchor against the live terrain.
        public readonly bool Allowed;
        public readonly string DeniedReason;
        public readonly bool RecoverableByWaiting;
        public readonly bool BlockedBySoftObstruction;

        // Counts that tie the preview to the eventual build.
        public readonly int BlueprintCellCount;
        public readonly int PlannedWriteCount;   // == BlueprintCellCount when allowed on clear air
        public readonly int SoftClearCount;

        public BuildPreviewModel(
            CellPos origin, int sizeX, int sizeY, int sizeZ,
            int minX, int maxX, int minZ, int maxZ,
            CellPos[] corners, int borderCellCount, int footprintArea,
            int heightMinY, int heightMaxY,
            int doorFacingRot, string doorFacingLabel, bool hasDoor,
            bool allowed, string deniedReason, bool recoverable, bool softBlocked,
            int blueprintCellCount, int plannedWriteCount, int softClearCount)
        {
            Origin = origin;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            Corners = corners ?? new CellPos[0];
            BorderCellCount = borderCellCount;
            FootprintArea = footprintArea;
            HeightMinY = heightMinY;
            HeightMaxY = heightMaxY;
            DoorFacingRot = doorFacingRot;
            DoorFacingLabel = doorFacingLabel;
            HasDoor = hasDoor;
            Allowed = allowed;
            DeniedReason = deniedReason;
            RecoverableByWaiting = recoverable;
            BlockedBySoftObstruction = softBlocked;
            BlueprintCellCount = blueprintCellCount;
            PlannedWriteCount = plannedWriteCount;
            SoftClearCount = softClearCount;
        }

        /// <summary>
        /// The invariant the acceptance test enforces: the preview describes EXACTLY the
        /// build envelope (same size, same anchor, and - when placement is allowed on clear
        /// ground - the same number of writes as blueprint cells). If this ever returns false
        /// the preview would be lying about what gets built, which is forbidden.
        /// </summary>
        public bool MatchesBuildEnvelope(Blueprint effectiveBlueprint, int plannedChangeCount)
        {
            if (effectiveBlueprint == null)
            {
                return false;
            }
            bool sizeOk = SizeX == effectiveBlueprint.SizeX &&
                          SizeY == effectiveBlueprint.SizeY &&
                          SizeZ == effectiveBlueprint.SizeZ;
            bool cellOk = BlueprintCellCount == effectiveBlueprint.CellCount;
            bool writeOk = PlannedWriteCount == plannedChangeCount;
            // Area must equal the X/Z rectangle the blueprint spans.
            bool areaOk = FootprintArea == SizeX * SizeZ &&
                          (MaxX - MinX + 1) == SizeX &&
                          (MaxZ - MinZ + 1) == SizeZ;
            return sizeOk && cellOk && writeOk && areaOk;
        }

        /// <summary>One-line human summary for the toast / future dialog.</summary>
        public string Summarize()
        {
            string verdict = Allowed
                ? ("允许施工（" + PlannedWriteCount + " 格写入" + (SoftClearCount > 0 ? "，将清理 " + SoftClearCount + " 处植被" : "") + "）")
                : ("禁止施工：" + DeniedReason);
            string door = HasDoor ? ("，主门朝向 " + DoorFacingLabel) : "";
            return "占地 " + SizeX + "×" + SizeZ + " 面积 " + FootprintArea + " 格，高 " + SizeY +
                   " 层（Y " + HeightMinY + ".." + HeightMaxY + "），共 " + BlueprintCellCount + " 方块" + door +
                   "。" + verdict;
        }
    }
}
