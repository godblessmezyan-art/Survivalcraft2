using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>
    /// Local-only sky state for Eternal Judgment. It never writes SubsystemWeather, time,
    /// terrain or creature state. World geometry remains owned by GungnirScene.
    /// </summary>
    internal static class GungnirSkyVisuals
    {
        private sealed class SkyState
        {
            internal ushort Caster;
            internal Vector3 Target;
            internal double Start;
            internal double LastSeen;
            internal bool LocalCaster;
        }

        private static readonly Dictionary<Project, Dictionary<ushort, SkyState>> s_states =
            new Dictionary<Project, Dictionary<ushort, SkyState>>();

        internal static void Sync(Project project, ushort caster, Vector3 target, float age, bool localCaster, double now)
        {
            if (project == null || age < 0f || age >= GungnirSkyRules.Lifetime) return;
            Dictionary<ushort, SkyState> map;
            if (!s_states.TryGetValue(project, out map))
            {
                map = new Dictionary<ushort, SkyState>();
                s_states.Add(project, map);
            }

            SkyState state;
            if (!map.TryGetValue(caster, out state))
            {
                state = new SkyState { Caster = caster, Start = now - age };
                map.Add(caster, state);
            }
            state.Target = target;
            state.LocalCaster = localCaster;
            state.LastSeen = now;
        }

        internal static void RemoveCaster(Project project, ushort caster)
        {
            Dictionary<ushort, SkyState> map;
            if (project == null || !s_states.TryGetValue(project, out map)) return;
            map.Remove(caster);
            if (map.Count == 0) s_states.Remove(project);
        }

        internal static void Update(Project project, double now)
        {
            Dictionary<ushort, SkyState> map;
            if (project == null || !s_states.TryGetValue(project, out map)) return;
            List<ushort> stale = null;
            foreach (KeyValuePair<ushort, SkyState> pair in map)
            {
                SkyState state = pair.Value;
                if (GungnirSkyRules.ShouldRemove((float)(now - state.Start), (float)(now - state.LastSeen), true))
                    (stale ?? (stale = new List<ushort>())).Add(pair.Key);
            }
            if (stale != null) for (int i = 0; i < stale.Count; i++) map.Remove(stale[i]);
            if (map.Count == 0) s_states.Remove(project);
        }

        internal static float GetSkyBlend()
        {
            float combined = 0f;
            foreach (KeyValuePair<Project, Dictionary<ushort, SkyState>> projectPair in s_states)
            {
                SubsystemTime time = projectPair.Key.FindSubsystem<SubsystemTime>(false);
                if (time == null) continue;
                foreach (SkyState state in projectPair.Value.Values)
                {
                    if (!state.LocalCaster) continue;
                    combined = GungnirSkyRules.Combine(combined, GungnirSkyRules.SkyBlend((float)(time.GameTime - state.Start)));
                }
            }
            return MathUtils.Saturate(combined);
        }

        internal static float GetFlash()
        {
            float flash = 0f;
            foreach (KeyValuePair<Project, Dictionary<ushort, SkyState>> projectPair in s_states)
            {
                SubsystemTime time = projectPair.Key.FindSubsystem<SubsystemTime>(false);
                if (time == null) continue;
                foreach (SkyState state in projectPair.Value.Values)
                {
                    if (!state.LocalCaster) continue;
                    flash = MathUtils.Max(flash, GungnirSkyRules.ImpactFlash((float)(time.GameTime - state.Start), StormhammerOptions.ReducedFlashes));
                }
            }
            return flash;
        }

        internal static void Reset(Project project)
        {
            if (project != null) s_states.Remove(project);
        }
    }
}
