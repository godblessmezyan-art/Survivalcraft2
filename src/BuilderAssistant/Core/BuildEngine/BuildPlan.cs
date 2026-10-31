using System.Collections.Generic;
using BuilderAssistant.Core.Transactions;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.BuildEngine
{
    /// <summary>Result of validating a blueprint at a world anchor (no writes yet).</summary>
    internal sealed class BuildPlan
    {
        public bool Success { get; private set; }
        public string FailureReason { get; private set; }
        public List<PlannedChange> Changes { get; private set; }

        /// <summary>
        /// Every cell that failed the editability check during planning (empty on
        /// success, or when the failure had nothing to do with editability). Carries
        /// the precise reason + chunk context so the caller can explain WHICH check
        /// on WHICH chunk failed instead of the old collapsed "目标区块未加载或越界".
        /// </summary>
        public List<CellEditability> EditabilityFailures { get; private set; }

        /// <summary>
        /// True when the plan failed ONLY because chunks are not ready yet (missing /
        /// not-valid / not-loaded), i.e. waiting for the engine to stream them in
        /// could plausibly succeed. False for hard errors (invalid Y, invalid cell
        /// extent, terrain null, non-air, unknown material), which must not be retried.
        /// </summary>
        public bool AllEditabilityFailuresRecoverable { get; private set; }

        /// <summary>
        /// v0.6.3 clear-soft mode: how many non-air target cells are walk-through
        /// plants/snow that this plan will REPLACE (only meaningful on a successful
        /// plan with clear-soft enabled). Solid terrain / player structures are never
        /// counted here because they abort the plan. The runtime surfaces this to the
        /// user in the second-tap confirmation so clearing vegetation is always visible.
        /// </summary>
        public int SoftClearedCount { get; private set; }

        /// <summary>
        /// v0.6.3: true when the ONLY thing blocking the plan is soft obstructions
        /// (grass/flowers/tall grass/dead bushes/snow layers) that clear-soft mode could
        /// replace. Hard conflicts (solid terrain, player structures) and unknown
        /// materials leave this false, so the runtime only ever offers the "clear
        /// vegetation" confirmation for the genuinely safe case.
        /// </summary>
        public bool FailedBySoftObstruction { get; private set; }

        private BuildPlan(
            bool success,
            string reason,
            List<PlannedChange> changes,
            List<CellEditability> editabilityFailures,
            bool allRecoverable,
            int softClearedCount,
            bool softFail)
        {
            Success = success;
            FailureReason = reason;
            Changes = changes;
            EditabilityFailures = editabilityFailures ?? new List<CellEditability>();
            AllEditabilityFailuresRecoverable = allRecoverable;
            SoftClearedCount = softClearedCount;
            FailedBySoftObstruction = softFail;
        }

        public static BuildPlan Fail(string reason)
        {
            return new BuildPlan(false, reason, null, null, false, 0, false);
        }

        /// <summary>
        /// Failure caused by one or more non-editable target cells. <c>reason</c> is
        /// a concise human summary; the machine-readable breakdown lives in
        /// <c>EditabilityFailures</c> / <c>AllEditabilityFailuresRecoverable</c>.
        /// </summary>
        public static BuildPlan Fail(
            string reason,
            List<CellEditability> editabilityFailures,
            bool allRecoverable)
        {
            return new BuildPlan(false, reason, null, editabilityFailures, allRecoverable, 0, false);
        }

        /// <summary>
        /// v0.6.3: failure caused purely by soft obstructions (plants/snow) sitting in
        /// the footprint while clear-soft is off. The runtime may offer to enable
        /// clear-soft and re-plan; solid/structure conflicts never come through here.
        /// </summary>
        public static BuildPlan FailSoftBlocked(string reason)
        {
            return new BuildPlan(false, reason, null, null, false, 0, true);
        }

        public static BuildPlan Ok(List<PlannedChange> changes)
        {
            return Ok(changes, 0);
        }

        public static BuildPlan Ok(List<PlannedChange> changes, int softClearedCount)
        {
            return new BuildPlan(true, null, changes, null, false, softClearedCount, false);
        }
    }

    /// <summary>A single validated, absolute write the engine is allowed to make.</summary>
    internal readonly struct PlannedChange
    {
        public readonly CellPos Absolute;
        public readonly int Value;
        public readonly BlockFamily Family;
        public readonly int Data;
        public readonly string Context;
        public bool HasSupport { get { return AttachmentRules.RequiresSupport(Family) && !(Family == BlockFamily.Falling && Absolute.Y == 0); } }

        public PlannedChange(CellPos absolute, int value, BlockFamily family = BlockFamily.Cube, int data = 0, string context = null)
        {
            Absolute = absolute;
            Value = value;
            Family = family;
            Data = data;
            Context = context;
        }
    }
}
