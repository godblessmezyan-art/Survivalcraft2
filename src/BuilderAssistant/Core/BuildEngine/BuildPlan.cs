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

        private BuildPlan(bool success, string reason, List<PlannedChange> changes)
        {
            Success = success;
            FailureReason = reason;
            Changes = changes;
        }

        public static BuildPlan Fail(string reason)
        {
            return new BuildPlan(false, reason, null);
        }

        public static BuildPlan Ok(List<PlannedChange> changes)
        {
            return new BuildPlan(true, null, changes);
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
