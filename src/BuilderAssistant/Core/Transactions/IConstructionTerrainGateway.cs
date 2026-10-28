using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Transactions
{
    /// <summary>Optional engine semantics, kept separate from the offline grid contract.</summary>
    internal interface IConstructionTerrainGateway
    {
        bool SupportsAttachment(int value, BlockFamily family, int face);
        // Returns the journal even on failure: callbacks may mutate terrain before throwing.
        bool ApplyWithJournal(CellPos cell, int value, out List<BlockUndoRecord> journal, out string error);
        bool ValuesEquivalent(int current, int expected);
    }
}
