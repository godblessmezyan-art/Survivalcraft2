using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.OmniscientEye
{
    /// <summary>World-owned, local-player UI state. All calls run on the game thread.</summary>
    public sealed class SubsystemOmniscientEye : SubsystemBlockBehavior
    {
        private readonly Dictionary<ComponentGui, EyeView> m_views = new Dictionary<ComponentGui, EyeView>();
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { OmniscientEyeBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (Terrain.ExtractContents(miner.ActiveBlockValue) != OmniscientEyeBlock.Index) return false;
            ComponentPlayer player = miner.ComponentPlayer;
            if (player?.GameWidget == null) return true;
            View(player.ComponentGui).OpenRequested = true;
            return true;
        }

        private EyeView View(ComponentGui gui)
        {
            EyeView view;
            if (!m_views.TryGetValue(gui, out view))
            {
                view = new EyeView(gui);
                m_views.Add(gui, view);
            }
            return view;
        }

        internal void Tick(ComponentGui gui)
        {
            // A server/remote player never gets widgets. Retire local players removed in split-screen.
            foreach (ComponentGui old in new List<ComponentGui>(m_views.Keys))
                if (old.m_componentPlayer?.GameWidget == null || old.m_componentPlayer.PlayerData.ComponentPlayer != old.m_componentPlayer)
                {
                    m_views[old].Dispose(); m_views.Remove(old);
                }
            EyeView view;
            if (gui?.m_componentPlayer?.GameWidget != null && m_views.TryGetValue(gui, out view))
            {
                try { view.Tick(); }
                catch (System.Exception exception)
                {
                    view.Dispose(); m_views.Remove(gui);
                    Log.Error("[SCIEPort:OmniscientEye] Local view closed after UI failure: " + exception);
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            foreach (EyeView view in m_views.Values) view.Dispose();
            m_views.Clear();
            base.Dispose();
        }
    }
}
