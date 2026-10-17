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

        private readonly List<PlannedChange> _changes;
        private readonly List<BlockUndoRecord> _applied = new List<BlockUndoRecord>();
        private readonly bool _allowOverride;
        private int _cursor;

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

                if (!gateway.IsCellEditable(change.Absolute))
                {
                    return Fail(gateway, "执行中区块变为未加载/越界: " + change.Absolute);
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
            RollbackApplied(gateway);
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
            RollbackApplied(gateway);
            State = BuildTaskState.Cancelled;
            return true;
        }

        private void RollbackApplied(ITerrainGateway gateway)
        {
            for (int i = _applied.Count - 1; i >= 0; i--)
            {
                BlockUndoRecord record = _applied[i];
                if (gateway.IsCellEditable(record.Absolute))
                {
                    gateway.SetCellValue(record.Absolute, record.OldValue);
                }
            }
        }

        /// <summary>
        /// Undo the completed build. Restores each original cell only where the
        /// current value still equals what THIS build wrote, so later player
        /// edits to the same coordinates are preserved (never overwritten).
        /// </summary>
        public int Undo(ITerrainGateway gateway)
        {
            if (State == BuildTaskState.Undone)
            {
                return 0;
            }

            int restored = 0;
            for (int i = _applied.Count - 1; i >= 0; i--)
            {
                BlockUndoRecord record = _applied[i];
                if (!gateway.IsCellEditable(record.Absolute))
                {
                    continue;
                }

                int current;
                if (!gateway.TryGetCellValue(record.Absolute, out current))
                {
                    continue;
                }

                if (current == record.WrittenValue)
                {
                    if (gateway.SetCellValue(record.Absolute, record.OldValue))
                    {
                        restored++;
                    }
                }
            }

            State = BuildTaskState.Undone;
            return restored;
        }
    }
}
