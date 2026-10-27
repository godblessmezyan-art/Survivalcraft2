namespace SCIEPort.Features.FourSaints
{
    /// <summary>Pure presentation budgets; no gameplay value is defined here.</summary>
    internal static class FourSaintsVisualRules
    {
        internal const double QinglongPulseSeconds = 0.8;
        internal const double XuanwuPulseSeconds = 0.55;
        internal const double XuanwuThrottleSeconds = 0.35;

        internal static int MaximumGlyphs(int quality)
        {
            return quality <= 0 ? 5 : quality == 1 ? 9 : 12;
        }
    }
}
