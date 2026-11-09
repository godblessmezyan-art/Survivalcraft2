using System;
using System.IO;

namespace BuilderAssistant.Networking
{
    internal sealed class BlueprintCloudCache
    {
        private static readonly object Gate = new object();
        private readonly string directory;
        internal BlueprintCloudCache(string directory) { this.directory = directory; }
        private string PathFor(string key)
        {
            if (string.IsNullOrEmpty(directory)) throw new IOException("游戏用户目录尚未就绪。");
            if (string.IsNullOrEmpty(key) || key.Length > 160 || !key.EndsWith(".cache", StringComparison.Ordinal)) throw new IOException("缓存名称无效。");
            foreach (char c in key) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '.') throw new IOException("缓存名称无效。");
            return Path.Combine(directory, key);
        }
        internal byte[] Read(string key)
        {
            lock (Gate)
            {
                try
                {
                    string path = PathFor(key);
                    using (var input = File.OpenRead(path))
                    using (var output = new MemoryStream())
                    {
                        if (input.Length > BlueprintCloudProtocol.MaxBytes) return null;
                        byte[] buffer = new byte[4096]; int read;
                        while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, BlueprintCloudProtocol.MaxBytes + 1 - (int)output.Length))) > 0)
                        { output.Write(buffer, 0, read); if (output.Length > BlueprintCloudProtocol.MaxBytes) return null; }
                        return output.ToArray();
                    }
                }
                catch (Exception) { return null; }
            }
        }
        internal void Delete(string key) { lock (Gate) { try { File.Delete(PathFor(key)); } catch (Exception) { } } }
        internal bool Write(string key, byte[] bytes)
        {
            lock (Gate)
            {
                string temp = null;
                try
                {
                    if (bytes == null || bytes.Length > BlueprintCloudProtocol.MaxBytes) return false;
                    string path = PathFor(key); Directory.CreateDirectory(directory);
                    temp = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
                    File.WriteAllBytes(temp, bytes);
                    if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                    Trim(path); return true;
                }
                catch (Exception) { return false; }
                finally { if (temp != null) try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { } }
            }
        }
        private void Trim(string keep)
        {
            // Only this private cache, never the player's saved BlueprintStorage library.
            while (true)
            {
                var files = new DirectoryInfo(directory).GetFiles("*.cache"); long total = 0; FileInfo oldest = null;
                foreach (FileInfo file in files) { total += file.Length; if (file.FullName != keep && (oldest == null || file.LastWriteTimeUtc < oldest.LastWriteTimeUtc)) oldest = file; }
                if ((files.Length <= 128 && total <= 32L * 1024 * 1024) || oldest == null) return;
                oldest.Delete();
            }
        }
    }
}
