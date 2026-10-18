namespace BuilderAssistant.Core.Transactions
{
    /// <summary>
    /// The single, specific reason a cell could not be marked editable. Collapsing
    /// all of these into one boolean is exactly what hid the "目标区块未加载或越界"
    /// root cause, so the gateway now classifies each failing branch separately.
    /// The member order mirrors the check sequence in GameTerrainGateway.
    /// </summary>
    internal enum CellEditabilityStatus
    {
        /// <summary>Cell can be read/written right now.</summary>
        Editable = 0,

        /// <summary>No terrain object at all (world not loaded / disposed).</summary>
        TerrainNull,

        /// <summary>Y outside [WorldBounds.MinY, MaxYExclusive). Hard error, never a wait.</summary>
        InvalidY,

        /// <summary>Engine IsCellValid returned false (outside generated extent / non-allocatable). Hard.</summary>
        InvalidCell,

        /// <summary>GetChunkAtCell returned null for this column. Recoverable by waiting (chunk streams in).</summary>
        ChunkMissing,

        /// <summary>Chunk exists but its State is not yet Valid. Recoverable by waiting.</summary>
        ChunkStateNotReady,

        /// <summary>Chunk State is fine but IsLoaded is false. Recoverable by waiting.</summary>
        ChunkNotLoaded,
    }

    /// <summary>
    /// A structured, game-agnostic answer to "is this cell editable, and if not,
    /// why?". Carries the raw coordinate plus whatever chunk context the concrete
    /// gateway could observe (chunk coords, its State ordinal, its loaded flag) so
    /// a device log can pinpoint WHICH check on WHICH chunk failed. Sentinel values
    /// mark context an implementation could not supply (e.g. the in-memory test
    /// grid, or a failure raised before any chunk handle was obtained).
    /// </summary>
    internal readonly struct CellEditability
    {
        /// <summary>Sentinel for "chunk coordinate not applicable / unknown".</summary>
        public const int UnknownCoord = int.MinValue;

        /// <summary>Sentinel for a raw State ordinal the engine did not report.</summary>
        public const int UnknownState = -1;

        public readonly CellPos Cell;
        public readonly CellEditabilityStatus Status;

        public readonly bool HasChunk;
        public readonly int ChunkX;
        public readonly int ChunkZ;
        public readonly int ChunkState;
        public readonly bool ChunkLoaded;

        public CellEditability(CellPos cell, CellEditabilityStatus status)
            : this(cell, status, false, UnknownCoord, UnknownCoord, UnknownState, false)
        {
        }

        public CellEditability(
            CellPos cell,
            CellEditabilityStatus status,
            bool hasChunk,
            int chunkX,
            int chunkZ,
            int chunkState,
            bool chunkLoaded)
        {
            Cell = cell;
            Status = status;
            HasChunk = hasChunk;
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            ChunkState = chunkState;
            ChunkLoaded = chunkLoaded;
        }

        public static CellEditability Ok(CellPos cell)
        {
            return new CellEditability(cell, CellEditabilityStatus.Editable);
        }

        public bool IsEditable
        {
            get { return Status == CellEditabilityStatus.Editable; }
        }

        /// <summary>
        /// True when the only thing missing is time: the region is legitimately
        /// part of the world and the engine is still streaming/generating the
        /// chunk. Such failures are worth a bounded wait-and-retry instead of an
        /// immediate abort. Out-of-bounds and non-allocatable cells are NOT
        /// recoverable, so waiting on them would be pointless (and risky).
        /// </summary>
        public bool IsRecoverableByWaiting
        {
            get
            {
                return Status == CellEditabilityStatus.ChunkMissing ||
                       Status == CellEditabilityStatus.ChunkStateNotReady ||
                       Status == CellEditabilityStatus.ChunkNotLoaded;
            }
        }

        /// <summary>One-line, log/clipboard friendly description for a single cell.</summary>
        public string Describe()
        {
            string s = Status + " @ " + Cell;
            if (HasChunk)
            {
                s += " chunk(" + ChunkX + "," + ChunkZ + ")";
                if (ChunkState != UnknownState)
                {
                    s += " state=" + ChunkState;
                }
                s += " loaded=" + ChunkLoaded;
            }
            return s;
        }
    }
}
