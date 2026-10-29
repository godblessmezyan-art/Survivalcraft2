using System;
using System.Collections.Generic;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Infrastructure;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    internal sealed class BlueprintToolDialog : Dialog
    {
        private readonly ComponentPlayer player;
        private readonly Action<int,bool> begin;
        private readonly Action close;
        private readonly CanvasWidget panel, view, body;
        private readonly ScrollPanelWidget scroll;
        private readonly LabelWidget current, message;
        private readonly TextBoxWidget name;
        private readonly BevelledButtonWidget import, save, start, rotate, clearButton, refresh, cancel, load, delete;
        private readonly List<BevelledButtonWidget> entries = new List<BevelledButtonWidget>();
        private readonly List<string> stems = new List<string>();
        private string chosen;
        private Dialog deletion;
        private int direction;
        private bool clear = true;
        internal BlueprintToolDialog(ComponentPlayer player, Action<int,bool> begin, Action close)
        {
            this.player=player; this.begin=begin; this.close=close;
            panel=new CanvasWidget { Size=new Vector2(460,500), HorizontalAlignment=WidgetAlignment.Center, VerticalAlignment=WidgetAlignment.Center }; Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor=new Color(20,28,37,245), OutlineColor=Color.White, OutlineThickness=2 });
            Add(panel,new LabelWidget { Text="蓝图管理与快捷建造", FontScale=.8f, Color=Color.White },12,10);
            view=new CanvasWidget { Size=new Vector2(436,405) }; Add(panel,view,12,45);
            scroll=new ScrollPanelWidget { Direction=LayoutDirection.Vertical, HorizontalAlignment=WidgetAlignment.Stretch, VerticalAlignment=WidgetAlignment.Stretch }; view.Children.Add(scroll);
            body=new CanvasWidget { Size=new Vector2(420,900), HorizontalAlignment=WidgetAlignment.Near, VerticalAlignment=WidgetAlignment.Near }; scroll.Children.Add(body);
            current=new LabelWidget { Size=new Vector2(405,100), WordWrap=true, FontScale=.68f, Color=Color.White }; Add(body,current,0,0);
            import=Button("从系统剪贴板导入",0,105,400); name=new TextBoxWidget { Size=new Vector2(390,44), Title="保存蓝图名称", Description="输入名称", MaximumLength=80, FontScale=.7f }; Add(body,new RectangleWidget { Size=new Vector2(400,46),FillColor=new Color(48,55,65),OutlineColor=Color.White,OutlineThickness=1 },0,158); Add(body,name,5,159);
            save=Button("保存当前蓝图",0,210,400); rotate=Button("朝向：0°",0,263,195); clearButton=Button("清障：开启",205,263,195);
            Add(body,new LabelWidget { Text="仅覆盖蓝图实际需要写入的方块位置，不清理整个矩形区域，也不向外扩展。不检查周围方块类型。",Size=new Vector2(400,76),WordWrap=true,FontScale=.63f,Color=Color.White },0,315);
            start=Button("选择落点并预览",0,395,400); refresh=Button("刷新蓝图库",0,448,195); load=Button("载入所选蓝图",205,448,195);
            delete=Button("删除所选文件",0,501,400);
            message=new LabelWidget { Text="选择下方文件后载入。",Size=new Vector2(400,65),WordWrap=true,FontScale=.64f,Color=Color.White }; Add(body,message,0,555);
            cancel=new BevelledButtonWidget { Text="关闭",Size=new Vector2(160,42),FontScale=.7f }; Add(panel,cancel,288,452);
            UpdateCurrent(); Refresh();
        }
        private static void Add(CanvasWidget c,Widget w,float x,float y) { c.Children.Add(w);c.SetWidgetPosition(w,new Vector2(x,y)); }
        private BevelledButtonWidget Button(string text,float x,float y,float w) { var b=new BevelledButtonWidget { Text=text, Size=new Vector2(w,46),FontScale=.64f }; Add(body,b,x,y);return b; }
        private void UpdateCurrent()
        {
            Blueprint bp=WorldEditRuntime.ClipboardBlueprint;
            string error=WorldEditRuntime.QuickValidate(bp);
            current.Text=bp==null ? "当前蓝图：无" : "当前："+bp.Name+"\n来源："+(WorldEditRuntime.QuickBlueprintSource ?? "游戏内复制或旧缓存")+"\n尺寸："+bp.SizeX+" × "+bp.SizeY+" × "+bp.SizeZ+"；方块："+bp.CellCount+"；家具："+bp.FurniturePlacements.Count+"\n"+(error==null ? "格式及材料校验通过" : error);
            if(bp!=null) name.Text=bp.Name ?? "";
        }
        private void Refresh()
        {
            foreach(var b in entries) body.Children.Remove(b); entries.Clear(); stems.Clear(); chosen=null;
            string error; var files=BlueprintStorage.ListNames(out error);
            if(error!=null) { message.Text=error; return; }
            float y=625;
            foreach(string stem in files)
            {
                string err; Blueprint bp=BlueprintStorage.LoadBlueprint(stem,out err);
                if(bp!=null) err=WorldEditRuntime.QuickValidate(bp);
                string label=bp==null ? stem+"："+err : bp.Name+"  "+bp.SizeX+"×"+bp.SizeY+"×"+bp.SizeZ+"  "+bp.CellCount+"格"+(err==null?"":"（不支持）");
                var b=Button(label,0,y,400); entries.Add(b); stems.Add(stem); y+=50;
            }
            body.Size=new Vector2(420,MathUtils.Max(630,y+10));
            message.Text=files.Count==0?"蓝图库为空。":"选择下方文件后点击载入（"+files.Count+" 个文件）。";
        }
        public override void Update()
        {
            if(deletion!=null)return;
            Vector2 size=player.GameWidget.GuiWidget.ActualSize; float scale=MathUtils.Min(1f,(size.X-16)/460f);
            if(scale>0) { float h=MathUtils.Min(500f,(size.Y-16)/scale);panel.Size=new Vector2(460,h);view.Size=new Vector2(436,MathUtils.Max(40,h-95));panel.SetWidgetPosition(cancel,new Vector2(288,h-48));panel.LayoutTransform=Matrix.CreateScale(scale); }
            if(import.IsClicked) { string raw=GameInterop.TryReadClipboardText(); string e=WorldEditRuntime.QuickImport(raw,"系统剪贴板"); message.Text=e==null?"已导入内存；点击保存才写入蓝图库。":"导入失败："+e; if(e==null)UpdateCurrent(); }
            if(save.IsClicked) { string filename; string e=WorldEditRuntime.QuickSave(name.Text,out filename); if(e==null) { Refresh(); chosen=System.IO.Path.GetFileNameWithoutExtension(filename); } message.Text=e==null?"已保存："+filename:"保存失败："+e; }
            if(rotate.IsClicked) { direction=(direction+1)%4;rotate.Text="朝向："+(direction*90)+"°"; }
            if(clearButton.IsClicked) { clear=!clear;clearButton.Text="清障："+(clear?"开启":"关闭"); }
            if(refresh.IsClicked) Refresh();
            for(int i=0;i<entries.Count;i++) if(entries[i].IsClicked) { chosen=stems[i]; message.Text="已选择文件："+chosen+".json"; }
            if(delete.IsClicked)
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
            if(load.IsClicked) { string e=chosen==null?"请先选择文件。":WorldEditRuntime.QuickLoad(chosen); message.Text=e==null?"已载入当前蓝图。":"载入失败："+e; if(e==null)UpdateCurrent(); }
            if(start.IsClicked) { string e=WorldEditRuntime.QuickValidate(WorldEditRuntime.ClipboardBlueprint); if(e!=null)message.Text=e; else begin(direction,clear); }
            if(cancel.IsClicked||Input.Back||Input.Cancel) { Input.Clear();if(deletion!=null)DialogsManager.HideDialog(deletion);close(); }
        }
        internal void CloseChildren() { if(deletion!=null)DialogsManager.HideDialog(deletion);deletion=null; }
    }
    internal sealed class BlueprintClearConfirmDialog : Dialog
    {
        private readonly ComponentPlayer player; private readonly CanvasWidget panel;
        private readonly BevelledButtonWidget yes,no; private readonly Action confirm,cancel;
        internal BlueprintClearConfirmDialog(ComponentPlayer player,Blueprint bp,CellPos pos,int count,Action confirm,Action cancel)
        {
            this.player=player;this.confirm=confirm;this.cancel=cancel;
            panel=new CanvasWidget { Size=new Vector2(460,300),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center };Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor=new Color(20,28,37,245),OutlineColor=Color.White,OutlineThickness=2 });
            var label=new LabelWidget { Size=new Vector2(420,190),WordWrap=true,FontScale=.68f,Color=Color.White,Text="清障并建造："+bp.Name+"\n位置："+pos+"\n尺寸："+bp.SizeX+"×"+bp.SizeY+"×"+bp.SizeZ+"\n将覆盖 "+count+" 个方块。仅写入蓝图所需位置；撤销不保证恢复物品、电路和流体连锁等副作用。" }; panel.Children.Add(label);panel.SetWidgetPosition(label,new Vector2(20,20));
            yes=new BevelledButtonWidget { Text="确认清障并建造",Size=new Vector2(200,48),FontScale=.65f };panel.Children.Add(yes);panel.SetWidgetPosition(yes,new Vector2(20,235));
            no=new BevelledButtonWidget { Text="取消",Size=new Vector2(200,48),FontScale=.7f };panel.Children.Add(no);panel.SetWidgetPosition(no,new Vector2(240,235));
        }
        public override void Update() { Vector2 s=player.GameWidget.GuiWidget.ActualSize;float f=MathUtils.Min(1f,MathUtils.Min((s.X-16)/460,(s.Y-16)/300));if(f>0)panel.LayoutTransform=Matrix.CreateScale(f);if(yes.IsClicked)confirm();if(no.IsClicked||Input.Back||Input.Cancel){Input.Clear();cancel();} }
    }
}
