namespace BuilderAssistant.Core.Transactions
{
    /// <summary>
    /// One captured change. OldValue is read immediately BEFORE the write, so
    /// the undo snapshot is always truthful. WrittenValue is what the build put
    /// there; undo only reverts a cell whose current value still equals
    /// WrittenValue, never overwriting a block the player edited afterwards.
    /// </summary>
    internal readonly struct BlockUndoRecord
    {
        public readonly CellPos Absolute;
        public readonly int OldValue;
        public readonly int WrittenValue;

        public BlockUndoRecord(CellPos absolute, int oldValue, int writtenValue)
        {
            Absolute = absolute;
            OldValue = oldValue;
            WrittenValue = writtenValue;
        }
    }
}
