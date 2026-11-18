using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using Engine;

internal static class LocalImportChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    internal static void Run(Assembly mod,Action<bool,string> check)
    {
        foreach(var reference in mod.GetReferencedAssemblies())
            check(reference.Name!="System.IO.Compression","runtime has no missing System.IO.Compression assembly dependency");
        Type sessionType=mod.GetType("SCIEPort.Features.CelestialCodex.Mod.ReaderSession",true);
        Type requestType=mod.GetType("SCIEPort.Features.CelestialCodex.Networking.Request",true);
        Type native=mod.GetType("SCIEPort.Features.CelestialCodex.Mod.AndroidBookPicker",true);
        var owned=(HashSet<string>)native.GetField("owned",F).GetValue(null);
        string root="data:/SCIEPort-Codex-Test-"+Guid.NewGuid().ToString("N");
        string physical=Storage.GetSystemPath(root),imports=Storage.CombinePaths(root,"Imports");
        string selectedRoot=Path.Combine(Path.GetTempPath(),"codex-selection-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(selectedRoot);string selected=Path.Combine(selectedRoot,"中文书.epub");
        object session=null;
        try
        {
            using(var file=File.Create(selected))
            using(var zip=new ZipArchive(file,ZipArchiveMode.Create,false))
            {
                Add(zip,"META-INF/container.xml","<container><rootfiles><rootfile full-path='OPS/book.opf'/></rootfiles></container>");
                Add(zip,"OPS/book.opf","<package xmlns='http://www.idpf.org/2007/opf' xmlns:dc='http://purl.org/dc/elements/1.1/'><metadata><dc:title>荒野之书</dc:title></metadata><manifest><item id='c' href='1.xhtml' media-type='application/xhtml+xml'/></manifest><spine><itemref idref='c'/></spine></package>");
                Add(zip,"OPS/1.xhtml","<html xmlns='http://www.w3.org/1999/xhtml'><body><p>山野正文😀</p></body></html>");
            }
            byte[] original=File.ReadAllBytes(selected);
            session=Activator.CreateInstance(sessionType,F,null,new object[]{root},null);
            lock(owned)owned.Add(selected);
            string name=(string)sessionType.GetMethod("ImportFile",F).Invoke(session,new object[]{selected,CancellationToken.None});
            check(!File.Exists(selected)&&!Directory.Exists(selectedRoot),"system staging source removed after import");
            check(Storage.FileExists(Storage.CombinePaths(imports,name)),"committed EPUB exists in Imports after staging cleanup");
            using(Stream saved=Storage.OpenFile(Storage.CombinePaths(imports,name),OpenFileMode.Read))
            using(var memory=new MemoryStream())
            {saved.CopyTo(memory);check(Convert.ToBase64String(original)==Convert.ToBase64String(memory.ToArray()),"imported EPUB bytes preserved through native Storage commit");}

            string slug=null,revision=null;
            for(int reopen=0;reopen<2;reopen++)
            {
                sessionType.GetMethod("SetLocal",F).Invoke(session,new object[]{true});
                object list=Read(session,New(requestType,new object[]{1}));
                var books=(IList)Field(Field(list,"Document"),"Books");
                check(books.Count==1,"local book persists after source deletion/session reopen");
                string current=(string)Field(books[0],"Slug");
                check(slug==null||slug==current,"local book slug remains stable across sessions");slug=current;
                object detail=Read(session,New(requestType,new object[]{slug,null,null}));
                object book=Field(Field(detail,"Document"),"Book");
                check((string)Field(book,"Title")=="荒野之书"&&((IList)Field(book,"Chapters")).Count==1,"imported EPUB metadata and chapter list read from committed file");
                string currentRevision=(string)Field(book,"Revision");
                check(revision==null||revision==currentRevision,"book revision stable across reopened sessions");revision=currentRevision;
                object chapter=Field(Field(Read(session,New(requestType,new object[]{slug,"c1",revision})),"Document"),"Chapter");
                check(((string)Field(chapter,"Text")).Contains("山野正文😀"),"actual compiled reader renders EPUB body after source staging cleanup");
                ((IDisposable)session).Dispose();session=null;
                if(reopen==0)session=Activator.CreateInstance(sessionType,F,null,new object[]{root},null);
            }
        }
        finally
        {
            if(session!=null)((IDisposable)session).Dispose();
            lock(owned)owned.Remove(selected);
            if(File.Exists(selected))File.Delete(selected);
            if(Directory.Exists(selectedRoot))Directory.Delete(selectedRoot);
            // Only this fixture's GUID directory, no recursive deletion or player world paths.
            string dir=Storage.GetSystemPath(imports);
            if(Directory.Exists(dir)){foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
            if(Directory.Exists(physical))Directory.Delete(physical);
        }
    }
    static object Field(object value,string name)=>value.GetType().GetField(name,F).GetValue(value);
    static object New(Type type,object[] args)=>Activator.CreateInstance(type,F,null,args,null);
    static object Read(object session,object request)
    {
        Type type=session.GetType();type.GetMethod("Begin",F).Invoke(session,new[]{request});
        var take=type.GetMethod("Take",F);
        for(int i=0;i<500;i++)
        {
            object result=take.Invoke(session,null);
            if(result!=null)
            {if(Field(result,"Document")==null)throw new Exception("Local import fixture failed: "+Field(result,"Error"));return result;}
            Thread.Sleep(10);
        }
        throw new TimeoutException("Local import fixture request timed out");
    }
    static void Add(ZipArchive zip,string name,string text)
    {using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
}
