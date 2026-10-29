using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Safety;
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

        /// <summary>
        /// v0.6.3 bounded stall: how many consecutive Advance calls have made zero
        /// forward progress because the next in-order target chunk is only transiently
        /// not-ready. Resets to 0 whenever a cell is actually applied. Once this exceeds
        /// BuildSafetyLimits.MaxIdleAdvanceCallsDuringBuild the build gives up and rolls
        /// back safely instead of hanging forever on a chunk that never streams in.
        /// </summary>
        public int IdleAdvanceCalls { get { return _idleAdvanceCalls; } }

        /// <summary>Running but currently blocked on a recoverable (waitable) chunk.</summary>
        public bool IsStalled
        {
            get { return State == BuildTaskState.Running && _idleAdvanceCalls > 0; }
        }

        private readonly List<PlannedChange> _changes;
        private readonly List<BlockUndoRecord> _applied = new List<BlockUndoRecord>();
        private readonly List<BlockUndoRecord> _pendingRollback = new List<BlockUndoRecord>();
        private readonly List<BlockUndoRecord> _pendingUndo = new List<BlockUndoRecord>();
        private readonly bool _allowOverride;
        private Func<CellPos, int, bool> _preWriteGuard;
        private readonly bool _clearSoftObstructions;
        private int _idleAdvanceCalls;
        private string _stallReason;
        private int _cursor;
        private bool _awaitingVerification;
        private int _verificationCursor;
        private int _writesThisAdvance;
        private readonly HashSet<CellPos> _doorPositions = new HashSet<CellPos>();
        public int LastRestoreCount { get; private set; }

        private enum RestoreOutcome
        {
            Restored,
            Skipped,   // intentionally left alone (a later player edit) — never retried
            Pending    // could not act now (chunk uneditable / write failed) — retry later
        }

        public BuildTask(string name, CellPos origin, List<PlannedChange> changes, bool allowOverride)
            : this(name, origin, changes, allowOverride, false)
        {
        }

        public BuildTask(
            string name,
            CellPos origin,
            List<PlannedChange> changes,
            bool allowOverride,
            bool clearSoftObstructions)
        {
            Name = name;
            Origin = origin;
            _changes = changes;
            foreach (PlannedChange c in changes) if (c.Family == BlockFamily.Door) _doorPositions.Add(c.Absolute);
            _allowOverride = allowOverride;
            _clearSoftObstructions = clearSoftObstructions;
            State = BuildTaskState.Pending;
        }

        /// <summary>Optional operation-specific last-moment terrain check before any write.</summary>
        public void SetPreWriteGuard(Func<CellPos, int, bool> guard) { _preWriteGuard = guard; }

        /// <summary>Apply up to maxBatch cells. Returns how many were applied.</summary>
        public int Advance(ITerrainGateway gateway, int maxBatch)
        {
            if (State == BuildTaskState.Failed || State == BuildTaskState.Cancelled ||
                State == BuildTaskState.Completed || State == BuildTaskState.Undone)
            {
                return 0;
            }

            State = BuildTaskState.Running;
            _writesThisAdvance = 0;
            int appliedThisCall = 0;
            maxBatch = Math.Min(maxBatch, BuildSafetyLimits.MaxBlocksPerBatch);
            var semantics = gateway as IConstructionTerrainGateway;
            if (_awaitingVerification)
            {
                int checkedThisCall = 0;
                while (_verificationCursor < _changes.Count && checkedThisCall < maxBatch)
                {
                    PlannedChange placed = _changes[_verificationCursor];
                    if (placed.HasSupport)
                    {
                        CellEditability ready = gateway.InspectCell(placed.Absolute);
                        if (!ready.IsEditable)
                            return ready.IsRecoverableByWaiting ? OnRecoverableStall(gateway, 0, ready) : Fail(gateway, "施工后状态无法复核");
                        int actual, supportValue, face;
                        CellPos support;
                        if (!AttachmentRules.TryGetSupport(placed.Absolute, placed.Family, placed.Data, out support, out face))
                            return Fail(gateway, "施工后支撑规则无效");
                        CellEditability supportReady = gateway.InspectCell(support);
                        if (!supportReady.IsEditable)
                            return supportReady.IsRecoverableByWaiting ? OnRecoverableStall(gateway, 0, supportReady) : Fail(gateway, "施工后支撑格不可编辑");
                        if (!gateway.TryGetCellValue(placed.Absolute, out actual) ||
                            !(semantics != null ? semantics.ValuesEquivalent(actual, placed.Value) : actual == placed.Value) ||
                            !gateway.TryGetCellValue(support, out supportValue) || semantics == null ||
                            !semantics.SupportsAttachment(supportValue, placed.Family, face))
                            return Fail(gateway, "施工后依附方块状态不一致: " + placed.Absolute);
                    }
                    _verificationCursor++;
                    checkedThisCall++;
                    _idleAdvanceCalls = 0;
                }
                if (_verificationCursor >= _changes.Count) State = BuildTaskState.Completed;
                return 0;
            }

            while (_cursor < _changes.Count && appliedThisCall < maxBatch)
            {
                PlannedChange change = _changes[_cursor];
                // Door addition can synchronously generate its upper cell: both consume the frame budget.
                int writeCost = semantics != null && change.Family == BlockFamily.Door ? 2 : 1;
                if (appliedThisCall + writeCost > maxBatch) break;

                CellEditability eco = gateway.InspectCell(change.Absolute);
                if (!eco.IsEditable)
                {
                    // v0.6.3: our own writes trigger an engine re-mesh that can briefly
                    // drop the chunk State below Valid. That is transient and recoverable,
                    // so pausing to retry is correct — aborting the whole cottage for it is
                    // not. Only a HARD failure (invalid Y / extent / terrain-null) aborts.
                    if (eco.IsRecoverableByWaiting)
                    {
                        return OnRecoverableStall(gateway, appliedThisCall, eco);
                    }
                    return Fail(gateway, "执行中区块不可编辑[" + eco.Describe() + "]");
                }

                if (!_allowOverride && !gateway.IsCellAir(change.Absolute))
                {
                    // Non-air target. Replace it only when the user opted into full
                    // override, or into clear-soft AND this is a walk-through plant/snow.
                    bool softClearable =
                        _clearSoftObstructions && gateway.IsCellSoftObstruction(change.Absolute);
                    if (!softClearable)
                    {
                        return Fail(gateway, "执行中目标已非空气，安全中止: " + change.Absolute);
                    }
                    // else: clear-soft is enabled and this is a plant/snow — allowed.
                }

                CellPos support;
                int face;
                if (AttachmentRules.TryGetSupport(change.Absolute, change.Family, change.Data, out support, out face))
                {
                    CellEditability supportReady = gateway.InspectCell(support);
                    if (!supportReady.IsEditable)
                        return supportReady.IsRecoverableByWaiting ? OnRecoverableStall(gateway, appliedThisCall, supportReady) : Fail(gateway, "支撑格不可编辑");
                    int supportValue;
                    if (semantics == null || !gateway.TryGetCellValue(support, out supportValue) ||
                        !semantics.SupportsAttachment(supportValue, change.Family, face))
                        return Fail(gateway, "施工中支撑失效: " + support);
                }

                int oldValue;
                if (!gateway.TryGetCellValue(change.Absolute, out oldValue))
                {
                    return Fail(gateway, "无法读取原始方块: " + change.Absolute);
                }

                if (_preWriteGuard != null)
                {
                    bool safe;
                    try { safe = _preWriteGuard(change.Absolute, oldValue); }
                    catch (Exception ex) { return Fail(gateway, "写前安全检查失败: " + ex.Message); }
                    if (!safe) return Fail(gateway, "确认后目标地形发生变化，已中止: " + change.Absolute);
                }

                _writesThisAdvance += writeCost;
                if (semantics != null)
                {
                    List<BlockUndoRecord> journal;
                    string error;
                    bool success = semantics.ApplyWithJournal(change.Absolute, change.Value, out journal, out error);
                    _applied.AddRange(journal); // includes partial mutations on a failing callback
                    if (!success) return Fail(gateway, "写入失败: " + change.Absolute + " " + error);
                }
                else
                {
                    // Reserve undo BEFORE invoking a gateway that may throw after changing the cell.
                    _applied.Add(new BlockUndoRecord(change.Absolute, oldValue, change.Value));
                    try
                    {
                        if (!gateway.SetCellValue(change.Absolute, change.Value))
                            return Fail(gateway, "写入失败: " + change.Absolute);
                    }
                    catch (Exception ex) { return Fail(gateway, "写入异常: " + ex.Message); }
                }
                _cursor++;
                appliedThisCall += writeCost;
            }

            if (appliedThisCall > 0)
            {
                _idleAdvanceCalls = 0;
                _stallReason = null;
            }

            if (_cursor >= _changes.Count)
            {
                bool hasAttachments = false;
                foreach (PlannedChange c in _changes) if (c.HasSupport) { hasAttachments = true; break; }
                if (hasAttachments) _awaitingVerification = true;
                else State = BuildTaskState.Completed;
            }

            return appliedThisCall;
        }

        /// <summary>
        /// Handles hitting a still-streaming / transiently re-meshed chunk mid-build. We
        /// never roll back for something the engine can recover from: we keep whatever was
        /// applied this call, stay Running, and let the next GuiUpdate tick call Advance
        /// again. Only after MaxIdleAdvanceCallsDuringBuild *consecutive zero-progress*
        /// calls do we give up (a chunk that truly never arrives) and roll back safely.
        /// </summary>
        private int OnRecoverableStall(ITerrainGateway gateway, int appliedThisCall, CellEditability eco)
        {
            _stallReason = eco.Describe();

            if (appliedThisCall > 0)
            {
                // We made real progress this call, so this is not an idle stall.
                _idleAdvanceCalls = 0;
                return appliedThisCall;
            }

            _idleAdvanceCalls++;
            if (_idleAdvanceCalls > BuildSafetyLimits.MaxIdleAdvanceCallsDuringBuild)
            {
                return Fail(gateway,
                    "目标区块持续未就绪，已等待 " + _idleAdvanceCalls +
                    " 帧仍未恢复[" + _stallReason + "]");
            }

            return 0;
        }

        /// <summary>Last recoverable stall description, or null when not stalled.</summary>
        public string StallReason { get { return _stallReason; } }

        private int Fail(ITerrainGateway gateway, string reason)
        {
            if (_cursor < _changes.Count) reason = _changes[_cursor].Context + reason;
            FailureReason = reason;
            int pending = RollbackApplied(gateway, Math.Max(0, BuildSafetyLimits.MaxBlocksPerBatch - _writesThisAdvance));
            if (pending > 0)
            {
                FailureReason = reason + "（另有 " + pending +
                    " 格等待分批回滚或区块恢复，待区块恢复后可自动/再次撤销重试）";
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
                ? "已取消，但 " + pending + " 格等待分批回滚或区块恢复，待恢复后重试"
                : FailureReason;
            State = BuildTaskState.Cancelled;
            return true;
        }

        /// <summary>Restore every already-applied cell; returns how many could not yet be restored.</summary>
        private int RollbackApplied(ITerrainGateway gateway, int budget = BuildSafetyLimits.MaxBlocksPerBatch)
        {
            _pendingRollback.Clear();
            for (int i = _applied.Count - 1; i >= 0; i--) _pendingRollback.Add(_applied[i]);
            DrainRestore(gateway, _pendingRollback, budget);
            return _pendingRollback.Count;
        }

        public int RetryRollback(ITerrainGateway gateway)
        {
            DrainRestore(gateway, _pendingRollback, BuildSafetyLimits.MaxBlocksPerBatch);
            return _pendingRollback.Count;
        }

        public int Undo(ITerrainGateway gateway)
        {
            if (State == BuildTaskState.Undone && _pendingUndo.Count == 0) return 0;
            if (State != BuildTaskState.Undone)
            {
                _pendingUndo.Clear();
                for (int i = _applied.Count - 1; i >= 0; i--) _pendingUndo.Add(_applied[i]);
            }
            DrainRestore(gateway, _pendingUndo, BuildSafetyLimits.MaxBlocksPerBatch);
            State = BuildTaskState.Undone;
            return LastRestoreCount;
        }

        public int RetryUndo(ITerrainGateway gateway)
        {
            DrainRestore(gateway, _pendingUndo, BuildSafetyLimits.MaxBlocksPerBatch);
            State = BuildTaskState.Undone;
            return _pendingUndo.Count;
        }

        // Pending records are always stored in reverse construction/dependency order.
        // Stop at a failure so a support cannot disappear beneath an unrestored attachment.
        private void DrainRestore(ITerrainGateway gateway, List<BlockUndoRecord> pending, int budget)
        {
            LastRestoreCount = 0;
            int consumed = 0, processed = 0;
            while (processed < pending.Count && consumed < budget)
            {
                BlockUndoRecord record = pending[processed];
                // Removing a door can remove its companion synchronously. Reserve both cells.
                int cost = _doorPositions.Contains(record.Absolute) ? 2 : 1;
                if (consumed + cost > budget) break;
                consumed += cost;
                RestoreOutcome outcome = TryRestore(gateway, record);
                if (outcome == RestoreOutcome.Pending) break;
                if (outcome == RestoreOutcome.Restored) LastRestoreCount++;
                processed++;
            }
            if (processed > 0) pending.RemoveRange(0, processed);
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

            var semantics = gateway as IConstructionTerrainGateway;
            if (!(semantics != null ? semantics.ValuesEquivalent(current, record.WrittenValue) : current == record.WrittenValue))
            {
                // A later player edit at this coordinate: honour it, never overwrite.
                return RestoreOutcome.Skipped;
            }

            try
            {
                if (gateway.SetCellValue(record.Absolute, record.OldValue)) return RestoreOutcome.Restored;
            }
            catch (Exception) { return RestoreOutcome.Pending; }

            return RestoreOutcome.Pending;
        }
    }
}
