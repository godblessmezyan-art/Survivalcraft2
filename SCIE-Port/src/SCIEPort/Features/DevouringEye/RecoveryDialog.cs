using Engine;
using Game;

namespace SCIEPort.Features.DevouringEye
{
    internal sealed class RecoveryDialog : Dialog
    {
        private SubsystemDevouringEye m_runtime;
        private ComponentPlayer m_player;
        private RecoveryTarget m_target;
        private readonly CanvasWidget m_panel;
        private readonly LabelWidget m_info;
        private readonly BevelledButtonWidget m_identify,m_recovery,m_confirm,m_close;
        private bool m_readOnly,m_consumed;
        internal RecoveryDialog(SubsystemDevouringEye runtime,ComponentPlayer player,RecoveryTarget target)
        {
            m_runtime=runtime;m_player=player;m_target=target;
            m_panel=new CanvasWidget{Size=new Vector2(540,370),HorizontalAlignment=WidgetAlignment.Center,VerticalAlignment=WidgetAlignment.Center};Children.Add(m_panel);
            Add(new RectangleWidget{FillColor=new Color(20,12,35,245),OutlineColor=new Color(181,127,62),OutlineThickness=2},0,0);
            Add(new LabelWidget{Text="吞噬之眼 · Eye of Oblivion",FontScale=.85f,Color=new Color(225,191,255)},16,10);
            m_identify=Button("识别 · 只读",16,48,160);m_recovery=Button("回收 · 默认",184,48,160);
            Button("领域 / 湮灭：后续",352,48,172).IsEnabled=false;
            m_info=new LabelWidget{Size=new Vector2(504,216),FontScale=.7f,WordWrap=true,Color=new Color(210,183,235)};Add(m_info,18,104);
            string description=target?.Describe()??"6 格内没有目标。";
            try { new NativeRecovery(runtime,player,target).Validate();description+="\n可回收；确认后返还物品并保存。"; }
            catch(System.Exception e) {description+="\n"+e.Message;m_consumed=true;}
            m_info.Text=description;
            m_confirm=Button("确认回收并保存",16,318,300);m_close=Button("关闭 / 取消",332,318,192);
        }
        private void Add(Widget w,float x,float y){m_panel.Children.Add(w);m_panel.SetWidgetPosition(w,new Vector2(x,y));}
        private BevelledButtonWidget Button(string text,float x,float y,float width)
        {var b=new BevelledButtonWidget{Text=text,Size=new Vector2(width,44),FontScale=.65f,CenterColor=new Color(35,21,50),BevelColor=new Color(173,122,62)};Add(b,x,y);return b;}
        public override void Update()
        {
            if(m_runtime==null)return;
            Vector2 size=m_player.GameWidget.GuiWidget.ActualSize;
            float scale=MathUtils.Min(1,MathUtils.Min((size.X-16)/540,(size.Y-16)/370));if(scale>0)m_panel.LayoutTransform=Matrix.CreateScale(scale);
            if(m_identify.IsClicked)m_readOnly=true;if(m_recovery.IsClicked)m_readOnly=false;
            m_confirm.IsEnabled=!m_consumed&&!m_readOnly;
            m_confirm.Text=m_readOnly?"识别模式：不会修改世界":"确认回收并保存";
            if(m_confirm.IsClicked&&!m_consumed&&!m_readOnly){m_consumed=true;m_confirm.IsEnabled=false;m_info.Text="正在验证、回收并保存……";m_runtime.Queue(m_player,m_target,this);}
            if(m_close.IsClicked||Input.Back||Input.Cancel){Input.Clear();m_runtime.Cancel(m_player);m_runtime.Close(m_player);}
        }
        internal void Result(string result){if(m_runtime!=null)m_info.Text=result;}
        internal void Release(){m_runtime=null;m_player=null;m_target=null;}
    }
}
