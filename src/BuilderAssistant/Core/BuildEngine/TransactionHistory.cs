using System.Collections.Generic;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.BuildEngine
{
    /// <summary>
    /// Bounded, single-player-only undo stack of successfully completed builds.
    /// Only Completed tasks are stored (Failed/Cancelled already rolled
    /// themselves back). Each Undo reverts just the most recent reversible
    /// transaction, honouring the "do not overwrite later player edits" policy.
    /// </summary>
    /// <remarks>
    /// If an Undo could only partially restore (some chunks were unloaded at the
    /// moment of the tap), the task is kept on the stack in an Undone-with-pending
    /// state instead of being dropped, so the next undo tap can finish the job once
    /// those chunks stream back. Without this, a partial undo was reported as done
    /// and the never-restored cells were silently lost.
    /// </remarks>
    internal sealed class TransactionHistory
    {
        public const int MaxDepth = 20;

        private readonly List<BuildTask> _completed = new List<BuildTask>();

        public int Count { get { return _completed.Count; } }

        public void Record(BuildTask task)
        {
            if (task == null || task.State != BuildTaskState.Completed)
            {
                return;
            }
            _completed.Add(task);
            if (_completed.Count > MaxDepth)
            {
                _completed.RemoveAt(0);
            }
        }

        /// <summary>True when there is a completed build to undo OR a partial undo to finish.</summary>
        public bool HasUndoable
        {
            get
            {
                for (int i = _completed.Count - 1; i >= 0; i--)
                {
                    BuildTask task = _completed[i];
                    if (task.State == BuildTaskState.Completed ||
                        (task.State == BuildTaskState.Undone && task.PendingUndoCount > 0))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Undo the newest reversible transaction (finishing a prior partial undo
        /// first if one exists). Returns cells restored this call (-1 if nothing
        /// undoable). <paramref name="pending"/> reports cells that STILL could not
        /// be restored because their chunk is uneditable; when &gt; 0 the task stays
        /// on the stack so a later tap can retry.
        /// </summary>
        public int UndoLast(ITerrainGateway gateway, out int pending)
        {
            pending = 0;
            for (int i = _completed.Count - 1; i >= 0; i--)
            {
                BuildTask task = _completed[i];

                if (task.State == BuildTaskState.Undone && task.PendingUndoCount > 0)
                {
                    // Finish the most recent partial undo before touching older ones.
                    int finished = task.RetryUndo(gateway);
                    pending = task.PendingUndoCount;
                    if (pending == 0)
                    {
                        _completed.RemoveAt(i);
                    }
                    return finished;
                }

                if (task.State != BuildTaskState.Completed)
                {
                    continue;
                }

                int restored = task.Undo(gateway);
                pending = task.PendingUndoCount;
                if (pending == 0)
                {
                    _completed.RemoveAt(i);
                }
                return restored;
            }
            return -1;
        }

        public void Clear()
        {
            _completed.Clear();
        }
    }
}
