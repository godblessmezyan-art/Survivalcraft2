using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace SCIEPort.Features.CelestialCodex.Core
{
    // Read-only EPUB ZIP subset (Store/Deflate). DeflateStream lives in the retained
    // Android System.dll; System.IO.Compression.dll/ZipArchive are absent in x25.
    // No archive member is extracted to disk. Format: PKWARE APPNOTE 6.3.10, 4.3.
    internal sealed class EpubArchive
    {
        internal readonly List<Entry> Entries = new List<Entry>();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly uint[] CrcTable = MakeCrcTable();

        internal EpubArchive(byte[] bytes, CancellationToken cancellation)
        {
            if(bytes==null || bytes.Length<22 || bytes.Length>LocalBookLibrary.MaxSourceBytes)
                throw new InvalidDataException("EPUB ZIP size");
            int end=-1;
            for(int i=bytes.Length-22;i>=Math.Max(0,bytes.Length-65557);i--)
            {
                cancellation.ThrowIfCancellationRequested();
                if(U32(bytes,i)==0x06054b50 && i+22+U16(bytes,i+20)==bytes.Length) {end=i;break;}
            }
            if(end<0) throw new InvalidDataException("EPUB ZIP directory missing");
            int count=U16(bytes,end+10);
            if(U16(bytes,end+4)!=0 || U16(bytes,end+6)!=0 || U16(bytes,end+8)!=count || count>LocalBookLibrary.MaxEntries)
                throw new InvalidDataException("EPUB ZIP disks or entry count");
            uint size=U32(bytes,end+12),offset=U32(bytes,end+16);
            if(offset>(uint)end || size>(uint)end-offset) throw new InvalidDataException("EPUB ZIP directory bounds");
            int cursor=(int)offset,stop=cursor+(int)size;
            long unpacked=0;
            for(int i=0;i<count;i++)
            {
                cancellation.ThrowIfCancellationRequested();
                Range(cursor,46,stop);
                if(U32(bytes,cursor)!=0x02014b50) throw new InvalidDataException("EPUB ZIP directory record");
                int flags=U16(bytes,cursor+8),method=U16(bytes,cursor+10),nameSize=U16(bytes,cursor+28);
                int recordSize=46+nameSize+U16(bytes,cursor+30)+U16(bytes,cursor+32);
                Range(cursor,recordSize,stop);
                uint crc=U32(bytes,cursor+16),compressed=U32(bytes,cursor+20),length=U32(bytes,cursor+24),header=U32(bytes,cursor+42);
                if(nameSize==0 || (flags&0x2041)!=0 || (method!=0 && method!=8) || U16(bytes,cursor+34)!=0)
                    throw new InvalidDataException("Unsupported EPUB ZIP member");
                unpacked+=length;
                if(unpacked>LocalBookLibrary.MaxUnpackedBytes || header>offset || compressed>offset)
                    throw new InvalidDataException("EPUB ZIP member limit");
                int local=(int)header;
                Range(local,30,(int)offset);
                if(U32(bytes,local)!=0x04034b50 || U16(bytes,local+6)!=flags || U16(bytes,local+8)!=method || U16(bytes,local+26)!=nameSize)
                    throw new InvalidDataException("EPUB ZIP local header mismatch");
                int data=local+30+nameSize+U16(bytes,local+28);
                Range(local,data-local,(int)offset);Range(data,(int)compressed,(int)offset);
                for(int n=0;n<nameSize;n++)
                    if(bytes[local+30+n]!=bytes[cursor+46+n]) throw new InvalidDataException("EPUB ZIP filename mismatch");
                if((flags&8)==0 && (U32(bytes,local+14)!=crc || U32(bytes,local+18)!=compressed || U32(bytes,local+22)!=length))
                    throw new InvalidDataException("EPUB ZIP local sizes mismatch");
                if(method==0 && compressed!=length) throw new InvalidDataException("EPUB stored size mismatch");
                string name=Utf8.GetString(bytes,cursor+46,nameSize);
                Entries.Add(new Entry(bytes,name,length,compressed,data,method,crc));
                cursor+=recordSize;
            }
            if(cursor!=stop) throw new InvalidDataException("EPUB ZIP directory size mismatch");
        }

        private static void Range(int offset,int count,int limit)
        {if(offset<0 || count<0 || offset>limit || count>limit-offset) throw new InvalidDataException("EPUB ZIP bounds");}
        private static int U16(byte[] data,int offset) => data[offset]|data[offset+1]<<8;
        private static uint U32(byte[] data,int offset) => (uint)(data[offset]|data[offset+1]<<8|data[offset+2]<<16|data[offset+3]<<24);
        private static uint[] MakeCrcTable()
        {
            var table=new uint[256];
            for(uint i=0;i<256;i++) {uint crc=i;for(int bit=0;bit<8;bit++) crc=(crc&1)==0?crc>>1:(crc>>1)^0xedb88320;table[i]=crc;}
            return table;
        }

        internal sealed class Entry
        {
            internal readonly string FullName;
            internal readonly long Length;
            private readonly byte[] archive;
            private readonly int compressed,offset,method;
            private readonly uint expectedCrc;
            internal Entry(byte[] archive,string name,uint length,uint compressed,int offset,int method,uint crc)
            {this.archive=archive;FullName=name;Length=length;this.compressed=(int)compressed;this.offset=offset;this.method=method;expectedCrc=crc;}

            internal byte[] Read(int limit,CancellationToken cancellation)
            {
                cancellation.ThrowIfCancellationRequested();
                if(Length>limit) throw new InvalidDataException("EPUB entry expansion limit");
                using(var input=new MemoryStream(archive,offset,compressed,false))
                {
                    if(method==0) return ReadChecked(input,limit,cancellation);
                    using(var inflater=new DeflateStream(input,CompressionMode.Decompress))
                        return ReadChecked(inflater,limit,cancellation);
                }
            }
            private byte[] ReadChecked(Stream input,int limit,CancellationToken cancellation)
            {
                using(var output=new MemoryStream())
                {
                    byte[] buffer=new byte[8192];uint crc=0xffffffff;int count;
                    while((count=input.Read(buffer,0,buffer.Length))>0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(count>limit-output.Length || count>Length-output.Length) throw new InvalidDataException("EPUB actual expansion limit");
                        for(int i=0;i<count;i++) crc=(crc>>8)^CrcTable[(crc^buffer[i])&255];
                        output.Write(buffer,0,count);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if(output.Length!=Length || ~crc!=expectedCrc) throw new InvalidDataException("EPUB member size or CRC mismatch");
                    return output.ToArray();
                }
            }
        }
    }
}
