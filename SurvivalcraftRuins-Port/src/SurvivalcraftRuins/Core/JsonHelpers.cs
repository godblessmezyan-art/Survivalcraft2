using SimpleJson;

namespace SurvivalcraftRuins.Core;

internal static class JsonHelpers
{
    public static JsonObject ParseObject(string json, string source)
    {
        object? value = SimpleJson.SimpleJson.DeserializeObject(json);
        if (value is not JsonObject result)
        {
            throw new InvalidDataException($"{source} root must be a JSON object.");
        }
        return result;
    }

    public static JsonObject Object(JsonObject parent, string property)
    {
        if (!parent.TryGetValue(property, out object value) || value is not JsonObject result)
        {
            throw new InvalidDataException($"JSON property '{property}' must be an object.");
        }
        return result;
    }

    public static JsonArray Array(JsonObject parent, string property, bool optional = false)
    {
        if (!parent.TryGetValue(property, out object value))
        {
            if (optional)
            {
                return new JsonArray();
            }
            throw new InvalidDataException($"JSON property '{property}' is required.");
        }
        if (value is not JsonArray result)
        {
            throw new InvalidDataException($"JSON property '{property}' must be an array.");
        }
        return result;
    }

    public static string String(JsonObject parent, string property, string fallback = "")
    {
        return parent.TryGetValue(property, out object value) && value != null
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? fallback
            : fallback;
    }

    public static int Int(JsonObject parent, string property, int fallback = 0)
    {
        return parent.TryGetValue(property, out object value) && value != null
            ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;
    }

    public static int Int(JsonArray array, int index)
    {
        if (index < 0 || index >= array.Count)
        {
            throw new InvalidDataException($"JSON array index {index} is missing.");
        }
        return Convert.ToInt32(array[index], System.Globalization.CultureInfo.InvariantCulture);
    }

    public static float Float(JsonObject parent, string property, float fallback = 0f)
    {
        return parent.TryGetValue(property, out object value) && value != null
            ? Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;
    }

    public static bool Bool(JsonObject parent, string property, bool fallback = false)
    {
        return parent.TryGetValue(property, out object value) && value != null
            ? Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture)
            : fallback;
    }
}
