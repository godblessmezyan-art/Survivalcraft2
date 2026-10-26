namespace SCIEPort.Features.Vehicles
{
    /// <summary>
    /// Runtime-tunable switch for the read-only vehicle collision audit. It is off by
    /// default and only ever gates diagnostic log output, so enabling it changes no
    /// physics value, no collision box and no movement behavior.
    /// </summary>
    public static class VehicleAuditOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether <see cref="VehicleCollisionAudit"/>
        /// emits the authored-box-versus-model-bounds comparison through the game log.
        /// Turn this on only while running a real-device collision test.
        /// </summary>
        public static bool Diagnostics { get; set; }
    }
}
