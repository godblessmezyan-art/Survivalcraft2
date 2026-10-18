namespace BuilderAssistant.Core.Transactions
{
    /// <summary>
    /// The ONLY way the pure build engine touches the world. The WorldEdit
    /// feature implements this against the real SubsystemTerrain/Terrain on the
    /// main thread; unit tests implement it with an in-memory grid. Keeping the
    /// game reference behind this interface is what lets every safety rule be
    /// exercised without launching Survivalcraft.
    /// </summary>
    internal interface ITerrainGateway
    {
        /// <summary>
        /// Cell is within world bounds AND its chunk is loaded/valid. Kept for hot
        /// paths (per-frame execution) and backwards compatibility; it is just a
        /// one-bit shorthand for InspectCell(...).IsEditable.
        /// </summary>
        bool IsCellEditable(CellPos cell);

        /// <summary>
        /// The classified reason a cell is or is not editable, plus whatever chunk
        /// context the implementation could observe. BuildPlanner uses this to turn
        /// the old collapsed "目标区块未加载或越界" into a precise, actionable
        /// diagnosis (which check, which chunk, recoverable-by-waiting or hard).
        /// </summary>
        CellEditability InspectCell(CellPos cell);

        /// <summary>Cell currently holds air (contents == 0).</summary>
        bool IsCellAir(CellPos cell);

        /// <summary>
        /// Cell holds a non-air but "soft" obstruction that is safe to clear for building
        /// — plants / snow / other walk-through blocks the engine marks non-collidable.
        /// v0.6.3 clear-soft mode may replace these; solid terrain and player structures
        /// (IsCollidable) must return false here so they are never silently wiped.
        /// </summary>
        bool IsCellSoftObstruction(CellPos cell);

        /// <summary>Read the current packed cell value, or false if unreadable.</summary>
        bool TryGetCellValue(CellPos cell, out int value);

        /// <summary>Write a packed cell value on the main thread; false on failure.</summary>
        bool SetCellValue(CellPos cell, int value);
    }
}
