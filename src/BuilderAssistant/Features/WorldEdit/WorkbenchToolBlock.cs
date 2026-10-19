using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Held by the player to open the v0.11 Visual Building Workbench: a guided,
    /// touch-only flow that SELECTs a building (cottage / two-storey house / the
    /// currently imported clipboard blueprint), ADJUSTs a small set of discrete
    /// parameters, sets ORIENTATION, shows a read-only footprint PREVIEW, and only
    /// then CONFIRMs a build that is fed back through the UNCHANGED plan -> execute
    /// -> undo pipeline. All logic lives in the game-free Core/Workbench state
    /// machine; this class exists solely so a tap fires <c>OnUse</c> on the main
    /// thread. See <see cref="WorkbenchSession"/>.
    ///
    /// Like the other tools it is a plain painted cube so it carries no orientation
    /// data bits. It reuses the VERIFIED glass texture slot (15) rather than an
    /// unverified one, because picking a base slot that does not resolve on this
    /// build could throw at render time; sharing a texture with the Blueprint Tool
    /// (903) is harmless (two distinct block indices may wear the same cube), it just
    /// means the two "design" tools look alike. Substituting a UNIQUE confirmed slot
    /// is a cosmetic on-device refinement and is recorded as 待验证 in COMPATIBILITY.md.
    /// Index 904 is above the native x25.01.17 range (0-263) and collides with none of
    /// the Speedboat (900), build (901), undo (902) or blueprint (903) tools; whether
    /// 904 clashes with another third-party mod on a given device is a separate, real
    /// -device concern (待验证) - SCNET keeps no global third-party index registry.
    /// </summary>
    public sealed class WorkbenchToolBlock : PaintedCubeBlock
    {
        /// <summary>SCNET block content index reserved by this tool.</summary>
        public const int Index = 904;

        /// <summary>Constructs the tool using the glass cube texture slot (verified).</summary>
        public WorkbenchToolBlock() : base(15)
        {
        }
    }
}
