namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// One cell of a blueprint: a position relative to the blueprint origin, a
    /// LOGICAL material key (never a raw block index), and - since schema v2 - an
    /// authored block <see cref="Family"/> plus <see cref="Data"/>. The family tells
    /// the pure Core how the data bits must be transformed on rotate/mirror; the data
    /// holds only authored orientation (rotation / top-slab / hinge / fence arms),
    /// never engine-derived state (stair corners, colors), which are stripped by
    /// <see cref="BlockOrientation.Normalize"/>. Plain cubes keep family Cube and data 0,
    /// so the pre-v2 behaviour is exactly reproduced by the two-argument constructor.
    /// Resolving a key to a concrete block stays the job of
    /// <see cref="Transactions.IBlockValueResolver"/>, which makes material
    /// substitution and future palette swaps easy.
    /// </summary>
    internal readonly struct BlueprintCell
    {
        public readonly CellPos Relative;
        public readonly string MaterialKey;
        public readonly BlockFamily Family;
        public readonly int Data;

        /// <summary>Pre-v2 shape: a plain cube with no authored orientation.</summary>
        public BlueprintCell(CellPos relative, string materialKey)
        {
            Relative = relative;
            MaterialKey = materialKey;
            Family = BlockFamily.Cube;
            Data = 0;
        }

        /// <summary>Schema v2 shape: an oriented cell. Data is normalized to the
        /// family's authored bits on construction so a cell can never carry transient
        /// engine bits (stair corners / color) into a blueprint.</summary>
        public BlueprintCell(CellPos relative, string materialKey, BlockFamily family, int data)
        {
            Relative = relative;
            MaterialKey = materialKey;
            Family = family;
            Data = BlockOrientation.Normalize(family, data);
        }
    }
}
