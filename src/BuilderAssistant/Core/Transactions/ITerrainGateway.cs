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
        /// <summary>Cell is within world bounds AND its chunk is loaded/valid.</summary>
        bool IsCellEditable(CellPos cell);

        /// <summary>Cell currently holds air (contents == 0).</summary>
        bool IsCellAir(CellPos cell);

        /// <summary>Read the current packed cell value, or false if unreadable.</summary>
        bool TryGetCellValue(CellPos cell, out int value);

        /// <summary>Write a packed cell value on the main thread; false on failure.</summary>
        bool SetCellValue(CellPos cell, int value);
    }
}
