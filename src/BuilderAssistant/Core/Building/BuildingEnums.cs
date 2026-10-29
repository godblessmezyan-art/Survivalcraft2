namespace BuilderAssistant.Core.Building
{
    /// <summary>Which building the composer should lay out.</summary>
    internal enum BuildingType
    {
        /// <summary>Single-storey cottage (the original structure, now parameterized).</summary>
        Cottage = 0,
        /// <summary>A real two-storey house: two usable floors, load-bearing structure,
        /// a walkable stair between them, and a gabled roof.</summary>
        HouseTwoStory = 1,
        /// <summary>Single-storey furnished living room and bedroom.</summary>
        FurnishedHome = 2,
    }

    /// <summary>Roof shape the <c>RoofGenerator</c> produces.</summary>
    internal enum RoofType
    {
        /// <summary>Stepped pyramid converging to a ridge/point (the cottage's hip roof).</summary>
        Hip = 0,
        /// <summary>Two sloping sides meeting at a single ridge along the longer axis.</summary>
        Gabled = 1,
        /// <summary>Flat deck with a parapet / railing (usable as a terrace).</summary>
        Flat = 2,
    }
}
