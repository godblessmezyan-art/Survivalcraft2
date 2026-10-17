using Game;

namespace SCIEPort.Mod
{
    /// <summary>SCNET x25.01.17 loader entry for the SCIE port.</summary>
    public sealed class SCIEPortLoader : ModLoader
    {
        /// <inheritdoc />
        public override void __ModInitialize()
        {
            // Registration of the probe block is performed by SCNET's normal
            // assembly type scan plus Assets/Blocks/SCIEPort.csv.
        }
    }
}
