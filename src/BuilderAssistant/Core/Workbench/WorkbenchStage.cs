using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Workbench
{
    /// <summary>Where the guided workbench flow currently is. Pure data, no game coupling.</summary>
    internal enum WorkbenchStage
    {
        /// <summary>Nothing chosen yet.</summary>
        Idle,
        /// <summary>A building is selected and its parameters can be tweaked / regenerated.</summary>
        Configure,
        /// <summary>A read-only footprint preview exists and awaits confirmation or tweak.</summary>
        Previewed,
    }

    /// <summary>
    /// The outcome of one workbench command: whether the request was accepted, an optional
    /// human reason, and the stage the machine is now in. Commands NEVER throw and a rejected
    /// command NEVER mutates the last valid blueprint (that guarantee is what makes
    /// "校验失败不改变当前有效建筑" and "改参后确认失效" provable offline).
    /// </summary>
    internal sealed class WorkbenchStatus
    {
        public readonly bool Accepted;
        public readonly string Reason;
        public readonly WorkbenchStage Stage;

        /// <summary>True when the command changed the blueprint that would be built, so any
        /// stale preview/confirmation must be discarded.</summary>
        public readonly bool BlueprintChanged;

        public WorkbenchStatus(bool accepted, string reason, WorkbenchStage stage, bool blueprintChanged)
        {
            Accepted = accepted;
            Reason = reason;
            Stage = stage;
            BlueprintChanged = blueprintChanged;
        }

        public static WorkbenchStatus Ok(string reason, WorkbenchStage stage, bool changed)
        {
            return new WorkbenchStatus(true, reason, stage, changed);
        }

        public static WorkbenchStatus Fail(string reason)
        {
            return new WorkbenchStatus(false, reason, WorkbenchStage.Configure, false);
        }
    }

    /// <summary>
    /// Everything needed to actually start a build once the pure gate approves it. The caller
    /// (WorldEditRuntime) hands this straight to the existing, unchanged PlanAndStart path, so
    /// the workbench adds NO second execution pipeline - it only decides whether the player is
    /// allowed to reach it.
    /// </summary>
    internal sealed class BuildStartDescriptor
    {
        public readonly Blueprint Blueprint; // effective (rotation already applied)
        public readonly CellPos Origin;
        public readonly string Name;
        public readonly bool ClearSoftObstructions;

        public BuildStartDescriptor(Blueprint blueprint, CellPos origin, string name, bool clearSoft)
        {
            Blueprint = blueprint;
            Origin = origin;
            Name = name;
            ClearSoftObstructions = clearSoft;
        }
    }

    /// <summary>Which parameter knob a sky-tap cycles while in Configure. Ordered for the UI.</summary>
    internal enum WorkbenchKnob
    {
        Building,
        Width,
        Length,
        Floors,
        WallHeight,
        Roof,
        Seed,
        Rotation,
    }
}
