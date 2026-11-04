using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.CelestialCodex.Mod
{
    /// <summary>Client reading tool integrated from the tested Celestial Codex 0.1.4.</summary>
    public sealed class CelestialCodexBlock : FlatBlock
    {
        /// <summary>Preserves the standalone item identity for existing inventories.</summary>
        public const int Index = 956;
        private Texture2D texture;
        /// <inheritdoc/>
        public override void Initialize() { texture = ContentManager.Get<Texture2D>("Textures/CelestialCodex/Book"); base.Initialize(); }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        { BlocksManager.DrawFlatOrImageExtrusionBlock(renderer, value, size, ref matrix, texture, color, false, environmentData); }
        /// <inheritdoc/>
        public override string GetDisplayName(SubsystemTerrain terrain, int value) { return "云天书典"; }
        /// <inheritdoc/>
        public override string GetCategory(int value) { return Inventory.PortInventoryRules.Tools; }
        /// <inheritdoc/>
        public override int GetTextureSlotCount(int value) { return 1; }
        /// <inheritdoc/>
        public override float GetMeleePower(int value) { return 0f; }
        /// <inheritdoc/>
        public override float GetProjectilePower(int value) { return 0f; }
    }
}
