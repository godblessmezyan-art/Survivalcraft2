using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.CelestialCodex.Core;
using SCIEPort.Features.CelestialCodex.Networking;
using Engine;

namespace SCIEPort.Features.CelestialCodex.Mod
{
    internal sealed class StorageAdapter : IFileStore
    {
        private readonly string root;
        internal StorageAdapter(string root) { this.root = root; }
        private string Path(string name)
        {
            if (name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name.IndexOf(':') >= 0 || name == "..") throw new InvalidDataException("Storage name");
            return Storage.CombinePaths(root, name);
        }
        public void Create() { Storage.CreateDirectory(root); }
        public IEnumerable<string> Names() { return Storage.ListFileNames(root); }
        public long Size(string name) { return Storage.GetFileSize(Path(name)); }
        public DateTime Modified(string name) { return Storage.GetFileLastWriteTime(Path(name)); }
        public Stream Read(string name) { return Storage.OpenFile(Path(name), OpenFileMode.Read); }
        public Stream Write(string name) { return Storage.OpenFile(Path(name), OpenFileMode.Create); }
        public void Move(string source, string destination) { Storage.MoveFile(Path(source), Path(destination)); }
        public void Delete(string name) { Storage.DeleteFile(Path(name)); }
    }
    internal sealed class ReaderSession : IDisposable
    {
        private readonly RequestGate gate = new RequestGate(LogNetwork);
        private readonly string root;
        private readonly PositionWriter positions;
        private readonly PositionWriter localPositions;
        private volatile bool offline;
        private bool local;
        internal ReaderSession()
        {
            // Read runtime metadata, avoiding inlining a Windows const into an Android assembly.
            FieldInfo field = typeof(ModsManager).GetField("UserDataPath", BindingFlags.Public | BindingFlags.Static);
            string data = field == null ? null : field.GetValue(null) as string;
            if (string.IsNullOrEmpty(data)) throw new InvalidDataException("UserDataPath");
            string parent = Storage.GetDirectoryName(data);
            if (string.IsNullOrEmpty(parent)) throw new InvalidDataException("UserDataPath parent");
            root = Storage.CombinePaths(parent, "CelestialCodex");
            string directory = root;
            positions = new PositionWriter(bytes => new BoundedCache(new StorageAdapter(directory)).Write("position.json", bytes));
            localPositions = new PositionWriter(bytes =>
            {
                var store = new StorageAdapter(directory); store.Create();
                using (Stream output = store.Write("local-position.tmp")) output.Write(bytes, 0, bytes.Length);
                store.Move("local-position.tmp", "local-position.json");
            });
        }
        private string Endpoint(StorageAdapter store)
        {
            string endpoint = SourcePolicy.DefaultEndpoint;
            try
            {
                if (store.Size("endpoint.txt") > 2048) throw new InvalidDataException("Endpoint file size");
                using (Stream input = store.Read("endpoint.txt"))
                { byte[] bytes = BoundedCache.ReadLimited(input, 2048); endpoint = new UTF8Encoding(false, true).GetString(bytes, 0, bytes.Length).Trim(); }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return SourcePolicy.Validate(endpoint);
        }
        internal void SetLocal(bool enabled)
        {
            gate.Cancel(); local = enabled;
            if (!local) offline = false;
        }
        private ReadingPosition LocalPosition()
        {
            try
            {
                var store = new StorageAdapter(root);
                if (store.Size("local-position.json") > 2048) return new ReadingPosition();
                using (Stream input = store.Read("local-position.json"))
                    return ReadingPosition.Decode(BoundedCache.ReadLimited(input, 2048));
            }
            catch (Exception) { return new ReadingPosition(); }
        }
        internal void Begin(Request request)
        {
            // The delegate captures this session and an immutable request, never a Dialog.
            if (local)
            {
                gate.Begin(token =>
                {
                    var source = new StorageAdapter(Storage.CombinePaths(root, "Imports"));
                    ReaderResult result = new LocalBookLibrary(source).Read(request, token);
                    result.Position = LocalPosition();
                    return Task.FromResult(result);
                });
                return;
            }
            gate.Begin(async token =>
            {
                var files = new StorageAdapter(root);
                var client = new LibraryClient(new BoundedCache(files), Endpoint(files), null, LogNetwork);
                var result = await client.Read(request, token, offline).ConfigureAwait(false);
                if (!token.IsCancellationRequested && result.Offline) offline = true;
                return result;
            });
        }
        internal string ImportFile(string selectedPath, CancellationToken token)
        {
            // The source is explicitly chosen by the player or selected in the in-game browser.
            // No server/network state and no world save state are accessed.
            if (string.IsNullOrEmpty(selectedPath) || selectedPath.Length > 2048)
                throw new InvalidDataException("文件路径无效");
            string name = BookImportService.FileName(selectedPath);
            token.ThrowIfCancellationRequested();
            using (Stream input = Storage.OpenFile(selectedPath, OpenFileMode.Read))
            {
                var store = new StorageAdapter(Storage.CombinePaths(root, "Imports"));
                return BookImportService.Copy(name, input, store, token);
            }
        }
        internal ReaderResult Take() { return gate.Take(); }
        private static void LogNetwork(string message) { Log.Error(message); }
        internal void Cancel() { gate.Cancel(); }
        internal void GoOnline() { offline = false; }
        internal bool IsOffline { get { return offline; } }
        internal void Save(ReadingPosition position)
        {
            // Snapshot serialization is bounded and in memory; disk work always runs on a worker.
            if (local) localPositions.Save(position.Encode());
            else positions.Save(position.Encode());
        }
        public void Dispose() { gate.Dispose(); }
    }
}
