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

        public static readonly string[] All =
        {
            StoneBase, StoneWall, WoodWall, WoodTrim, Roof, Glass, Floor,
            RoofStairs, EntryDoor, WindowSill, GardenFence,
        };
    }
}
