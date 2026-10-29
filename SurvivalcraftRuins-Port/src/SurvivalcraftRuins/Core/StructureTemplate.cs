using SimpleJson;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Loads converted structure templates from JSON:
/// { "name": "...", "palette": [["BlockName", data], ...],
///   "cells": [[x,y,z,paletteIndex], ...], "chests": [[x,y,z], ...], "spawners": [[x,y,z], ...] }
/// Cells listed with the palette name "AirBlock" carve terrain; cells absent
/// from the list leave terrain untouched.
/// </summary>
public static class StructureTemplate
{
    public static StructureData Parse(string json)
    {
        JsonObject root = JsonHelpers.ParseObject(json, "structure template");
        string name = JsonHelpers.String(root, "name", "unnamed");

        var palette = new List<PaletteEntry>();
        foreach (object value in JsonHelpers.Array(root, "palette"))
        {
            if (value is not JsonArray pair || pair.Count < 2)
            {
                throw new InvalidDataException($"Structure '{name}' has an invalid palette entry.");
            }
            palette.Add(new PaletteEntry(Convert.ToString(pair[0]) ?? "AirBlock", JsonHelpers.Int(pair, 1)));
        }

        var cells = new List<Cell>();
        foreach (object value in JsonHelpers.Array(root, "cells"))
        {
            if (value is not JsonArray q || q.Count < 4)
            {
                throw new InvalidDataException($"Structure '{name}' has an invalid cell entry.");
            }
            cells.Add(new Cell(
                checked((short)JsonHelpers.Int(q, 0)), checked((short)JsonHelpers.Int(q, 1)), checked((short)JsonHelpers.Int(q, 2)),
                checked((ushort)JsonHelpers.Int(q, 3))));
        }

        var chests = new List<ChestMarker>();
        foreach (object value in JsonHelpers.Array(root, "chests", optional: true))
        {
            if (value is not JsonArray q || q.Count < 3)
            {
                throw new InvalidDataException($"Structure '{name}' has an invalid chest marker.");
            }
            chests.Add(new ChestMarker(checked((short)JsonHelpers.Int(q, 0)), checked((short)JsonHelpers.Int(q, 1)), checked((short)JsonHelpers.Int(q, 2))));
        }

        var ambushes = new List<AmbushMarker>();
        foreach (object value in JsonHelpers.Array(root, "spawners", optional: true))
        {
            if (value is not JsonArray q || q.Count < 3)
            {
                throw new InvalidDataException($"Structure '{name}' has an invalid spawner marker.");
            }
            ambushes.Add(new AmbushMarker(checked((short)JsonHelpers.Int(q, 0)), checked((short)JsonHelpers.Int(q, 1)), checked((short)JsonHelpers.Int(q, 2))));
        }

        return StructureData.FromCells(name, palette, cells, chests, ambushes);
    }
}
