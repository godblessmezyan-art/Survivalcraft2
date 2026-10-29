using System;
using BuilderAssistant.Core.Building;
using BuilderAssistant.Core.Workbench;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    internal sealed class BuildingWorkbenchDialog : Dialog
    {
        private readonly ComponentPlayer player;
        private readonly BuildingSelectionService service;
        private readonly BuildingWorkbench workbench;
        private readonly Action select, close;
        private readonly CanvasWidget panel, body, view;
        private readonly ScrollPanelWidget scroll;
        private readonly BevelledButtonWidget choice, roof, rotation, defaults, random, generate, position, cancel;
        private readonly TextBoxWidget width, length, floors, height, seed, density;
        private readonly LabelWidget info;
        private string key;
        private RoofType roofValue;
        private int direction;
        private BuildingSpec spec;
        private bool generated;

        internal BuildingWorkbenchDialog(ComponentPlayer player, BuildingSelectionService service, BuildingWorkbench workbench, Action select, Action close)
        {
            this.player = player; this.service = service; this.workbench = workbench; this.select = select; this.close = close;
            panel = new CanvasWidget { Size = new Vector2(460, 500), HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center };
            Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor = new Color(20, 28, 37, 245), OutlineColor = new Color(170, 145, 90), OutlineThickness = 2 });
            Add(panel, new LabelWidget { Text = "建筑工作台", FontScale = .9f, Color = Color.White }, 16, 10);
            scroll = new ScrollPanelWidget { Direction = LayoutDirection.Vertical, HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Stretch };
            view = new CanvasWidget { Size = new Vector2(436, 405) }; Add(panel, view, 12, 48); view.Children.Add(scroll);
            body = new CanvasWidget { Size = new Vector2(420, 730), HorizontalAlignment = WidgetAlignment.Near, VerticalAlignment = WidgetAlignment.Near }; scroll.Children.Add(body);
            choice = Button("建筑", 0, 0, 400);
            width = Field("宽度", 58); length = Field("长度", 112); floors = Field("楼层", 166); height = Field("每层墙高", 220);
            roof = Button("屋顶", 0, 274, 400); seed = Field("随机种子", 328); density = Field("窗户密度", 382);
            rotation = Button("朝向", 0, 436, 400);
            defaults = Button("恢复默认", 0, 490, 194); random = Button("随机种子", 206, 490, 194);
            generate = Button("生成蓝图", 0, 544, 400); position = Button("选择建造位置", 0, 598, 400);
            info = new LabelWidget { Size = new Vector2(400, 75), FontScale = .65f, WordWrap = true, Color = Color.White }; Add(body, info, 0, 652);
            cancel = new BevelledButtonWidget { Text = "关闭", Size = new Vector2(160, 42), FontScale = .7f }; Add(panel, cancel, 288, 452);
            key = workbench.Choice != null ? workbench.Choice.Key : "workbench_cottage";
            direction = workbench.Rotation; spec = workbench.Draft; generated = !workbench.NeedsRegenerate && workbench.EffectiveBlueprint() != null;
            if (spec == null && key != "workbench_imported") spec = BuildingSpec.DefaultCottage();
            LoadFields();
        }
        private void Add(CanvasWidget canvas, Widget widget, float x, float y) { canvas.Children.Add(widget); canvas.SetWidgetPosition(widget, new Vector2(x,y)); }
        private BevelledButtonWidget Button(string text, float x, float y, float w) { var b = new BevelledButtonWidget { Text = text, Size = new Vector2(w, 46), FontScale = .7f }; Add(body,b,x,y); return b; }
        private TextBoxWidget Field(string title, float y)
        {
            Add(body, new LabelWidget { Text = title, FontScale = .7f, Color = Color.White }, 4, y + 10);
            var bg = new RectangleWidget { Size = new Vector2(240, 46), FillColor = new Color(45, 52, 62), OutlineColor = Color.White, OutlineThickness = 1 }; Add(body,bg,160,y);
            var t = new TextBoxWidget { Size = new Vector2(230, 44), Title = title, Description = "请输入整数", MaximumLength = 12, FontScale = .8f }; Add(body,t,165,y); return t;
        }
        private void LoadFields()
        {
            bool imported = key == "workbench_imported";
            BuildingChoice c = service.Find(key); choice.Text = c != null ? c.DisplayName : "建筑不可用";
            foreach (var t in new[] { width,length,floors,height,seed,density }) t.IsEnabled = !imported;
            roof.IsEnabled = random.IsEnabled = !imported;
            if (!imported && spec != null)
            {
                width.Text=spec.Width.ToString(); length.Text=spec.Length.ToString(); floors.Text=spec.Floors.ToString(); height.Text=spec.WallHeight.ToString(); seed.Text=spec.Seed.ToString(); density.Text=spec.WindowDensity.ToString(); roofValue=spec.Roof;
            }
            else { width.Text=length.Text=floors.Text=height.Text=seed.Text=density.Text=""; }
            roof.Text = imported ? "屋顶：导入蓝图固定" : "屋顶：" + RoofName();
            rotation.Text = "朝向：" + (direction * 90) + "°";
            info.Text = imported ? "导入蓝图的尺寸和结构不可编辑；可调整朝向。" : "填写整数，生成后选择落点。拖动面板可滚动。";
        }
        private string RoofName() { return roofValue == RoofType.Hip ? "四坡" : roofValue == RoofType.Gabled ? "人字" : "平顶"; }
        private bool Number(TextBoxWidget box, string name, out int value)
        {
            if (!BuildingSpec.TryParseInteger(box.Text, out value)) { info.Text = name + "必须是有效的整数。"; return false; }
            return true;
        }
        private bool Apply()
        {
            BuildingSpec next = null;
            if (key != "workbench_imported")
            {
                next = spec; int a,b,c,d,e,f;
                if (!Number(width,"宽度",out a) || !Number(length,"长度",out b) || !Number(floors,"楼层",out c) || !Number(height,"每层墙高",out d) || !Number(seed,"种子",out e) || !Number(density,"窗户密度",out f)) return false;
                next.Width=a; next.Length=b; next.Floors=c; next.WallHeight=d; next.Seed=e; next.WindowDensity=f; next.Roof=roofValue;
            }
            WorkbenchStatus result = workbench.TryApplySpec(key,next,direction);
            if (!result.Accepted) { info.Text = result.Reason; return false; }
            return true;
        }
        public override void Update()
        {
            Vector2 size = player.GameWidget.GuiWidget.ActualSize;
            float scale = MathUtils.Min(1f, (size.X - 16f) / 460f);
            if (scale > 0)
            {
                float h = MathUtils.Min(500f, (size.Y - 16f) / scale);
                panel.Size = new Vector2(460f, h);
                view.Size = new Vector2(436f, MathUtils.Max(40f, h - 95f));
                panel.SetWidgetPosition(cancel, new Vector2(288f, h - 48f));
                panel.LayoutTransform = Matrix.CreateScale(scale);
            }
            if (choice.IsClicked)
            {
                var choices=service.Choices; int i=0; for(int j=0;j<choices.Count;j++) if(choices[j].Key==key) i=j;
                key=choices[(i+1)%choices.Count].Key; BuildingChoice c=service.Find(key); service.TryMakeDefaultSpec(c,out spec); direction=0; generated=false; LoadFields();
            }
            if (defaults.IsClicked) { BuildingChoice c=service.Find(key); service.TryMakeDefaultSpec(c,out spec); direction=0; generated=false; LoadFields(); }
            if (roof.IsClicked) { roofValue=roofValue==RoofType.Hip?RoofType.Gabled:roofValue==RoofType.Gabled?RoofType.Flat:RoofType.Hip; roof.Text="屋顶："+RoofName(); generated=false; }
            if (rotation.IsClicked) { direction=(direction+1)%4; rotation.Text="朝向："+(direction*90)+"°"; generated=false; }
            if (random.IsClicked) { byte[] bytes=new byte[4]; using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes); seed.Text=BitConverter.ToInt32(bytes,0).ToString(); generated=false; }
            if (generate.IsClicked && Apply()) { WorkbenchStatus r=workbench.Regenerate(); info.Text=r.Reason; generated=r.Accepted; }
            if (position.IsClicked && Apply())
            {
                if (!generated || workbench.NeedsRegenerate || workbench.EffectiveBlueprint()==null) info.Text="参数已变化，请先生成蓝图。";
                else select();
            }
            if (cancel.IsClicked || Input.Back || Input.Cancel) { Input.Clear(); close(); }
        }
    }

    internal sealed class WorkbenchConfirmDialog : Dialog
    {
        private readonly ComponentPlayer player; private readonly CanvasWidget panel;
        private readonly BevelledButtonWidget confirm, back; private readonly Action yes, edit, close;
        internal WorkbenchConfirmDialog(ComponentPlayer player, BuildPreviewModel model, Action yes, Action edit, Action close)
        {
            this.player=player; this.yes=yes; this.edit=edit; this.close=close;
            panel=new CanvasWidget { Size=new Vector2(460,300), HorizontalAlignment=WidgetAlignment.Center, VerticalAlignment=WidgetAlignment.Center }; Children.Add(panel);
            panel.Children.Add(new RectangleWidget { FillColor=new Color(20,28,37,245), OutlineColor=Color.White, OutlineThickness=2 });
            var label=new LabelWidget { Size=new Vector2(420,190), Text="落点：("+model.Origin.X+", "+model.Origin.Y+", "+model.Origin.Z+")\n"+model.Summarize()+"\n障碍物："+(model.Allowed ? "无阻挡" : model.DeniedReason), WordWrap=true, FontScale=.7f, Color=Color.White }; panel.Children.Add(label); panel.SetWidgetPosition(label,new Vector2(20,20));
            confirm=new BevelledButtonWidget { Text="确认建造", Size=new Vector2(200,48), IsEnabled=model.Allowed, FontScale=.7f }; panel.Children.Add(confirm); panel.SetWidgetPosition(confirm,new Vector2(20,235));
            back=new BevelledButtonWidget { Text="返回 / 取消", Size=new Vector2(200,48), FontScale=.7f }; panel.Children.Add(back); panel.SetWidgetPosition(back,new Vector2(240,235));
        }
        public override void Update() { Vector2 s=player.GameWidget.GuiWidget.ActualSize; float scale=MathUtils.Min(1f,MathUtils.Min((s.X-16)/460,(s.Y-16)/300)); if(scale>0)panel.LayoutTransform=Matrix.CreateScale(scale); if(confirm.IsClicked)yes(); if(back.IsClicked)edit(); if(Input.Back||Input.Cancel){Input.Clear();close();} }
    }
}
