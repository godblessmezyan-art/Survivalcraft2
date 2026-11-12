using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SCIEPort.Features.CelestialCodex.Core
{
    /// <summary>Copies an explicitly selected file into the client's private book library.
    /// No server calls, no world writes, no overwrite of existing books.</summary>
    internal static class BookImportService
    {
        private const int MaxVisibleBooks = LocalBookLibrary.MaxBooks;
        private const int BufferSize = 8192;

        internal static bool Supported(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase));
        }

        internal static string FileName(string selectedPath)
        {
            if (string.IsNullOrEmpty(selectedPath)) throw new InvalidDataException("未选择文件");
            // Storage virtual paths are slash-delimited, but tolerate a Windows path returned by a picker.
            int cut = Math.Max(selectedPath.LastIndexOf('/'), selectedPath.LastIndexOf('\\'));
            string name = selectedPath.Substring(cut + 1);
            // A URI or opaque document ID without a filename must not be silently saved as a book.
            if (!Supported(name)) throw new InvalidDataException("仅支持 TXT 和 EPUB 文件");
            return NormalizeName(name);
        }

        private static string NormalizeName(string raw)
        {
            if (!Supported(raw) || raw.Length > 180) throw new InvalidDataException("文件名无效");
            var safe = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsControl(c) || c == '/' || c == '\\' || c == ':' ||
                    c == '<' || c == '>' || c == '|' || c == '"' || c == '?' || c == '*')
                    throw new InvalidDataException("文件名包含非法字符");
                safe.Append(c);
            }
            string name = safe.ToString().Trim();
            if (name.Length == 0 || name.Length > 150 || name.StartsWith(".", StringComparison.Ordinal) ||
                name == ".." || !Supported(name))
                throw new InvalidDataException("文件名不受支持");
            return name;
        }

        internal static string Copy(string originalName, Stream source, IFileStore destination, CancellationToken token)
        {
            if (source == null || !source.CanRead || destination == null) throw new InvalidDataException("文件无法读取");
            string name = NormalizeName(originalName);
            token.ThrowIfCancellationRequested();
            destination.Create();

            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int entryCount = 0, bookCount = 0;
            foreach (string existing in destination.Names())
            {
                token.ThrowIfCancellationRequested();
                if (++entryCount > 512) throw new InvalidDataException("图书目录文件过多");
                if (existing == null) continue;
                occupied.Add(existing);
                if (Supported(existing)) bookCount++;
            }
            if (bookCount >= MaxVisibleBooks) throw new InvalidDataException("书库已满（64 本）");

            string extension = name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) ? ".epub" : ".txt";
            string stem = name.Substring(0, name.Length - extension.Length);
            string finalName = name;
            for (int suffix = 2; occupied.Contains(finalName); suffix++)
            {
                if (suffix > 9999) throw new InvalidDataException("重名文件过多");
                string tail = " (" + suffix + ")" + extension;
                string shortened = stem.Length > 150 - tail.Length ? stem.Substring(0, 150 - tail.Length) : stem;
                finalName = shortened + tail;
            }

            string stage = ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
            bool stageCreated = false;
            try
            {
                using (Stream output = destination.Write(stage))
                {
                    stageCreated = true;
                    byte[] buffer = new byte[BufferSize];
                    int total = 0, count;
                    while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        if (count > LocalBookLibrary.MaxSourceBytes - total)
                            throw new InvalidDataException("单本图书不得超过 8 MiB");
                        output.Write(buffer, 0, count);
                        total += count;
                    }
                    if (total == 0) throw new InvalidDataException("文件内容为空");
                    token.ThrowIfCancellationRequested();
                }
                destination.Move(stage, finalName);
                stageCreated = false;
                return finalName;
            }
            finally
            {
                if (stageCreated)
                {
                    try { destination.Delete(stage); }
                    catch (Exception) { /* Preserve the original error; .tmp files are ignored by the library. */ }
                }
            }
        }
    }
}
