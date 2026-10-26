namespace SCIEPort.Features.IndustrialFurniture
{
    /// <summary>
    /// Runtime-tunable settings for the geometry-derived furniture collision. These
    /// are read once during block initialization and are not touched by the movement
    /// or rendering pipeline, so changing them never alters a model's appearance.
    /// </summary>
    public static class FurnitureCollisionOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether furniture collision diagnostics are
        /// emitted through the game log. When enabled, each furniture block records its
        /// authored local box count, local height range and, for a queried cell, the
        /// absolute world-space upper and lower bounds so the physical surface can be
        /// compared against the visible surface on a real device.
        /// </summary>
        public static bool Diagnostics { get; set; }

        /// <summary>Gets the voxel subdivisions across local X (0.125 blocks).</summary>
        public static int GridX { get; } = 8;

        /// <summary>Gets the voxel subdivisions across local Y (0.05 blocks of height).</summary>
        public static int GridY { get; } = 20;

        /// <summary>Gets the voxel subdivisions across local Z (0.125 blocks).</summary>
        public static int GridZ { get; } = 8;

        /// <summary>
        /// Gets the height quantum implied by <see cref="GridY"/>. The physical top of a
        /// collision box can round up to at most this much above the visible geometric top,
        /// which is the smallest representable step and the practical tolerance for the
        /// 0.25 / 0.35 / 0.5 / 0.75 / 1.0 acceptance heights.
        /// </summary>
        public static double HeightQuantum => 1d / GridY;
    }
}
