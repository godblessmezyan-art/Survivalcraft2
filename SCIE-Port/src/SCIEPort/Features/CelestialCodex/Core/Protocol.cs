using System;
using System.Collections.Generic;
using System.IO;

namespace SCIEPort.Features.CelestialCodex.Core
{
    internal sealed class Book
    {
        internal string Slug, Title, Author, ResourceId, SourceMode, Revision, Status, Reason;
        internal readonly List<Chapter> Chapters = new List<Chapter>();
    }
    internal sealed class Chapter { internal string Id, Title, Text; }
    internal sealed class Document
    {
        internal readonly List<Book> Books = new List<Book>();
        internal Book Book;
        internal Chapter Chapter;
        internal int Page;
        internal bool HasMore;
    }
    internal static class Protocol
    {
        internal static Dictionary<string, object> Object(object value) { var obj = value as Dictionary<string, object>; if (obj == null) throw new InvalidDataException("JSON object"); return obj; }
        internal static object Field(Dictionary<string, object> obj, string name) { object value; if (!obj.TryGetValue(name, out value)) throw new InvalidDataException("Missing " + name); return value; }
        internal static string String(Dictionary<string, object> obj, string name, int max, bool nullable = false)
        {
            object value = Field(obj, name); if (nullable && value == null) return "";
            string s = value as string; if (s == null || s.Length > max) throw new InvalidDataException("Invalid " + name); return s;
        }
        internal static int Integer(Dictionary<string, object> obj, string name, int min, int max)
        {
            object value = Field(obj, name); if (!(value is long) || (long)value < min || (long)value > max) throw new InvalidDataException("Invalid " + name); return (int)(long)value;
        }
        internal static bool SafeSlug(string slug)
        {
            if (string.IsNullOrEmpty(slug) || slug.Length > 160 || slug[0] == '-' || slug[slug.Length - 1] == '-') return false;
            char last = '\0'; foreach (char c in slug) { if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-') return false; if (last == '-' && c == '-') return false; last = c; } return true;
        }
        internal static bool Revision(string value) { if (value == null || value.Length != 64) return false; foreach (char c in value) if (!(c >= 'a' && c <= 'f') && !(c >= '0' && c <= '9')) return false; return true; }
        internal static bool ChapterId(string value)
        {
            if (value == null || value.Length < 2 || value.Length > 4 || value[0] != 'c' || value[1] < '1' || value[1] > '9') return false;
            int number = 0; for (int i = 1; i < value.Length; i++) { if (value[i] < '0' || value[i] > '9') return false; number = number * 10 + value[i] - '0'; }
            return number <= 512;
        }
        private static List<object> Array(Dictionary<string, object> obj, string name, int max) { var list = Field(obj, name) as List<object>; if (list == null || list.Count > max) throw new InvalidDataException("Invalid " + name); return list; }
        private static Chapter ReadChapter(object value, bool body)
        {
            var o = Object(value); var chapter = new Chapter { Id = String(o, "id", 4), Title = String(o, "title", 80) };
            if (!ChapterId(chapter.Id)) throw new InvalidDataException("Chapter ID");
            if (body) chapter.Text = String(o, "text", 12000);
            return chapter;
        }
        private static Book ReadBook(Dictionary<string, object> o, bool detail)
        {
            var b = new Book { Slug = String(o, "slug", 160), Title = String(o, "title", 255), Author = String(o, "author", 160), ResourceId = String(o, "resourceId", 80, true), SourceMode = String(o, "sourceMode", 24, true), Revision = String(o, "revision", 64, true), Status = String(o, "status", 24), Reason = String(o, "reason", 80, true) };
            if (!SafeSlug(b.Slug) || b.Title.Length == 0 || (b.SourceMode != "original" && b.SourceMode != "ai_reading" && b.SourceMode != "")) throw new InvalidDataException("Book metadata");
            if (b.Status != "available" && b.Status != "ready" && b.Status != "empty" && b.Status != "unsupported" && b.Status != "unavailable") throw new InvalidDataException("Book status");
            if (detail && (b.Status == "ready" || b.Status == "empty") && !Revision(b.Revision)) throw new InvalidDataException("Book revision");
            return b;
        }
        internal static Document Decode(byte[] bytes, string slug = null, string chapterId = null, string revision = null)
        {
            var o = Object(StrictJson.Parse(bytes)); Integer(o, "schemaVersion", 1, 1); var document = new Document();
            if (slug == null)
            {
                document.Page = Integer(o, "page", 1, 10000);
                object more = Field(o, "hasMore"); if (!(more is bool)) throw new InvalidDataException("Pagination"); document.HasMore = (bool)more;
                var slugs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in Array(o, "books", 50)) { var b = ReadBook(Object(value), false); if (!slugs.Add(b.Slug)) throw new InvalidDataException("Duplicate book"); document.Books.Add(b); }
            }
            else
            {
                document.Book = ReadBook(o, true);
                if (document.Book.Slug != slug || (revision != null && document.Book.Revision != revision)) throw new InvalidDataException("Content identity changed");
                if (chapterId == null)
                {
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (object value in Array(o, "chapters", 512)) { Chapter c = ReadChapter(value, false); if (!ids.Add(c.Id)) throw new InvalidDataException("Duplicate chapter"); document.Book.Chapters.Add(c); }
                }
                else { document.Chapter = ReadChapter(Field(o, "chapter"), true); if (document.Chapter.Id != chapterId || document.Book.Status != "ready") throw new InvalidDataException("Chapter identity"); }
            }
            return document;
        }
    }
}
