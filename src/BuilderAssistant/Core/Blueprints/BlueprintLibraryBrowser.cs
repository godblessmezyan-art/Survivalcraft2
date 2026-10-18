namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// Pure, terrain-independent cursor that walks a blueprint library list when the
    /// player taps the blueprint tool. It knows nothing about files, the game or the
    /// UI - it only maps (count, current cursor) to the next index to load, which is
    /// what makes the browse/loop behavior offline-testable. The WorldEdit runtime
    /// feeds it the live file count and reads back the chosen index.
    /// </summary>
    internal static class BlueprintLibraryBrowser
    {
        /// <summary>
        /// Index to load after one tap, given the current number of library entries
        /// and the cursor left from the previous tap. The cursor advances by one and
        /// wraps around; a non-positive count yields -1 (nothing to load). The caller
        /// should store the returned value as the new cursor.
        /// </summary>
        public static int Advance(int count, int currentCursor)
        {
            if (count <= 0) return -1;
            int next = currentCursor + 1;
            if (next >= count) next = 0;
            return next;
        }

        /// <summary>
        /// A concise "[i/N]" position label for the toast message, using a 1-based
        /// display index. Returns an empty string when there is nothing to show.
        /// </summary>
        public static string PositionLabel(int count, int cursor)
        {
            if (count <= 0 || cursor < 0 || cursor >= count) return string.Empty;
            return "[" + (cursor + 1) + "/" + count + "]";
        }
    }
}
