using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using SCIEPort.Features.CelestialCodex.Core;

internal static class EpubArchiveChecks
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(bool streamed in new[]{false,true})
        foreach(CompressionLevel level in new[]{CompressionLevel.NoCompression,CompressionLevel.Optimal})
        {
            byte[] bytes=Zip("书籍/正文.xhtml","你好，世界😀",level,streamed);
            var archive=new EpubArchive(bytes,CancellationToken.None);
            check(!streamed||(bytes[Central(bytes)+8]&8)!=0,"streamed ZIP fixture uses a data descriptor");
            var entry=archive.Entries[0];
            byte[] text=entry.Read(100,CancellationToken.None);
            check(archive.Entries.Count==1&&entry.FullName=="书籍/正文.xhtml"&&Encoding.UTF8.GetString(text)=="你好，世界😀","EPUB stored/deflated/streamed Unicode ZIP reads");
            check(entry.Read(text.Length,CancellationToken.None).Length==text.Length,"exact entry byte limit accepted");
            Reject(()=>entry.Read(text.Length-1,CancellationToken.None),check,"entry byte limit enforced");
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();
                bool stopped=false;try{entry.Read(100,cancelled.Token);}catch(OperationCanceledException){stopped=true;}
                check(stopped,"entry cancellation preserved");
            }
        }
        byte[] sample=Zip("body.txt","正文",CompressionLevel.Optimal,false);
        int central=Central(sample);
        Reject(()=>new EpubArchive(new byte[21],CancellationToken.None),check,"truncated ZIP rejected");
        for(int length=0;length<sample.Length;length++)
        {
            byte[] truncated=new byte[length];Array.Copy(sample,truncated,length);
            Reject(()=>Read(truncated),check,"truncated member/directory rejected: "+length);
        }
        byte[] bad=(byte[])sample.Clone();Put16(bad,6,1);Put16(bad,central+8,1);
        Reject(()=>Read(bad),check,"encrypted ZIP rejected");
        bad=(byte[])sample.Clone();Put16(bad,8,99);Put16(bad,central+10,99);
        Reject(()=>Read(bad),check,"unsupported compression rejected");
        bad=(byte[])sample.Clone();Put32(bad,central+20,uint.MaxValue);
        Reject(()=>Read(bad),check,"oversized compressed span rejected");
        bad=(byte[])sample.Clone();Put32(bad,central+24,LocalBookLibrary.MaxUnpackedBytes+1);
        Reject(()=>Read(bad),check,"declared expansion bound rejected");
        bad=(byte[])sample.Clone();Put32(bad,central+24,1);Put32(bad,22,1);
        Reject(()=>Read(bad),check,"underreported expansion rejected while reading");
        bad=(byte[])sample.Clone();Put32(bad,central+16,0);Put32(bad,14,0);
        Reject(()=>Read(bad),check,"CRC corruption rejected");
        bad=(byte[])sample.Clone();bad[30]^=1;
        Reject(()=>Read(bad),check,"local/central filename mismatch rejected");
        bad=(byte[])sample.Clone();Put16(bad,bad.Length-18,1);
        Reject(()=>Read(bad),check,"multi-disk ZIP rejected");
        using(var output=new MemoryStream())
        {
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
                for(int i=0;i<257;i++) zip.CreateEntry("entry"+i);
            Reject(()=>Read(output.ToArray()),check,"257 entry ZIP rejected before entry allocation");
        }
        using(var output=new MemoryStream())
        {
            using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
            {
                Add(zip,"META-INF/container.xml","<container xmlns='urn:oasis:names:tc:opendocument:xmlns:container'><rootfiles><rootfile full-path='OPS/book.opf'/></rootfiles></container>");
                Add(zip,"OPS/book.opf","<package xmlns='http://www.idpf.org/2007/opf' xmlns:dc='http://purl.org/dc/elements/1.1/'><metadata><dc:title>命名空间图书</dc:title><dc:creator>作者</dc:creator></metadata><manifest><item id='a' href='%E6%AD%A3%E6%96%87.xhtml#part' media-type='application/xhtml+xml'/></manifest><spine><itemref idref='a'/></spine></package>");
                Add(zip,"OPS/正文.xhtml","<html xmlns='http://www.w3.org/1999/xhtml'><body><h1>章节</h1><p>正文😀</p><style>hidden</style><script>hidden</script></body></html>");
            }
            var files=new MemoryFiles(output.ToArray());Exception reported=null;
            var library=new LocalBookLibrary(files,error=>reported=error);
            var list=library.Read(new SCIEPort.Features.CelestialCodex.Networking.Request(1),CancellationToken.None);
            string id=list.Document.Books[0].Slug;
            var detail=library.Read(new SCIEPort.Features.CelestialCodex.Networking.Request(id),CancellationToken.None);
            check(detail.Document!=null&&detail.Document.Book.Title=="命名空间图书"&&detail.Document.Book.Author=="作者","EPUB namespaces/metadata work without XPath");
            var body=library.Read(new SCIEPort.Features.CelestialCodex.Networking.Request(id,"c1",detail.Document.Book.Revision),CancellationToken.None);
            check(body.Document.Chapter.Text.Contains("正文😀")&&!body.Document.Chapter.Text.Contains("hidden"),"EPUB URI/namespace body extraction and script exclusion");
            files.FailRead=true;
            var failure=library.Read(new SCIEPort.Features.CelestialCodex.Networking.Request(id),CancellationToken.None);
            check(failure.Document==null&&reported is FileNotFoundException,"local read reports full original exception for diagnostics");
            library=new LocalBookLibrary(files,error=>{throw new Exception("logger failed");});
            check(library.Read(new SCIEPort.Features.CelestialCodex.Networking.Request(id),CancellationToken.None).Error!=null,"diagnostic failure does not escape local read");
        }
    }
    static void Add(ZipArchive archive,string name,string text){using(var writer=new StreamWriter(archive.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
    static byte[] Zip(string name,string text,CompressionLevel level,bool streamed)
    {
        using(var output=new MemoryStream())
        {
            Stream target=streamed?(Stream)new SequentialOutput(output):output;
            using(var archive=new ZipArchive(target,ZipArchiveMode.Create,true))
            using(var writer=new StreamWriter(archive.CreateEntry(name,level).Open(),new UTF8Encoding(false))) writer.Write(text);
            return output.ToArray();
        }
    }
    static int Central(byte[] bytes){for(int i=0;i<bytes.Length-4;i++)if(BitConverter.ToUInt32(bytes,i)==0x02014b50)return i;throw new Exception("No fixture directory");}
    static void Put16(byte[] b,int p,int n){b[p]=(byte)n;b[p+1]=(byte)(n>>8);}
    static void Put32(byte[] b,int p,uint n){for(int i=0;i<4;i++)b[p+i]=(byte)(n>>(8*i));}
    static void Read(byte[] bytes){var a=new EpubArchive(bytes,CancellationToken.None);foreach(var e in a.Entries)e.Read(1024,CancellationToken.None);}
    static void Reject(Action action,Action<bool,string> check,string name){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}check(rejected,name);}
    sealed class SequentialOutput:Stream
    {
        readonly Stream output;internal SequentialOutput(Stream output){this.output=output;}
        public override bool CanRead=>false;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>output.Length;public override long Position{get=>output.Position;set=>throw new NotSupportedException();}
        public override void Flush()=>output.Flush();public override void Write(byte[] b,int o,int n)=>output.Write(b,o,n);
        public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    sealed class MemoryFiles:IFileStore
    {
        readonly byte[] bytes;internal bool FailRead;internal MemoryFiles(byte[] bytes){this.bytes=bytes;}
        public void Create(){}public System.Collections.Generic.IEnumerable<string> Names(){yield return "测试.epub";}
        public long Size(string name)=>bytes.Length;public DateTime Modified(string name)=>DateTime.UtcNow;
        public Stream Read(string name){if(FailRead)throw new FileNotFoundException("Missing fixture","测试.epub");return new MemoryStream(bytes,false);}
        public Stream Write(string name)=>throw new NotSupportedException();public void Move(string a,string b)=>throw new NotSupportedException();public void Delete(string name)=>throw new NotSupportedException();
    }
}
