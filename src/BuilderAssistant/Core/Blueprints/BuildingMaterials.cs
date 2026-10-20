namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// Logical material keys used by generators. These are intentionally not
    /// block indices and not CLR type names: the WorldEdit palette maps a key
    /// to a runtime-verified vanilla block. Keeping the key abstract lets the
    /// same blueprint re-skin with different materials later.
    /// </summary>
    internal static class BuildingMaterials
    {
        public const string StoneBase = "StoneBase";       // foundation / chimney stone
        public const string StoneWall = "StoneWall";       // dressed stone (upper foundation, accents)
        public const string WoodWall = "WoodWall";         // main wall surface
        public const string WoodTrim = "WoodTrim";         // beams / posts / decor wood
        public const string Roof = "Roof";                 // roofing surface
        public const string Glass = "Glass";               // windows
        public const string Floor = "Floor";               // interior floor

        // Oriented logical keys (schema v2). The WorldEdit palette maps each to a
        // vanilla block whose family (stairs/slab/fence/door) owns the cell's data bits;
        // the generator sets the matching BlockFamily on the cell.
        public const string RoofStairs  = "RoofStairs";    // stepped roof edges (WoodenStairsBlock)
        public const string EntryDoor   = "EntryDoor";     // doorway door (WoodenDoorBlock, two-cell)
        public const string WindowSill  = "WindowSill";    // slab windowsill (StoneSlabBlock)
        public const string GardenFence = "GardenFence";   // fence accent (WoodenFenceBlock)

        // v0.10 parametric-engine logical keys. Every one of these is mapped to a
        // verified vanilla block in Core/Building/MaterialCatalog (the single source of
        // truth both the offline validator and the WorldEdit palette read), so a key
        // added here without a catalog entry is caught by an offline parity test.
        public const string InteriorWall   = "InteriorWall";    // room partitions    (PlanksBlock)
        public const string SupportBeam    = "SupportBeam";     // structural posts   (PlanksBlock)
        public const string InteriorStairs = "InteriorStairs";  // floor-to-floor steps (WoodenStairsBlock)
        public const string StoneRailing   = "StoneRailing";    // stairwell/terrace rail (StoneFenceBlock)
        public const string FlatDeck       = "FlatDeck";        // flat roof / terrace deck (StoneSlabBlock)

        // v0.12 giant-tree / treehouse logical keys. The concrete vanilla blocks behind
        // them (OakWoodBlock, SpruceLeavesBlock) were each confirmed against the
        // x25.01.17 assembly in the M1 audit: logs are opaque solid cubes whose only
        // authored DATA bit-pair is the cut-face (vertical log = 0), and evergreen spruce
        // leaves are a data-0 AlphaTestCube that decays only when a nearby log is removed
        // (SubsystemWoodBlockBehavior radius 3), so a canopy kept within 3 of our logs is
        // stable across save/reload. See docs/COMPATIBILITY.md v0.12.
        public const string LogTrunk  = "LogTrunk";    // vertical oak log trunk   (OakWoodBlock, Log, data 0)
        public const string LogBranch = "LogBranch";   // vertical oak log branch  (OakWoodBlock, Log, data 0)
        public const string Leaves    = "Leaves";      // evergreen spruce foliage (SpruceLeavesBlock, Cube, data 0)

        public static readonly string[] All =
        {
            StoneBase, StoneWall, WoodWall, WoodTrim, Roof, Glass, Floor,
            RoofStairs, EntryDoor, WindowSill, GardenFence,
            InteriorWall, SupportBeam, InteriorStairs, StoneRailing, FlatDeck,
            LogTrunk, LogBranch, Leaves,
        };
    }
}
