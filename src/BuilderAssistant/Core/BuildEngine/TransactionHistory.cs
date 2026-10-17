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

        public bool HasUndoable
        {
            get
            {
                for (int i = _completed.Count - 1; i >= 0; i--)
                {
                    if (_completed[i].State == BuildTaskState.Completed)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Undo the newest still-completed transaction. Returns cells restored, -1 if none.</summary>
        public int UndoLast(ITerrainGateway gateway)
        {
            for (int i = _completed.Count - 1; i >= 0; i--)
            {
                BuildTask task = _completed[i];
                if (task.State != BuildTaskState.Completed)
                {
                    continue;
                }
                int restored = task.Undo(gateway);
                _completed.RemoveAt(i);
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
