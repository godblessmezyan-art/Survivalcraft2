using System.Collections.Generic;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// The complete, serializable description of a building to generate. This is the
    /// "structured parameter" the whole engine keys off: change a handful of fields here
    /// and get a different size / layout, instead of editing block coordinates. A future
    /// AI path only ever needs to emit a <c>BuildingSpec</c>, never hundreds of cells.
    ///
    /// A spec is a plain data bag; it knows nothing about terrain or the game. All size
    /// rules are enforced in <see cref="Validate"/>, which the composer calls before any
    /// generation, so an invalid or over-budget spec is refused up front (no partial
    /// blueprint, no unbounded loop).
    /// </summary>
    internal sealed class BuildingSpec
    {
        // --- Hard bounds. The axis limit is the existing selection cap; the others keep
        //     a single generated structure well inside one build transaction. ---
        public const int MinWidth = 5;
        public const int MinLength = 5;
        public const int MinWallHeight = 2;
        public const int MaxWallHeight = 8;
        public const int MinFloors = 1;
        public const int MaxFloors = 4;
        public const int MaxSeedSpan = int.MaxValue; // any int seed is legal

        public BuildingType Type = BuildingType.Cottage;
        public int Width = 9;          // X extent
        public int Length = 11;        // Z extent
        public int Floors = 1;         // number of usable storeys
        public int WallHeight = 4;     // wall courses per storey
        public RoofType Roof = RoofType.Hip;
        public int Seed = 0;
        public bool HasChimney = true;
        public int WindowDensity = 50; // 0..100, how many candidate window slots are glazed

        public MaterialProfile Materials = MaterialProfile.Cottage();

        /// <summary>Parse user input without throwing, including the complete signed int range.</summary>
        public static bool TryParseInteger(string text, out int value)
        {
            return int.TryParse(text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>The original 9x11x10 cottage, expressed as parameters.</summary>
        public static BuildingSpec DefaultCottage()
        {
            return new BuildingSpec
            {
                Type = BuildingType.Cottage,
                Width = 9,
                Length = 11,
                Floors = 1,
                WallHeight = 4,
                Roof = RoofType.Hip,
                Seed = 0,
                HasChimney = true,
                WindowDensity = 50,
                Materials = MaterialProfile.Cottage(),
            };
        }

        /// <summary>A two-storey house: two floors, gabled roof, stone shell.</summary>
        public static BuildingSpec DefaultTwoStoryHouse()
        {
            return new BuildingSpec
            {
                Type = BuildingType.HouseTwoStory,
                Width = 9,
                Length = 9,
                Floors = 2,
                WallHeight = 4,
                Roof = RoofType.Gabled,
                Seed = 0,
                HasChimney = true,
                WindowDensity = 65,
                Materials = MaterialProfile.House(),
            };
        }

        /// <summary>
        /// The Y level of each story's FLOOR slab (its living surface). Story s occupies
        /// wall courses FloorSlabY(s)+1 .. +WallHeight, and story s+1's slab sits one above
        /// the top of those walls. The roof always starts at FloorSlabY(Floors).
        /// </summary>
        public int FloorSlabY(int story)
        {
            return story * (WallHeight + 1);
        }

        /// <summary>Topmost Y a completed building can reach (ceiling/roof base, before roof shape).</summary>
        public int CeilingY { get { return FloorSlabY(Floors); } }

        /// <summary>
        /// A deliberately CONSERVATIVE upper bound on block count for pre-generation budget
        /// rejection. It over-counts (counts every floor as a full solid plate, plus a full
        /// roof plate plus a margin), so anything the real hollow generator produces is
        /// strictly less. It is a cheap guard against absurd specs, not the authoritative
        /// count - <c>BuildingValidator</c> checks the true count against the cap.
        /// </summary>
        public int EstimateBlockCount()
        {
            int perimeter = 2 * (Width + Length) - 4;
            int floors = Floors * Width * Length;          // each storey's slab, full plate
            int ceiling = Width * Length;                   // roof base plate
            int walls = Floors * WallHeight * perimeter;    // exterior wall courses
            int roof = (Roof == RoofType.Flat) ? Width * Length : Width * Length + perimeter * WallHeight;
            int decor = perimeter * 3 + 64;                 // doors/windows/rails/chimney margin
            return floors + ceiling + walls + roof + decor;
        }

        /// <summary>
        /// Validate every rule before generation. Returns a result listing all problems
        /// (never throws). On any error the composer aborts and emits nothing.
        /// </summary>
        public SpecValidationResult Validate()
        {
            var errors = new List<string>();

            if (Width < MinWidth || Width > BuildSafetyLimits.MaxSelectionAxisLength)
            {
                errors.Add("Width " + Width + " 超出范围 [" + MinWidth + ".." + BuildSafetyLimits.MaxSelectionAxisLength + "]。");
            }
            if (Length < MinLength || Length > BuildSafetyLimits.MaxSelectionAxisLength)
            {
                errors.Add("Length " + Length + " 超出范围 [" + MinLength + ".." + BuildSafetyLimits.MaxSelectionAxisLength + "]。");
            }
            if (Floors < MinFloors || Floors > MaxFloors)
            {
                errors.Add("Floors " + Floors + " 超出范围 [" + MinFloors + ".." + MaxFloors + "]。");
            }
            if (WallHeight < MinWallHeight || WallHeight > MaxWallHeight)
            {
                errors.Add("WallHeight " + WallHeight + " 超出范围 [" + MinWallHeight + ".." + MaxWallHeight + "]。");
            }
            if (WindowDensity < 0 || WindowDensity > 100)
            {
                errors.Add("WindowDensity " + WindowDensity + " 不是合法的 0..100 百分比。");
            }

            // The whole structure must fit inside the world's vertical span with room to
            // place it; a roof that could reach near 255 is rejected on the honest estimate.
            int totalHeightEstimate = CeilingY + WallHeight + 2; // roof stack allowance
            if (totalHeightEstimate > 64)
            {
                errors.Add("估算总高 " + totalHeightEstimate + " 超过单轴上限 64。");
            }

            if (Materials == null)
            {
                errors.Add("Materials 为空。");
            }
            else
            {
                ValidateMaterial(errors, "Foundation", Materials.Foundation);
                ValidateMaterial(errors, "Wall", Materials.Wall);
                ValidateMaterial(errors, "Trim", Materials.Trim);
                ValidateMaterial(errors, "Floor", Materials.Floor);
                ValidateMaterial(errors, "Roof", Materials.Roof);
                ValidateMaterial(errors, "Glass", Materials.Glass);
                ValidateMaterial(errors, "Door", Materials.Door);
                ValidateMaterial(errors, "Stair", Materials.Stair);
                ValidateMaterial(errors, "InteriorStair", Materials.InteriorStair);
                ValidateMaterial(errors, "Slab", Materials.Slab);
                ValidateMaterial(errors, "Deck", Materials.Deck);
                ValidateMaterial(errors, "Railing", Materials.Railing);
                ValidateMaterial(errors, "InteriorWall", Materials.InteriorWall);
            }

            // Budget: refuse before we ever build. A single-storey is checked against the
            // conservative estimate; two-storey needs a stairwell so also require enough width.
            int estimate = EstimateBlockCount();
            if (estimate > BuildSafetyLimits.MaxBlocksPerOperation)
            {
                errors.Add("估算方块数 " + estimate + " 已超过每事务上限 " + BuildSafetyLimits.MaxBlocksPerOperation + "，请缩小尺寸或减少层数。");
            }
            if (Type == BuildingType.HouseTwoStory && Floors < 2)
            {
                errors.Add("双层住宅 Floors 至少为 2。");
            }
            if (Type == BuildingType.HouseTwoStory && (Width < 7 || Length < 7))
            {
                errors.Add("双层住宅需至少 7x7 以容纳可通行楼梯井（当前 " + Width + "x" + Length + "）。");
            }

            return new SpecValidationResult(errors);
        }

        private static void ValidateMaterial(List<string> errors, string slot, string key)
        {
            if (!MaterialCatalog.IsKnownKey(key))
            {
                errors.Add("材料槽 " + slot + " 的键 '" + key + "' 不在 MaterialCatalog 已知键集合内。");
            }
        }
    }

    /// <summary>Outcome of <see cref="BuildingSpec.Validate"/> - a list of problems or success.</summary>
    internal sealed class SpecValidationResult
    {
        private readonly List<string> _errors;

        public SpecValidationResult(List<string> errors)
        {
            _errors = errors ?? new List<string>();
        }

        public bool IsValid { get { return _errors.Count == 0; } }
        public IReadOnlyList<string> Errors { get { return _errors; } }

        public string JoinedErrors()
        {
            return string.Join("; ", _errors.ToArray());
        }
    }
}
