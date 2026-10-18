using System.Collections.Generic;
using BuilderAssistant.Core.Transactions;

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

        private BuildPlan(
            bool success,
            string reason,
            List<PlannedChange> changes,
            List<CellEditability> editabilityFailures,
            bool allRecoverable)
        {
            Success = success;
            FailureReason = reason;
            Changes = changes;
            EditabilityFailures = editabilityFailures ?? new List<CellEditability>();
            AllEditabilityFailuresRecoverable = allRecoverable;
        }

        public static BuildPlan Fail(string reason)
        {
            return new BuildPlan(false, reason, null, null, false);
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
            return new BuildPlan(false, reason, null, editabilityFailures, allRecoverable);
        }

        public static BuildPlan Ok(List<PlannedChange> changes)
        {
            return new BuildPlan(true, null, changes, null, false);
        }
    }

    /// <summary>A single validated, absolute write the engine is allowed to make.</summary>
    internal readonly struct PlannedChange
    {
        public readonly CellPos Absolute;
        public readonly int Value;

        public PlannedChange(CellPos absolute, int value)
        {
            Absolute = absolute;
            Value = value;
        }
    }
}
