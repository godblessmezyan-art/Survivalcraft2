namespace SurvivalcraftRuins.Core;

internal static class CompatMath
{
    public static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

    public static float Round(float value) => (float)Math.Round(value);

    public static float Min(float left, float right) => Math.Min(left, right);

    public static float Max(float left, float right) => Math.Max(left, right);

    public static float Sin(float value) => (float)Math.Sin(value);

    public static float Cos(float value) => (float)Math.Cos(value);

    public const float Pi = (float)Math.PI;
}
