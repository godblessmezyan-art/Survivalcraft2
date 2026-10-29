using SimpleJson;

namespace SurvivalcraftRuins.Core;

internal static class PersistenceJson
{
    public static RuinsSaveData Parse(string json)
    {
        JsonObject root = JsonHelpers.ParseObject(json, "SurvivalcraftRuins.json");
        var result = new RuinsSaveData
        {
            DensityScale = JsonHelpers.Float(root, "DensityScale", 1f),
            LayoutVersion = JsonHelpers.Int(root, "LayoutVersion", 0),
        };
        foreach (object value in JsonHelpers.Array(root, "Ambushes", optional: true))
        {
            if (value is not JsonObject item)
            {
                throw new InvalidDataException("Ambush entry must be an object.");
            }
            result.Ambushes.Add(new AmbushState
            {
                Id = JsonHelpers.String(item, "Id"),
                X = JsonHelpers.Int(item, "X"),
                Y = JsonHelpers.Int(item, "Y"),
                Z = JsonHelpers.Int(item, "Z"),
                Creature = JsonHelpers.String(item, "Creature"),
                Min = JsonHelpers.Int(item, "Min"),
                Max = JsonHelpers.Int(item, "Max"),
                Radius = JsonHelpers.Float(item, "Radius"),
                Triggered = JsonHelpers.Bool(item, "Triggered"),
                Seed = JsonHelpers.Int(item, "Seed"),
                Aquatic = JsonHelpers.Bool(item, "Aquatic"),
                Substitute = JsonHelpers.String(item, "Substitute"),
            });
        }
        foreach (object value in JsonHelpers.Array(root, "Chests", optional: true))
        {
            if (value is not JsonObject item)
            {
                throw new InvalidDataException("Chest entry must be an object.");
            }
            result.Chests.Add(new PendingChest
            {
                X = JsonHelpers.Int(item, "X"),
                Y = JsonHelpers.Int(item, "Y"),
                Z = JsonHelpers.Int(item, "Z"),
                Pool = JsonHelpers.String(item, "Pool"),
                Seed = JsonHelpers.Int(item, "Seed"),
                Container = JsonHelpers.String(item, "Container"),
            });
        }
        return result;
    }

    public static string Serialize(RuinsSaveData data)
    {
        var root = new JsonObject
        {
            ["DensityScale"] = data.DensityScale,
            ["LayoutVersion"] = data.LayoutVersion,
        };
        var ambushes = new JsonArray();
        foreach (AmbushState item in data.Ambushes)
        {
            ambushes.Add(new JsonObject
            {
                ["Id"] = item.Id,
                ["X"] = item.X,
                ["Y"] = item.Y,
                ["Z"] = item.Z,
                ["Creature"] = item.Creature,
                ["Min"] = item.Min,
                ["Max"] = item.Max,
                ["Radius"] = item.Radius,
                ["Triggered"] = item.Triggered,
                ["Seed"] = item.Seed,
                ["Aquatic"] = item.Aquatic,
                ["Substitute"] = item.Substitute,
            });
        }
        root["Ambushes"] = ambushes;
        var chests = new JsonArray();
        foreach (PendingChest item in data.Chests)
        {
            chests.Add(new JsonObject
            {
                ["X"] = item.X,
                ["Y"] = item.Y,
                ["Z"] = item.Z,
                ["Pool"] = item.Pool,
                ["Seed"] = item.Seed,
                ["Container"] = item.Container,
            });
        }
        root["Chests"] = chests;
        return SimpleJson.SimpleJson.SerializeObject(root) ?? throw new InvalidOperationException("Persistence serialization failed.");
    }
}
