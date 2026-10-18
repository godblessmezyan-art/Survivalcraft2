using System.Collections.Generic;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.BuildEngine
{
    /// <summary>
    /// A validated plan staged for batched execution. Writes no more than
    /// maxBatch cells per call (which the WorldEdit runtime advances once per
    /// frame on the main thread), captures every original value immediately
    /// before its write, rolls back the applied portion on any failure, and can
    /// be cancelled or undone without ever clobbering a block the player edited
    /// after the build ran.
    /// </summary>
    /// <remarks>
    /// Rollback and undo previously swallowed cells whose chunk was momentarily
    /// uneditable at restore time and still reported "已回滚/已撤销" — which could
    /// leave the world half-restored while the UI claimed a clean result. This
    /// version keeps an explicit pending list for both paths, distinguishes
    /// fully-done from partial, and exposes a retry so a chunk streaming back in
    /// lets the world converge without ever overwriting a later player edit.
    /// </remarks>
    internal sealed class BuildTask
    {
        public string Name { get; private set; }
        public CellPos Origin { get; private set; }
        public BuildTaskState State { get; private set; }
        public string FailureReason { get; private set; }
        public int PlannedCount { get { return _changes.Count; } }
        public int AppliedCount { get { return _applied.Count; } }
        public bool IsFinished
        {
            get { return State != BuildTaskState.Pending && State != BuildTaskState.Running; }
        }

        /// <summary>Applied cells that could not yet be restored during failure/cancel rollback.</summary>
        public int PendingRollbackCount { get { return _pendingRollback.Count; } }

        /// <summary>Cells still awaiting restore after an Undo (chunk was uneditable at undo time).</summary>
        public int PendingUndoCount { get { return _pendingUndo.Count; } }

        /// <summary>True when the task reached a terminal state AND nothing is left unrestored.</summary>
        public bool IsCleanlyFinished
        {
            get { return IsFinished && _pendingRollback.Count == 0 && _pendingUndo.Count == 0; }
        }

        private readonly List<PlannedChange> _changes;
        private readonly List<BlockUndoRecord> _applied = new List<BlockUndoRecord>();
        private readonly List<BlockUndoRecord> _pendingRollback = new List<BlockUndoRecord>();
        private readonly List<BlockUndoRecord> _pendingUndo = new List<BlockUndoRecord>();
        private readonly bool _allowOverride;
        private int _cursor;

        private enum RestoreOutcome
        {
            Restored,
            Skipped,   // intentionally left alone (a later player edit) — never retried
            Pending    // could not act now (chunk uneditable / write failed) — retry later
        }

        public BuildTask(string name, CellPos origin, List<PlannedChange> changes, bool allowOverride)
        {
            Name = name;
            Origin = origin;
            _changes = changes;
            _allowOverride = allowOverride;
            State = BuildTaskState.Pending;
        }

        /// <summary>Apply up to maxBatch cells. Returns how many were applied.</summary>
        public int Advance(ITerrainGateway gateway, int maxBatch)
        {
            if (State == BuildTaskState.Failed || State == BuildTaskState.Cancelled ||
                State == BuildTaskState.Completed || State == BuildTaskState.Undone)
            {
                return 0;
            }

            State = BuildTaskState.Running;
            int appliedThisCall = 0;

            while (_cursor < _changes.Count && appliedThisCall < maxBatch)
            {
                PlannedChange change = _changes[_cursor];

                CellEditability eco = gateway.InspectCell(change.Absolute);
                if (!eco.IsEditable)
                {
                    return Fail(gateway, "执行中区块变为未加载/越界[" + eco.Describe() + "]");
                }

                if (!_allowOverride && !gateway.IsCellAir(change.Absolute))
                {
                    return Fail(gateway, "执行中目标已非空气，安全中止: " + change.Absolute);
                }

                int oldValue;
                if (!gateway.TryGetCellValue(change.Absolute, out oldValue))
                {
                    return Fail(gateway, "无法读取原始方块: " + change.Absolute);
                }

                if (!gateway.SetCellValue(change.Absolute, change.Value))
                {
                    return Fail(gateway, "写入失败: " + change.Absolute);
                }

                _applied.Add(new BlockUndoRecord(change.Absolute, oldValue, change.Value));
                _cursor++;
                appliedThisCall++;
            }

            if (_cursor >= _changes.Count)
            {
                State = BuildTaskState.Completed;
            }

            return appliedThisCall;
        }

        private int Fail(ITerrainGateway gateway, string reason)
        {
            FailureReason = reason;
            int pending = RollbackApplied(gateway);
            if (pending > 0)
            {
                FailureReason = reason + "（另有 " + pending +
                    " 格因区块未就绪暂未回滚，待区块恢复后可自动/再次撤销重试）";
            }
            State = BuildTaskState.Failed;
            return 0;
        }

        /// <summary>Abort an in-flight build and restore everything it placed.</summary>
        public bool Cancel(ITerrainGateway gateway)
        {
            if (State != BuildTaskState.Pending && State != BuildTaskState.Running)
            {
                return false;
            }
            int pending = RollbackApplied(gateway);
            FailureReason = pending > 0
                ? "已取消，但 " + pending + " 格暂未回滚（区块未就绪），待恢复后重试"
                : FailureReason;
            State = BuildTaskState.Cancelled;
            return true;
        }

        /// <summary>Restore every already-applied cell; returns how many could not yet be restored.</summary>
        private int RollbackApplied(ITerrainGateway gateway)
        {
            _pendingRollback.Clear();
            for (int i = _applied.Count - 1; i >= 0; i--)
            {
                BlockUndoRecord record = _applied[i];
                RestoreOutcome outcome = TryRestore(gateway, record);
                if (outcome == RestoreOutcome.Pending)
                {
                    _pendingRollback.Add(record);
                }
            }
            return _pendingRollback.Count;
        }

        /// <summary>
        /// Retry a previously incomplete rollback once chunks have streamed back.
        /// Cells a player has since edited are dropped (never clobbered). Returns
        /// the count still pending (0 means the world fully converged).
        /// </summary>
        public int RetryRollback(ITerrainGateway gateway)
        {
            if (_pendingRollback.Count == 0)
            {
                return 0;
            }
            var stillPending = new List<BlockUndoRecord>(_pendingRollback.Count);
            for (int i = _pendingRollback.Count - 1; i >= 0; i--)
            {
                BlockUndoRecord record = _pendingRollback[i];
                if (TryRestore(gateway, record) == RestoreOutcome.Pending)
                {
                    stillPending.Add(record);
                }
            }
            _pendingRollback.Clear();
            _pendingRollback.AddRange(stillPending);
            return _pendingRollback.Count;
        }

        /// <summary>
        /// Undo the completed build. Restores each original cell only where the
        /// current value still equals what THIS build wrote, so later player
        /// edits to the same coordinates are preserved (never overwritten). Cells
        /// whose chunk is uneditable right now are parked in the pending list so a
        /// later RetryUndo can finish them; they are NOT silently forgotten.
        /// </summary>
        public int Undo(ITerrainGateway gateway)
        {
            if (State == BuildTaskState.Undone && _pendingUndo.Count == 0)
            {
                return 0;
            }

            // First undo walks the full applied set; a retry only walks what is left.
            List<BlockUndoRecord> source = _pendingUndo.Count == 0 ? _applied : _pendingUndo;
            if (source == _pendingUndo)
            {
                return RetryUndo(gateway);
            }

            _pendingUndo.Clear();
            int restored = 0;
            for (int i = source.Count - 1; i >= 0; i--)
            {
                BlockUndoRecord record = source[i];
                RestoreOutcome outcome = TryRestore(gateway, record);
                if (outcome == RestoreOutcome.Restored)
                {
                    restored++;
                }
                else if (outcome == RestoreOutcome.Pending)
                {
                    _pendingUndo.Add(record);
                }
            }

            State = BuildTaskState.Undone;
            return restored;
        }

        /// <summary>Finish an undo that was left partial; returns the count still pending.</summary>
        public int RetryUndo(ITerrainGateway gateway)
        {
            if (_pendingUndo.Count == 0)
            {
                return 0;
            }
            var stillPending = new List<BlockUndoRecord>(_pendingUndo.Count);
            for (int i = _pendingUndo.Count - 1; i >= 0; i--)
            {
                BlockUndoRecord record = _pendingUndo[i];
                if (TryRestore(gateway, record) == RestoreOutcome.Pending)
                {
                    stillPending.Add(record);
                }
            }
            _pendingUndo.Clear();
            _pendingUndo.AddRange(stillPending);
            State = BuildTaskState.Undone;
            return _pendingUndo.Count;
        }

        /// <summary>
        /// Try to put one cell back to its captured original value, but ONLY when
        /// the cell still holds exactly what this build wrote. If a player changed
        /// it in the meantime we leave it (Skipped) forever; if we cannot even look
        /// at it right now (chunk uneditable / write failed) we mark it Pending.
        /// </summary>
        private static RestoreOutcome TryRestore(ITerrainGateway gateway, BlockUndoRecord record)
        {
            CellEditability eco = gateway.InspectCell(record.Absolute);
            if (!eco.IsEditable)
            {
                return RestoreOutcome.Pending;
            }

            int current;
            if (!gateway.TryGetCellValue(record.Absolute, out current))
            {
                return RestoreOutcome.Pending;
            }

            if (current != record.WrittenValue)
            {
                // A later player edit at this coordinate: honour it, never overwrite.
                return RestoreOutcome.Skipped;
            }

            if (gateway.SetCellValue(record.Absolute, record.OldValue))
            {
                return RestoreOutcome.Restored;
            }

            return RestoreOutcome.Pending;
        }
    }
}
