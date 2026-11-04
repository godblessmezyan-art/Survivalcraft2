using System.Collections.Generic;

namespace SCIEPort.Features.CelestialCodex.Core
{
    internal static class ChapterNavigation
    {
        // Android 7723's BCL has no List<T>.Exists. Use only indexed access here.
        internal static int IndexOf(List<Chapter> chapters, string id)
        {
            if (chapters == null || id == null) return -1;
            for (int i = 0; i < chapters.Count; i++) if (chapters[i].Id == id) return i;
            return -1;
        }
    }
}
