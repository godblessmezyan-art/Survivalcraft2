using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Networking;

namespace BuilderAssistant.Infrastructure
{
    /// <summary>Uses the engine-resolved directory on Android and Windows.</summary>
    internal static class BlueprintStorage
    {
        public static string BaseDirectory
        {
            get
            {
                // UserDataPath identifies a virtual FILE (app:/UserId.dat), not a directory.
                // Resolve its parent through the target engine before using System.IO.
                FieldInfo field = typeof(ModsManager).GetField("UserDataPath", BindingFlags.Public | BindingFlags.Static);
                string data = field == null ? null : field.GetValue(null) as string;
                if (string.IsNullOrEmpty(data)) return null;
                string parent = Engine.Storage.GetDirectoryName(data);
                return string.IsNullOrEmpty(parent) ? null : Engine.Storage.GetSystemPath(Engine.Storage.CombinePaths(parent, "BuilderAssistant/blueprints"));
            }
        }
        private static BlueprintFileLibrary Library { get { return new BlueprintFileLibrary(BaseDirectory); } }
        public static string SanitizeName(string raw) { return BlueprintNamePolicy.ToStem(raw); }
        public static string Save(string name, string json, out string filename) { return Library.Save(name, json, out filename); }
        public static string Load(string stem, out string error) { return Library.Load(stem, out error); }
        public static List<string> ListNames(out string error) { return Library.ListNames(out error); }
        public static string Delete(string stem) { return Library.Delete(stem); }
        internal static string SaveCloud(CloudBlueprintDownload download, out string filename)
        {
            // Call after the current runtime's full material preflight succeeds.
            return Library.SaveExact("cloud-" + download.Entry.Id + "-r" + download.Entry.Revision, download.Json, out filename);
        }
        internal static BlueprintCloudClient CloudClient()
        {
            string root = BaseDirectory;
            return new BlueprintCloudClient(new BlueprintCloudCache(string.IsNullOrEmpty(root) ? null : Path.Combine(Path.GetDirectoryName(root), "cloud-blueprints")));
        }
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
