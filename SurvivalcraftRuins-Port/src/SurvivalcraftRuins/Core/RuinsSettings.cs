using System.Xml.Linq;

namespace SurvivalcraftRuins.Core;

/// <summary>
/// Player-facing knobs, persisted by the game through ModLoader's settings
/// hooks. Density is only consulted when a world is first visited: it is then
/// frozen into that world's save so a later change cannot leave half-written
/// structures stranded in already-generated chunks.
/// </summary>
public static class RuinsSettings
{
    public const float MinScale = 0f;
    public const float MaxScale = 3f;

    /// <summary>Multiplier on every slot's spawn chance. 1 = tuned default.</summary>
    public static float Density { get; set; } = 1f;

    /// <summary>Multiplier on guard counts. 0 disables guards entirely.</summary>
    public static float Guards { get; set; } = 1f;

    public static void Save(XElement element)
    {
        element.SetAttributeValue("Density", Density.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        element.SetAttributeValue("Guards", Guards.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    public static void Load(XElement element)
    {
        Density = ReadScale(element, "Density", 1f);
        Guards = ReadScale(element, "Guards", 1f);
    }

    private static float ReadScale(XElement element, string name, float fallback)
    {
        var attribute = element.Attribute(name);
        if (attribute != null
            && float.TryParse(attribute.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value))
        {
            return CompatMath.Clamp(value, MinScale, MaxScale);
        }
        return fallback;
    }
}
