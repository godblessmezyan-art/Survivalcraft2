namespace BuilderAssistant.Core.Building
{
    /// <summary>
    /// One reusable stage of building composition (foundation, walls, openings, roof,
    /// stairs, layout, decoration). A component reads the shared
    /// <see cref="BuildingContext"/> and writes blocks into its builder using only
    /// relative coordinates and the spec's material profile. Splitting generation into
    /// these stages is what lets a NEW building type reuse the SAME primitives instead of
    /// re-handwriting coordinate math, and lets each stage be unit-tested in isolation.
    ///
    /// The composer runs components in a fixed order; because the builder is
    /// last-write-wins, order is the single, explicit source of override precedence
    /// (slabs, then walls, then stairs carving through slabs, then openings carved into
    /// walls, then roof, then decoration).
    /// </summary>
    internal interface IBuildingComponent
    {
        void Generate(BuildingContext ctx);
    }
}
