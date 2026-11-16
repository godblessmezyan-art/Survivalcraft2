using Engine;

namespace SCIEPort.Compatibility
{
    /// <summary>Collision-box ABI for the fixed SCNET target.</summary>
    internal static class BlockRaycastSafety
    {
        // Block.Raycast unconditionally reads boxes[0], even when no box was hit.
        // A zero-volume sentinel preserves an empty physical shape without crashing
        // native sleep/shelter/temperature rays. Non-solid blocks also disable collision.
        internal static readonly BoundingBox[] NonSolid = { new BoundingBox(Vector3.Zero, Vector3.Zero) };
    }
}
