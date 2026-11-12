using System;
using System.IO;
using System.Threading;
using SCIEPort.Features.Blueprints.Networking;
using SCIEPort.Features.Blueprints.Core.Blueprints;

internal static partial class BlueprintTests
{
    internal static void Live()
    {
        string root=Path.Combine(Path.GetTempPath(),"tiangong-live-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var client=new BlueprintCloudClient(new BlueprintCloudCache(root),null,Console.WriteLine);
            var page=client.List(1,CancellationToken.None).GetAwaiter().GetResult();
            Check(page.Value!=null&&!page.Offline,"final DLL reads live HTTPS catalog: "+page.Error);
            Console.WriteLine("Live Tiangong catalog: total="+page.Value.Total+"; no cached fallback.");
            if(page.Value.Items.Count>0)
            {
                var entry=page.Value.Items[0];
                var download=client.Download(entry,CancellationToken.None).GetAwaiter().GetResult();
                Check(download.Value!=null&&!download.Offline,"live download checks SHA/revision/schema: "+download.Error);
                var library=new BlueprintFileLibrary(Path.Combine(root,"library"));string filename;
                Check(library.SaveExact("cloud-"+entry.Id+"-r"+entry.Revision,download.Value.Json,out filename)==null,"live validated blueprint saved");
                var offline=client.Download(entry,CancellationToken.None,true).GetAwaiter().GetResult();
                Check(offline.Value!=null&&offline.Offline,"live download survives offline cache reload");
                Console.WriteLine("Live Tiangong download: bytes="+entry.ByteLength+"; revision="+entry.Revision+"; saved/offline verified.");
            }
        }
        finally { if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true); }
    }
}
