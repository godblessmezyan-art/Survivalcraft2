using System.Collections.Generic;
using BuilderAssistant.Core.Safety;

namespace BuilderAssistant.Core.Tree
{
    /// <summary>
    /// The ordered set of structural phases the giant treehouse is built in. Each phase
    /// becomes its own blueprint (auto-sub-divided if it would exceed the 1024-cell cap),
    /// so a player/host confirms construction one legible stage at a time instead of one
    /// huge un-reversible transaction. The order is bottom-up and dependency-first: roots,
    /// then the trunk that everything hangs off, then branches, then the leaf canopy that
    /// must hug those logs, then the three dwelling floors (each bolted to the trunk),
    /// then decoration. <see cref="TreehouseBlueprintSet"/> preserves this order.
    /// </summary>
    internal static class TreehousePhaseNames
    {
        public const string Roots = "1.树根与地基 Roots";
        public const string TrunkLower = "2.主树干(下) Trunk-Lower";
        public const string TrunkUpper = "3.主树干(上) Trunk-Upper";
        public const string Branches = "4.树枝与支撑 Branches";
        public const string Canopy = "5.树冠 Canopy";
        public const string FloorOne = "6.第一层木屋 Floor1";
        public const string FloorTwo = "7.第二层木屋 Floor2";
        public const string FloorThree = "8.第三层木屋与屋顶 Floor3";
        public const string Decoration = "9.露台桥接与装饰 Decor";
    }

    /// <summary>
    /// Parameters describing the fantasy treehouse "森之王庭". A plain data bag with no
    /// game dependency; <see cref="Validate"/> refuses anything that could not be built
    /// inside the existing safety envelope, so an absurd spec never reaches a generator.
    /// The footprint/height here are the MASTER assembly bounds; individual phase
    /// blueprints are always smaller (each is re-checked against the caps in
    /// <see cref="TreehouseBlueprintSet"/>).
    /// </summary>
    internal sealed class TreehouseSpec
    {
        // Master footprint. 33x33 base, ~36 tall, matching the task's target envelope
        // (about 33x33 footprint, about 36 high). Every axis stays under the 64 selection
        // cap, so a phase sub-box is always legal.
        public const int HardMaxFootprint = 33; // per horizontal axis
        public const int HardMaxHeight = 36;

        public int Width = 33;                 // X extent
        public int Depth = 33;                 // Z extent
        public int Height = 36;                // Y extent (ground floor at y=0)
        public int Seed = 20261020;            // deterministic layout seed
        public int TrunkTopY = 30;             // where the trunk tapers into the crown

        // The three dwelling platforms (deck surface Y). Walls sit a few courses above.
        public int FloorOneDeckY = 6;
        public int FloorTwoDeckY = 15;
        public int FloorThreeDeckY = 22;

        // Canopy clustering: how many branch tips get a foliage cluster, and the cluster radius.
        public int CanopyClusterCount = 12;
        public int CanopyRadius = 2;           // leaf puff radius (kept <=3 of its anchor log)

        public static TreehouseSpec DefaultKingdomOfTheForest()
        {
            return new TreehouseSpec();
        }

        public TreehouseSpec Clone()
        {
            return (TreehouseSpec)MemberwiseClone();
        }

        /// <summary>Master center of the trunk in the X/Z footprint.</summary>
        public int CenterX { get { return Width / 2; } }
        public int CenterZ { get { return Depth / 2; } }

        /// <summary>
        /// Refuse anything unbuildable up front (never throws). The footprint/height must
        /// fit the selection axis cap; every deck must leave a couple of wall courses plus a
        /// roof below the master ceiling; the canopy must sit above the top floor.
        /// </summary>
        public SpecValidationResult Validate()
        {
            var errors = new List<string>();

            if (Width < 15 || Width > HardMaxFootprint)
                errors.Add("Width " + Width + " 超出树屋范围 [15.." + HardMaxFootprint + "]。");
            if (Depth < 15 || Depth > HardMaxFootprint)
                errors.Add("Depth " + Depth + " 超出树屋范围 [15.." + HardMaxFootprint + "]。");
            if (Height < 24 || Height > HardMaxHeight)
                errors.Add("Height " + Height + " 超出树屋范围 [24.." + HardMaxHeight + "]。");

            // Any single axis over the selection cap is impossible to place in one batch.
            if (Width > BuildSafetyLimits.MaxSelectionAxisLength ||
                Depth > BuildSafetyLimits.MaxSelectionAxisLength ||
                Height > BuildSafetyLimits.MaxSelectionAxisLength)
                errors.Add("尺寸超过单轴上限 " + BuildSafetyLimits.MaxSelectionAxisLength + "。");

            if (FloorOneDeckY < 3) errors.Add("FloorOneDeckY 需 >= 3 以容纳底部支撑。");
            if (FloorTwoDeckY <= FloorOneDeckY + 4) errors.Add("第二层需高于一层至少 5 格以容纳一层墙体屋顶。");
            if (FloorThreeDeckY <= FloorTwoDeckY + 4) errors.Add("第三层需高于二层至少 5 格以容纳二层墙体屋顶。");
            if (FloorThreeDeckY + 6 > Height) errors.Add("第三层屋顶超出总高 Height。");
            if (TrunkTopY < FloorThreeDeckY) errors.Add("树干顶端 TrunkTopY 必须不低于第三层。");

            if (CanopyClusterCount < 4 || CanopyClusterCount > 40) errors.Add("CanopyClusterCount 需 4..40。");
            if (CanopyRadius < 1 || CanopyRadius > 3) errors.Add("CanopyRadius 需 1..3(须保证树叶在原木切比雪夫距离<=3 内不衰减)。");

            return new SpecValidationResult(errors);
        }
    }

    /// <summary>Outcome of <see cref="TreehouseSpec.Validate"/>.</summary>
    internal sealed class SpecValidationResult
    {
        private readonly List<string> _errors;
        public SpecValidationResult(List<string> errors) { _errors = errors ?? new List<string>(); }
        public bool IsValid { get { return _errors.Count == 0; } }
        public IReadOnlyList<string> Errors { get { return _errors; } }
        public string JoinedErrors() { return string.Join("; ", _errors.ToArray()); }
    }
}
