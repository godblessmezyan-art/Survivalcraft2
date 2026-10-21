using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// Outcome of one <see cref="TreehouseAssembly.Advance"/> step. Everything except
    /// <see cref="TreehouseAdvanceResult.Failed"/> and <see cref="TreehouseAdvanceResult.Cancelled"/>
    /// is non-terminal: the caller simply drives again on the next game tick.
    /// </summary>
    internal enum TreehouseAdvanceResult
    {
        /// Made (or is making) ordinary progress on the current phase; nothing to announce.
        Steady,
        /// A phase finished, was recorded for undo, and more phases remain.
        PhaseCompleted,
        /// The final phase finished; the whole treehouse is now in the world.
        AllCompleted,
        /// The current phase is not fully chunk-ready; write nothing and retry later.
        WaitingChunks,
        /// The current phase is blocked only by walk-through plants/snow. The caller may
        /// set <see cref="TreehouseAssembly.ClearSoft"/> and drive again, or abort.
        BlockedBySoft,
        /// A phase hard-failed (or planning hard-failed). Later phases will NOT start.
        Failed,
        /// The player cancelled. The in-flight phase was rolled back; later phases will NOT start.
        Cancelled,
    }

    /// <summary>
    /// M4 phased in-world assembler. It takes the <see cref="TreehousePart"/> slices produced by
    /// <see cref="TreehouseBlueprintSet.Build"/> and stamps them onto ONE master anchor, phase by
    /// phase, so a single confirmed tap can grow the entire giant treehouse.
    ///
    /// Design guarantees (AGENTS.md):
    /// - It reuses the existing plan/execute/undo pipeline VERBATIM. Each phase is planned with
    ///   <c>BuildPlanner.Plan(blueprint, origin, resolver, gateway, allowOverride, clearSoft)</c>
    ///   at origin = anchor + part.OffsetInMaster, executed with a <see cref="BuildTask"/> advancing
    ///   at most the existing MaxBlocksPerBatch (32) cells per call, and recorded into a shared
    ///   <see cref="TransactionHistory"/>. No limit is raised.
    /// - Phases run strictly one at a time (a single in-flight <see cref="BuildTask"/>), so two
    ///   batches never interleave and every absolute cell is anchor + OffsetInMaster + relative.
    /// - Default no-overwrite: allowOverride is always false, and a phase that hits a hard
    ///   obstruction stops the whole assembly instead of cascading partial writes.
    /// - Cancel rolls back ONLY the current in-flight phase; already-completed phases stay and
    ///   remain individually undoable through the shared history.
    ///
    /// This class is game-free and deterministic, so the whole orchestration is provable offline.
    /// </summary>
    internal sealed class TreehouseAssembly
    {
        private readonly TreehousePart[] _parts;
        private readonly CellPos _anchor;
        private readonly IBlockValueResolver _resolver;
        private readonly ITerrainGateway _gateway;
        private readonly TransactionHistory _history;

        private int _phaseIndex;
        private BuildTask _current;
        private int _appliedCompleted;
        private int _totalPlanned;

        private bool _cancelRequested;
        private bool _halted;
        private bool _done;
        private TreehouseAdvanceResult _haltedResult;

        public TreehouseAssembly(TreehousePart[] parts, CellPos anchor,
            IBlockValueResolver resolver, ITerrainGateway gateway, TransactionHistory history)
        {
            _parts = parts != null ? parts : new TreehousePart[0];
            _anchor = anchor;
            _resolver = resolver;
            _gateway = gateway;
            _history = history;

            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null && _parts[i].Blueprint != null)
                {
                    _totalPlanned += _parts[i].Blueprint.CellCount;
                }
            }

            // An empty part list is an immediate, clean completion (never a silent hang).
            if (_parts.Length == 0)
            {
                _done = true;
                LastMessage = "没有可装配的阶段（蓝图集为空）。";
            }
        }

        /// <summary>When true, a phase blocked only by plants/snow will clear them and continue.
        /// Only ever set by an explicit player confirmation upstream; never the default.</summary>
        public bool ClearSoft { get; set; }

        /// <summary>v0.13.2 explicit "clear obstacles within footprint" opt-in. When true each
        /// phase is planned and executed with allowOverride, so EVERY cell the blueprint writes
        /// into is replaced unconditionally - solid terrain, player structures and fluids alike -
        /// with NO clearance margin beyond the exact footprint, and no per-phase soft/hard abort.
        /// Only ever set by an explicit on-device toggle upstream; never the default. Undo still
        /// captures each original value (including the displaced fluid) before writing.</summary>
        public bool ClearObstacles { get; set; }

        /// <summary>Human-readable status for the last step, used by the caller to toast.</summary>
        public string LastMessage { get; private set; }

        /// <summary>The master anchor every part is stamped relative to.</summary>
        public CellPos Anchor { get { return _anchor; } }

        public int PhaseCount { get { return _parts.Length; } }

        /// <summary>Phases fully completed and recorded so far (also the next phase index).</summary>
        public int CompletedPhaseCount { get { return _phaseIndex; } }

        /// <summary>Total cells across all phases (the planned grand total).</summary>
        public int TotalPlannedCells { get { return _totalPlanned; } }

        /// <summary>Cells currently sitting in the world thanks to this assembly (completed
        /// phases plus any partial progress of the in-flight phase).</summary>
        public int AppliedSoFar
        {
            get { return _appliedCompleted + (_current != null ? _current.AppliedCount : 0); }
        }

        /// <summary>Name of the phase currently being planned/advanced (or the last one).</summary>
        public string CurrentPhaseName
        {
            get
            {
                int i = _phaseIndex < _parts.Length ? _phaseIndex : _parts.Length - 1;
                if (i < 0 || i >= _parts.Length) return "";
                TreehousePart p = _parts[i];
                return p != null ? p.Phase : "";
            }
        }

        public bool IsCompleted { get { return _done; } }
        public bool IsHalted { get { return _halted; } }
        public bool IsTerminal { get { return _done || _halted; } }

        /// <summary>Drive exactly one bounded step. Returns what happened so the caller can
        /// notify the player and decide whether to keep pumping on subsequent ticks.</summary>
        public TreehouseAdvanceResult Advance(int maxBatch)
        {
            if (_done) return TreehouseAdvanceResult.AllCompleted;
            if (_halted)
            {
                // A cancelled/failed task may still owe a deferred rollback; finish it quietly
                // before we truly stop, but never start a new phase.
                if (_current != null && _current.IsFinished && _current.PendingRollbackCount > 0)
                {
                    int still = _current.RetryRollback(_gateway);
                    if (still > 0) return TreehouseAdvanceResult.Steady;
                    _current = null;
                }
                return _haltedResult;
            }

            // A cancellation that arrived while a phase was in flight: roll it back first.
            if (_cancelRequested && _current != null && !_current.IsFinished)
            {
                _current.Cancel(_gateway);
            }

            // 1) In-flight phase: advance it a bounded amount and stop.
            if (_current != null && !_current.IsFinished)
            {
                _current.Advance(_gateway, maxBatch);
                return TreehouseAdvanceResult.Steady;
            }

            // 2) A finished phase still owing a deferred rollback: retry, do not move on.
            if (_current != null && _current.IsFinished && _current.PendingRollbackCount > 0)
            {
                int still = _current.RetryRollback(_gateway);
                if (still > 0) return TreehouseAdvanceResult.Steady;
            }

            // 3) A cleanly finished phase: decide its fate before planning the next one.
            if (_current != null && _current.IsFinished)
            {
                return FinishCurrentPhase();
            }

            // 4) No current task. Honour a between-phase cancel, then completion, then plan next.
            if (_cancelRequested)
            {
                _halted = true;
                _haltedResult = TreehouseAdvanceResult.Cancelled;
                LastMessage = "已取消装配，未开始后续阶段。已完成阶段可用撤销工具逐段回退。";
                return TreehouseAdvanceResult.Cancelled;
            }

            if (_phaseIndex >= _parts.Length)
            {
                _done = true;
                LastMessage = "森之王庭装配完成。";
                return TreehouseAdvanceResult.AllCompleted;
            }

            TreehousePart part = _parts[_phaseIndex];
            CellPos origin = _anchor.Add(part.OffsetInMaster);
            BuildPlan plan = BuildPlanner.Plan(part.Blueprint, origin, _resolver, _gateway, ClearObstacles, ClearSoft);

            if (!plan.Success)
            {
                if (plan.AllEditabilityFailuresRecoverable)
                {
                    LastMessage = "等待区块就绪（阶段 " + (_phaseIndex + 1) + "/" + _parts.Length +
                        "：" + part.Phase + "）。尚未改动世界。";
                    return TreehouseAdvanceResult.WaitingChunks;
                }
                if (plan.FailedBySoftObstruction && !ClearSoft)
                {
                    LastMessage = "阶段「" + part.Phase + "」被植被/积雪覆盖。确认清理后再继续，尚未改动世界。";
                    return TreehouseAdvanceResult.BlockedBySoft;
                }

                // Hard failure: stop the assembly, do NOT start further batches.
                _halted = true;
                _haltedResult = TreehouseAdvanceResult.Failed;
                LastMessage = "装配中止（阶段「" + part.Phase + "」）：" + plan.FailureReason +
                    " 已完成阶段可逐一撤销，未继续后续阶段。";
                return TreehouseAdvanceResult.Failed;
            }

            _current = new BuildTask(part.Phase, origin, plan.Changes, ClearObstacles, ClearSoft);
            return TreehouseAdvanceResult.Steady;
        }

        /// <summary>Request cancellation. The in-flight phase is rolled back (fully, or a
        /// deferred rollback is retried on later <see cref="Advance"/> calls); completed phases
        /// are left in the world and stay individually undoable. Returns false if a rollback
        /// still needs to finish.</summary>
        public bool Cancel()
        {
            if (_done) return true;
            if (_halted) return _haltedResult == TreehouseAdvanceResult.Cancelled;

            _cancelRequested = true;
            if (_current != null && !_current.IsFinished)
            {
                bool clean = _current.Cancel(_gateway);
                if (!clean)
                {
                    LastMessage = "已请求取消，但有 " + _current.PendingRollbackCount +
                        " 格将随区块恢复继续回滚。";
                    return false;
                }
            }
            return true;
        }

        private TreehouseAdvanceResult FinishCurrentPhase()
        {
            BuildTask task = _current;
            switch (task.State)
            {
                case BuildTaskState.Completed:
                    _history.Record(task);
                    _appliedCompleted += task.AppliedCount;
                    _current = null;
                    _phaseIndex++;
                    if (_phaseIndex >= _parts.Length)
                    {
                        _done = true;
                        LastMessage = "森之王庭装配完成（" + _parts.Length + " 阶段，共 " +
                            _appliedCompleted + " 方块）。";
                        return TreehouseAdvanceResult.AllCompleted;
                    }
                    LastMessage = "阶段完成 " + _phaseIndex + "/" + _parts.Length + "：" + task.Name + "。";
                    return TreehouseAdvanceResult.PhaseCompleted;

                case BuildTaskState.Failed:
                    _current = null;
                    _halted = true;
                    _haltedResult = TreehouseAdvanceResult.Failed;
                    LastMessage = "阶段执行失败并已回滚（" + task.Name + "）：" + task.FailureReason +
                        " 未继续后续阶段。";
                    return TreehouseAdvanceResult.Failed;

                case BuildTaskState.Cancelled:
                    _current = null;
                    _halted = true;
                    _haltedResult = TreehouseAdvanceResult.Cancelled;
                    LastMessage = "已取消装配：阶段「" + task.Name + "」已回滚。";
                    return TreehouseAdvanceResult.Cancelled;

                default:
                    // Pending/Running should not reach here (only called when IsFinished).
                    return TreehouseAdvanceResult.Steady;
            }
        }
    }
}
