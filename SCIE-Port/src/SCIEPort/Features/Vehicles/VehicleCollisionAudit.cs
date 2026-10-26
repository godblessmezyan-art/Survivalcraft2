using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Vehicles
{
    /// <summary>
    /// Read-only collision audit for drivable vehicles. This helper never writes any
    /// physics value; it only measures the authored <c>ComponentBody.BoxSize</c> against
    /// the model's own geometry so a real device test can confirm whether the invisible
    /// collision volume agrees with the visible hull. It is completely silent unless
    /// <see cref="VehicleAuditOptions.Diagnostics"/> is turned on.
    /// </summary>
    internal static class VehicleCollisionAudit
    {
        /// <summary>
        /// Logs the authored collision box next to the model geometry bounds for one
        /// vehicle instance. Call this once from the drive component's <c>Load</c>.
        /// </summary>
        /// <param name="vehicleName">Human readable vehicle label used to prefix the log line.</param>
        /// <param name="body">The vehicle's native <see cref="ComponentBody"/> whose <c>BoxSize</c> is the collision volume.</param>
        /// <param name="model">The vehicle's <see cref="ComponentModel"/> providing the visible geometry bounds.</param>
        public static void LogOnce(string vehicleName, ComponentBody body, ComponentModel model)
        {
            if (!VehicleAuditOptions.Diagnostics || body == null)
            {
                return;
            }

            Vector3 boxSize = body.BoxSize;
            BoundingBox worldBox = body.BoundingBox;
            Log.Information(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[SCIEPort VehicleAudit] {0} authored BoxSize=({1:0.###},{2:0.###},{3:0.###}) worldMin=({4:0.###},{5:0.###},{6:0.###}) worldMax=({7:0.###},{8:0.###},{9:0.###})",
                vehicleName,
                boxSize.X,
                boxSize.Y,
                boxSize.Z,
                worldBox.Min.X,
                worldBox.Min.Y,
                worldBox.Min.Z,
                worldBox.Max.X,
                worldBox.Max.Y,
                worldBox.Max.Z));

            // The model bounds are in local model space (bone-transform not applied by us),
            // so this is a shape comparison, not a direct world overlap test. Reading both
            // lines is enough to tell whether the authored box is far taller or wider than
            // the visible geometry, which is what makes a player brush an invisible wall.
            if (model != null && model.Model != null)
            {
                foreach (ModelMesh mesh in model.Model.Meshes)
                {
                    BoundingBox mb = mesh.BoundingBox;
                    Log.Information(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "[SCIEPort VehicleAudit] {0} model bounds min=({1:0.###},{2:0.###},{3:0.###}) max=({4:0.###},{5:0.###},{6:0.###}) size=({7:0.###},{8:0.###},{9:0.###})",
                        vehicleName,
                        mb.Min.X,
                        mb.Min.Y,
                        mb.Min.Z,
                        mb.Max.X,
                        mb.Max.Y,
                        mb.Max.Z,
                        mb.Max.X - mb.Min.X,
                        mb.Max.Y - mb.Min.Y,
                        mb.Max.Z - mb.Min.Z));
                }
            }
        }
    }
}
