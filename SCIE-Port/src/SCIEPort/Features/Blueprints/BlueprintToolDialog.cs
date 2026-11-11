using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.Blueprints.Core;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Infrastructure;
using SCIEPort.Features.Blueprints.Networking;
using Engine;
using Game;

namespace SCIEPort.Features.Blueprints
{
    internal sealed class BlueprintToolDialog : Dialog
    {
        private readonly WorldEditRuntime runtime;
        private readonly ComponentPlayer player;
        private readonly Action<int,bool,bool> begin;
        private readonly Action close;
        private readonly CanvasWidget panel, view, body;
        private readonly ScrollPanelWidget scroll;
        private readonly LabelWidget current, message;
        private readonly TextBoxWidget name;
        private readonly BevelledButtonWidget import, save, start, rotate, clearButton, refresh, cancel, load, delete, copy, undo, stop, mirror;
        private readonly BevelledButtonWidget localTab, cloudTab, previous, next;
        private readonly BlueprintCloudClient cloudClient;
        private readonly List<CloudBlueprintEntry> cloudEntries = new List<CloudBlueprintEntry>();
        private CloudBlueprintPage cloudPage;
        private CloudBlueprintEntry cloudChosen;
        private int page = 1;
        private bool cloud, closed;
        private CancellationTokenSource network;
        private Task<CloudResult<CloudBlueprintPage>> listTask;
        private Task<CloudResult<CloudBlueprintDownload>> downloadTask;
        private readonly List<BevelledButtonWidget> entries = new List<BevelledButtonWidget>();
        private readonly List<string> stems = new List<string>();
        private string chosen;
        private Dialog deletion;
        private int direction;
        private bool clear, mirrored;
        internal BlueprintToolDialog(WorldEditRuntime runtime,ComponentPlayer player, Action<int,bool,bool> begin, Action close)
        {
            this.runtime=runtime; this.player=player; this.begin=begin; this.close=close;
            cloudClient=BlueprintStorage.CloudClient();
            panel=new CanvasWidget { Size=new Vector2(460,500), HorizontalAlignment=WidgetAlignment.Center, VerticalAlignment=WidgetAlignment.Center }; Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor=new Color(20,28,37,245), OutlineColor=Color.White, OutlineThickness=2 });
            Add(panel,new LabelWidget { Text="天工绘卷", FontScale=.8f, Color=Color.White },12,10);
            view=new CanvasWidget { Size=new Vector2(436,405) }; Add(panel,view,12,45);
            scroll=new ScrollPanelWidget { Direction=LayoutDirection.Vertical, HorizontalAlignment=WidgetAlignment.Stretch, VerticalAlignment=WidgetAlignment.Stretch }; view.Children.Add(scroll);
            body=new CanvasWidget { Size=new Vector2(420,900), HorizontalAlignment=WidgetAlignment.Near, VerticalAlignment=WidgetAlignment.Near }; scroll.Children.Add(body);
            current=new LabelWidget { Size=new Vector2(405,100), WordWrap=true, FontScale=.68f, Color=Color.White }; Add(body,current,0,0);
            import=Button("导入",0,105,400); name=new TextBoxWidget { Size=new Vector2(390,44), Title="保存蓝图名称", Description="输入名称", MaximumLength=80, FontScale=.7f }; Add(body,new RectangleWidget { Size=new Vector2(400,46),FillColor=new Color(48,55,65),OutlineColor=Color.White,OutlineThickness=1 },0,158); Add(body,name,5,159);
            save=Button("保存",0,210,400); rotate=Button("朝向：0°",0,263,195); clearButton=Button("清障：关闭",205,263,195);
            copy=Button("复制",0,320,126); undo=Button("撤销",136,320,126); stop=Button("取消施工",272,320,128);
            mirror=Button("镜像：关闭",0,374,400);
            start=Button("预览",0,428,400);
            localTab=Button("本地",0,482,195); cloudTab=Button("云端",205,482,195);
            refresh=Button("刷新",0,536,195); load=Button("载入",205,536,195);
            delete=Button("删除",0,590,400);
            previous=Button("上一页",0,644,195); next=Button("下一页",205,644,195);
            message=new LabelWidget { Text="选择蓝图。",Size=new Vector2(400,85),WordWrap=true,FontScale=.64f,Color=Color.White }; Add(body,message,0,698);
            cancel=new BevelledButtonWidget { Text="关闭",Size=new Vector2(160,48),FontScale=.7f }; Add(panel,cancel,288,452);
            UpdateCurrent(); Refresh();
        }
        private static void Add(CanvasWidget c,Widget w,float x,float y) { c.Children.Add(w);c.SetWidgetPosition(w,new Vector2(x,y)); }
        private BevelledButtonWidget Button(string text,float x,float y,float w) { var b=new BevelledButtonWidget { Text=text, Size=new Vector2(w,48),FontScale=.64f }; Add(body,b,x,y);return b; }
        private void UpdateCurrent()
        {
            Blueprint bp=runtime.ClipboardBlueprint;
            string error=runtime.QuickValidate(bp);
            current.Text=bp==null ? "当前蓝图：无" : bp.Name+"\n"+bp.SizeX+" × "+bp.SizeY+" × "+bp.SizeZ+" · "+bp.CellCount+" 格"+(bp.FurniturePlacements.Count>0?" · "+bp.FurniturePlacements.Count+" 件家具":"")+(error==null?"":"\n"+error);
            if(bp!=null) name.Text=bp.Name ?? "";
        }
        private void Refresh()
        {
            ClearEntries(); chosen=null;
            localTab.IsEnabled=false;cloudTab.IsEnabled=true;delete.IsVisible=true;previous.IsVisible=false;next.IsVisible=false;load.Text="载入";
            string error; var files=BlueprintStorage.ListNames(out error);
            if(error!=null) { message.Text=error; return; }
            float y=790;
            foreach(string stem in files)
            {
                string err; Blueprint bp=BlueprintStorage.LoadBlueprint(stem,out err);
                if(bp!=null) err=runtime.QuickValidate(bp);
                string label=bp==null ? stem+"："+err : bp.Name+"  "+bp.SizeX+"×"+bp.SizeY+"×"+bp.SizeZ+"  "+bp.CellCount+"格"+(err==null?"":"（不支持）");
                var b=Button(label,0,y,400); entries.Add(b); stems.Add(stem); y+=50;
            }
            body.Size=new Vector2(420,MathUtils.Max(790,y+10));
            message.Text=files.Count==0?"蓝图库为空。":files.Count+" 份蓝图";
        }
        private void ClearEntries()
        { foreach(var b in entries) body.Children.Remove(b);entries.Clear();stems.Clear();cloudEntries.Clear();chosen=null;cloudChosen=null; }
        private bool NetworkBusy { get { return listTask!=null || downloadTask!=null; } }
        private void StopNetwork()
        { if(network!=null){network.Cancel();network.Dispose();network=null;}listTask=null;downloadTask=null; }
        private void CloudList(bool offlineOnly)
        {
            StopNetwork(); ClearEntries(); cloudPage=null;
            localTab.IsEnabled=true;cloudTab.IsEnabled=false;delete.IsVisible=false;previous.IsVisible=true;next.IsVisible=true;previous.IsEnabled=page>1;next.IsEnabled=false;load.Text="下载并载入";
            message.Text=offlineOnly?"读取缓存…":"刷新中…";
            network=new CancellationTokenSource();CancellationToken token=network.Token;int requestedPage=page;
            listTask=Task.Run(()=>cloudClient.List(requestedPage,token,offlineOnly));
        }
        private void PaintCloud(CloudBlueprintPage data)
        {
            ClearEntries();cloudPage=data;float y=790;
            foreach(CloudBlueprintEntry entry in data.Items)
            { string label=entry.Name.Length>22?entry.Name.Substring(0,22)+"…":entry.Name;var button=Button(label+"\n"+entry.SizeX+"×"+entry.SizeY+"×"+entry.SizeZ+" · "+entry.BlockCount+"格 · r"+entry.Revision,0,y,400);button.Size=new Vector2(400,66);entries.Add(button);cloudEntries.Add(entry);y+=74; }
            body.Size=new Vector2(420,MathUtils.Max(790,y+10));
            previous.IsEnabled=page>1;next.IsEnabled=page*20<data.Total;
        }
        // All task results are consumed here, inside Dialog.Update on the game's main thread.
        private void PollNetwork()
        {
            if(listTask!=null && listTask.IsCompleted)
            {
                var task=listTask;listTask=null;
                if(task.IsCanceled){message.Text="已取消。";return;}
                if(task.IsFaulted){var ignored=task.Exception;message.Text="蓝图库请求失败。";return;}
                var result=task.Result;
                if(result.Value==null){message.Text=result.Error;return;}
                PaintCloud(result.Value);message.Text=(result.Offline?"离线缓存 · ":"")+"第 "+page+" 页 · "+result.Value.Total+" 份"+(result.CacheFailed?"；缓存保存失败":"");
            }
            if(downloadTask!=null && downloadTask.IsCompleted)
            {
                var task=downloadTask;downloadTask=null;
                if(task.IsCanceled){message.Text="已取消。";return;}
                if(task.IsFaulted){var ignored=task.Exception;message.Text="下载失败。";return;}
                var result=task.Result;
                if(result.Value==null){message.Text=result.Error;return;}
                var download=result.Value;
                string error=runtime.QuickValidate(download.Blueprint);
                if(error!=null){message.Text="载入失败："+error;return;}
                // Adoption retains the existing material preflight and never invokes terrain writes.
                error=runtime.QuickAdopt(download.Blueprint,"云端 r"+download.Entry.Revision+(result.Offline?"（离线）":""));
                if(error!=null){message.Text=error;return;}
                string filename;string saved=BlueprintStorage.SaveCloud(download,out filename);
                UpdateCurrent();message.Text=(result.Offline?"已从离线缓存载入。":"已下载并载入。")+(saved==null?"已保存到本地。":"本地保存失败："+saved)+(result.CacheFailed?"云端缓存未保存。":"");
            }
        }
        public override void Update()
        {
            if(closed)return;
            PollNetwork();
            if(deletion!=null)return;
            Vector2 size=player.GameWidget.GuiWidget.ActualSize; float scale=MathUtils.Min(1f,(size.X-16)/460f);
            if(scale>0) { float h=MathUtils.Min(500f,(size.Y-16)/scale);panel.Size=new Vector2(460,h);view.Size=new Vector2(436,MathUtils.Max(40,h-95));panel.SetWidgetPosition(cancel,new Vector2(288,h-48));panel.LayoutTransform=Matrix.CreateScale(scale); }
            if(copy.IsClicked) { CloseChildren();close();runtime.BeginCopy();return; }
            if(undo.IsClicked) { runtime.Undo(); }
            if(stop.IsClicked) { CloseChildren();close();runtime.Cancel();return; }
            if(mirror.IsClicked) { mirrored=!mirrored;mirror.Text="镜像："+(mirrored?"开启":"关闭"); }
            start.IsEnabled=!runtime.IsBusy;copy.IsEnabled=!runtime.IsBusy;undo.IsEnabled=!runtime.IsBusy;
            if(import.IsClicked) { StopNetwork();string raw=GameInterop.TryReadClipboardText(); string e=runtime.QuickImport(raw,"系统剪贴板"); message.Text=e==null?"已导入。":"导入失败："+e; if(e==null)UpdateCurrent(); }
            if(save.IsClicked) { string filename; string e=runtime.QuickSave(name.Text,out filename); if(e==null && !cloud) { Refresh(); chosen=System.IO.Path.GetFileNameWithoutExtension(filename); } message.Text=e==null?"已保存："+filename:"保存失败："+e; }
            if(rotate.IsClicked) { direction=(direction+1)%4;rotate.Text="朝向："+(direction*90)+"°"; }
            if(clearButton.IsClicked) { clear=!clear;clearButton.Text="清障："+(clear?"开启":"关闭"); }
            refresh.IsEnabled=!NetworkBusy;load.IsEnabled=!NetworkBusy;
            if(localTab.IsClicked){StopNetwork();cloud=false;Refresh();}
            if(cloudTab.IsClicked){cloud=true;page=1;CloudList(true);}
            if(refresh.IsClicked){if(cloud)CloudList(false);else Refresh();}
            if(cloud && !NetworkBusy && previous.IsClicked && page>1){page--;CloudList(false);}
            if(cloud && !NetworkBusy && next.IsClicked && cloudPage!=null && page*20<cloudPage.Total){page++;CloudList(false);}
            for(int i=0;i<entries.Count;i++) if(entries[i].IsClicked)
            { if(cloud){cloudChosen=cloudEntries[i];message.Text=cloudChosen.Name+" · r"+cloudChosen.Revision;}else{chosen=stems[i];message.Text=chosen+".json";} }
            if(!cloud && delete.IsClicked)
            {
                if(chosen==null) message.Text="请先选择要删除的文件。";
                else
                {
                    string stem=chosen;
                    deletion=new MessageDialog("删除本地蓝图？",stem+".json\n删除后无法恢复；当前已载入内存的蓝图和世界建筑不受影响。","删除文件","取消", result =>
                    {
                        deletion=null;
                        if(result==MessageDialogButton.Button1)
                        {
                            string error=BlueprintStorage.Delete(stem);
                            Refresh(); message.Text=error??"文件已删除，当前内存蓝图仍可使用或重新保存。";
                        }
                    });
                    DialogsManager.ShowDialog(player.GameWidget.GuiWidget,deletion);
                }
            }
            if(load.IsClicked && !NetworkBusy)
            {
                if(cloud)
                {
                    if(cloudChosen==null)message.Text="请先选择蓝图。";
                    else
                    {
                        StopNetwork();network=new CancellationTokenSource();CancellationToken token=network.Token;CloudBlueprintEntry entry=cloudChosen;
                        message.Text="下载中…";downloadTask=Task.Run(()=>cloudClient.Download(entry,token));
                    }
                }
                else { string e=chosen==null?"请先选择文件。":runtime.QuickLoad(chosen); message.Text=e==null?"已载入。":"载入失败："+e; if(e==null)UpdateCurrent(); }
            }
            if(start.IsClicked) { string e=runtime.QuickValidate(runtime.ClipboardBlueprint); if(e!=null)message.Text=e; else begin(direction,clear,mirrored); }
            if(cancel.IsClicked||Input.Back||Input.Cancel) { Input.Clear();CloseChildren();close(); }
        }
        internal void CloseChildren() { closed=true;StopNetwork();if(deletion!=null)DialogsManager.HideDialog(deletion);deletion=null; }
    }
}
