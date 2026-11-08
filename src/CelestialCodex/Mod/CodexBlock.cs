using Engine;
using Engine.Graphics;
using Game;

namespace CelestialCodex.Mod
{
    public sealed class CelestialCodexBlock : FlatBlock
    {
        public const int Index = 956;
        private Texture2D texture;
        public override void Initialize() { texture = ContentManager.Get<Texture2D>("Textures/CelestialCodex/Book"); base.Initialize(); }
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        { BlocksManager.DrawFlatOrImageExtrusionBlock(renderer, value, size, ref matrix, texture, color, false, environmentData); }
        public override string GetDisplayName(SubsystemTerrain terrain, int value) { return "云天书典"; }
        public override string GetCategory(int value) { return "云天书典"; }
        public override int GetTextureSlotCount(int value) { return 1; }
        public override float GetMeleePower(int value) { return 0f; }
        public override float GetProjectilePower(int value) { return 0f; }
    }
    public sealed class CelestialCodexModLoader : ModLoader
    {
        public override void __ModInitialize() { Log.Information("[CelestialCodex] in-game local TXT/EPUB import source; x25.01.17 Android picker ABI pending."); }
    }
}
