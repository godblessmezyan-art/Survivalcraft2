using Game;
using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Mod;

public sealed class RuinsModLoader : ModLoader
{
    public override void __ModInitialize()
    {
        ModsManager.RegisterHook("OnTerrainContentsGenerated", this);
        ModsManager.RegisterHook("SubsystemUpdate", this);
        try
        {
            RuinsRuntime.LoadAssets(Entity);
        }
        catch (Exception exception)
        {
            Engine.Log.Error($"[Ruins] Asset load failed: {exception.Message}");
        }
    }

    public override void OnTerrainContentsGenerated(TerrainChunk chunk)
    {
        try
        {
            RuinsRuntime.OnChunkGenerated(chunk);
        }
        catch (Exception exception)
        {
            Engine.Log.Error($"[Ruins] Chunk generation hook failed: {exception}");
        }
    }

    public override void SubsystemUpdate(float dt)
    {
        try
        {
            RuinsRuntime.Update(dt);
        }
        catch (Exception exception)
        {
            Engine.Log.Error($"[Ruins] Update hook failed: {exception}");
        }
    }

    public override void SaveSettings(System.Xml.Linq.XElement xElement)
    {
        RuinsSettings.Save(xElement);
    }

    public override void LoadSettings(System.Xml.Linq.XElement xElement)
    {
        RuinsSettings.Load(xElement);
    }
}
