using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// A named set of LOGICAL material keys, one per semantic building slot. Components
    /// never pick a concrete block; they ask the profile for the slot they need
    /// ("wall", "roof", "stair"), so re-skinning a building means swapping one profile
    /// and touching zero generator code. Every value must be a key in
    /// <see cref="MaterialCatalog"/>; <c>BuildingSpecValidator</c> enforces that.
    /// </summary>
    internal sealed class MaterialProfile
    {
        public string Foundation;   // ground-floor plinth / floor slab
        public string Wall;         // exterior wall field
        public string Trim;         // corner / frame timbers, posts
        public string Floor;        // upper-story floor slabs
        public string Roof;         // roof plate surfaces
        public string Glass;        // windows
        public string Door;         // entry door (two-cell)
        public string Stair;        // roof eaves (outdoor) stairs
        public string InteriorStair;// floor-to-floor (indoor) stairs
        public string Slab;         // windowsills
        public string Deck;         // flat roof / terrace deck slabs
        public string Railing;      // stairwell / terrace railings
        public string InteriorWall; // room partitions

        public MaterialProfile Clone()
        {
            return new MaterialProfile
            {
                Foundation = Foundation,
                Wall = Wall,
                Trim = Trim,
                Floor = Floor,
                Roof = Roof,
                Glass = Glass,
                Door = Door,
                Stair = Stair,
                InteriorStair = InteriorStair,
                Slab = Slab,
                Deck = Deck,
                Railing = Railing,
                InteriorWall = InteriorWall,
            };
        }

        /// <summary>The bright half-timbered cottage palette (matches the original cottage).</summary>
        public static MaterialProfile Cottage()
        {
            return new MaterialProfile
            {
                Foundation = BuildingMaterials.StoneBase,
                Wall = BuildingMaterials.WoodWall,
                Trim = BuildingMaterials.WoodTrim,
                Floor = BuildingMaterials.Floor,
                Roof = BuildingMaterials.Roof,
                Glass = BuildingMaterials.Glass,
                Door = BuildingMaterials.EntryDoor,
                Stair = BuildingMaterials.RoofStairs,
                InteriorStair = BuildingMaterials.InteriorStairs,
                Slab = BuildingMaterials.WindowSill,
                Deck = BuildingMaterials.FlatDeck,
                Railing = BuildingMaterials.GardenFence,
                InteriorWall = BuildingMaterials.InteriorWall,
            };
        }

        /// <summary>A dressed-stone townhouse palette with a walkable upper floor.</summary>
        public static MaterialProfile House()
        {
            return new MaterialProfile
            {
                Foundation = BuildingMaterials.StoneBase,
                Wall = BuildingMaterials.StoneWall,
                Trim = BuildingMaterials.WoodTrim,
                Floor = BuildingMaterials.Floor,
                Roof = BuildingMaterials.Roof,
                Glass = BuildingMaterials.Glass,
                Door = BuildingMaterials.EntryDoor,
                Stair = BuildingMaterials.RoofStairs,
                InteriorStair = BuildingMaterials.InteriorStairs,
                Slab = BuildingMaterials.WindowSill,
                Deck = BuildingMaterials.FlatDeck,
                Railing = BuildingMaterials.StoneRailing,
                InteriorWall = BuildingMaterials.InteriorWall,
            };
        }
    }
}
