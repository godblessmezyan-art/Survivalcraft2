using System;
using System.Text;
using SCIEPort.Features.CelestialCodex.Core;

internal static class ReaderPresentationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var size in new[] { new[] { 320f, 240f }, new[] { 375f, 812f }, new[] { 812f, 375f }, new[] { 1280f, 720f }, new[] { 2340f, 1080f }, new[] { 768f, 1024f } })
        {
            var l = new ReaderLayout(size[0], size[1]);
            check(l.ButtonWidth >= 48 && l.ContentHeight >= 48, "48 logical-unit touch and content minimum");
            check(l.ContentY >= 80 && l.ContentY + l.ContentHeight + 16 <= l.FooterY, "content clears header/status/footer");
            check(l.FooterY + 48 <= l.Height - 16 && l.ContentX >= 16, "footer and side gutters");
            check(l.ContentWidth <= 720 && l.ContentX + l.ContentWidth <= l.Width - 16, "bounded reading measure");
            check(l.ContentX + 4 * l.ButtonWidth + 24 <= l.Width - 16, "four actions fit with three 8-unit gaps");
        }
        foreach (ReaderTheme theme in new[] { ReaderTheme.Paper, ReaderTheme.Night })
        {
            foreach (int foreground in new[] { theme.Ink, theme.Muted })
            foreach (int background in new[] { theme.Background, theme.Surface, theme.Pressed })
                check(Contrast(foreground, background) >= 4.5, "normal text contrast in both themes and pressed state");
            check(Contrast(theme.OnAccent, theme.Accent) >= 4.5, "continue card contrast");
            check(Contrast(theme.Border, theme.Surface) >= 3, "button boundary contrast");
        }
        var old = Encoding.UTF8.GetBytes("{\"slug\":\"test\",\"revision\":\"" + new string('a', 64) + "\",\"chapter\":\"c1\",\"scroll\":650000,\"font\":4}");
        ReadingPosition position = ReadingPosition.Decode(old);
        check(position.Slug == "test" && position.Theme == 0 && position.Scroll == 650000, "old position keeps scroll and defaults to paper");
        position.Theme = 1;
        ReadingPosition restored = ReadingPosition.Decode(position.Encode());
        check(restored.Theme == 1 && restored.Font == 4 && restored.Scroll == 650000, "theme/font/position round trip");
        check(ReadingPosition.Decode(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(position.Encode()).Replace("\"theme\":1", "\"theme\":2"))).Slug == "", "corrupt theme degrades safely");
    }
    private static double Channel(int value) { double c = value / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
    private static double Luminance(int rgb) { return .2126 * Channel((rgb >> 16) & 255) + .7152 * Channel((rgb >> 8) & 255) + .0722 * Channel(rgb & 255); }
    private static double Contrast(int a, int b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
}
