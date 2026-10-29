using System.Collections.Generic;
using System.IO;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Infrastructure
{
    /// <summary>Uses the engine-resolved directory on Android and Windows.</summary>
    internal static class BlueprintStorage
    {
        public static string BaseDirectory
        {
            get { return string.IsNullOrEmpty(ModsManager.UserDataPath) ? null : Path.Combine(ModsManager.UserDataPath, "BuilderAssistant/blueprints"); }
        }
        private static BlueprintFileLibrary Library { get { return new BlueprintFileLibrary(BaseDirectory); } }
        public static string SanitizeName(string raw) { return BlueprintNamePolicy.ToStem(raw); }
        public static string Save(string name, string json, out string filename) { return Library.Save(name, json, out filename); }
        public static string Load(string stem, out string error) { return Library.Load(stem, out error); }
        public static List<string> ListNames(out string error) { return Library.ListNames(out error); }
        public static string Delete(string stem) { return Library.Delete(stem); }
        public static Blueprint LoadBlueprint(string stem, out string error)
        {
            string json = Load(stem, out error);
            if (json == null) return null;
            BlueprintImportResult result = BlueprintImportService.FromString(json, null);
            error = result.Success ? null : result.Detail;
            return result.Success ? result.Blueprint : null;
        }
    }
}
