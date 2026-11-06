using Engine;
using Game;
using GameEntitySystem;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.PixelFx
{
    /// <summary>Presentation adapters share the existing world scene, pool, atlas and camera budget.</summary>
    internal static class ArtifactSpriteFx
    {
        private static SpriteFxScene Scene(Project project) => AwakenedStormhammerVisuals.GetScene(project)?.SpriteFx;
        private static int Quality => (int)StormhammerOptions.Quality;
        private static bool Owner(Project project, ComponentPlayer player, out SpriteFxScene scene)
        { scene = Scene(project); return scene != null && player != null && player.Entity != null; }

        internal static void Excalibur(Project project, ComponentPlayer player, uint cast, byte skill, Vector3 origin, Vector3 direction, double now, float age)
        {
            SpriteFxScene scene;
            if (!Owner(project, player, out scene)) return;
            if (skill >= 1 && skill <= 3)
                scene.PlayUnique(ArtifactSpriteClips.HolyBlessing, origin - Vector3.UnitY * 1.5f, skill == 3 ? 6f : 4f,
                    now, Quality, SpriteFxEvents.Key(player.Entity.EntityId, cast, 1, 0), age, 4);
            if (skill == 2 && age >= SCIEPort.Features.Excalibur.ExcaliburRules.Release)
                ExcaliburRelease(project, player, cast, origin, direction, now, age - SCIEPort.Features.Excalibur.ExcaliburRules.Release);
        }

        internal static void ExcaliburRelease(Project project, ComponentPlayer player, uint cast, Vector3 origin, Vector3 direction, double now, float age)
        {
            SpriteFxScene scene;
            if (!Owner(project, player, out scene)) return;
            scene.PlayUnique(ArtifactSpriteClips.HolySunburst, origin + direction * 3f, 5.5f, now, Quality,
                SpriteFxEvents.Key(player.Entity.EntityId, cast, 1, 1), age, 4);
        }

        internal static void Gungnir(Project project, string cue, Vector3 point, double now)
        {
            SpriteFxScene scene = Scene(project);
            if (scene == null || (cue != "Invoke" && cue != "Impact") || !SpriteFxClip.Finite(point.X) || !SpriteFxClip.Finite(point.Y) || !SpriteFxClip.Finite(point.Z)) return;
            bool impact = cue == "Impact";
            scene.PlayUnique(impact ? ArtifactSpriteClips.HolySmite : ArtifactSpriteClips.HolySunburst,
                point + Vector3.UnitY * (impact ? 0.1f : 1f), impact ? 6f : 3.5f, now, Quality,
                SpriteFxEvents.Key(0, SpriteFxEvents.Spatial(point.X, point.Y, point.Z), 2, (byte)(impact ? 1 : 0)), 0, .12);
        }

        internal static void Poseidon(Project project, ComponentPlayer player, uint cast, byte skill, Vector3 origin, Vector3 direction, double now, float age)
        {
            if (skill < 1 || skill > 3) return;
            bool wave = skill == 2;
            float elapsed = wave ? age - SCIEPort.Features.Poseidon.PoseidonRules.WaveWindup : age;
            SpriteFxClip clip = wave ? ArtifactSpriteClips.WaterWave : ArtifactSpriteClips.WaterSplash;
            if (!SpriteFxClip.Finite(elapsed) || elapsed < 0 || elapsed >= clip.Duration) return;
            SpriteFxScene scene;
            if (!Owner(project, player, out scene)) return;
            scene.PlayUnique(clip,
                origin + (wave ? direction * 3f : Vector3.Zero) + Vector3.UnitY * .1f,
                wave ? 8f : skill == 3 ? 7f : 4f, now, Quality,
                SpriteFxEvents.Key(player.Entity.EntityId, cast, 3, skill), elapsed, 4);
        }

        internal static void Abyss(Project project, ComponentPlayer player, uint cast, Vector3 center, double now, float age)
        {
            float elapsed = age - (float)SCIEPort.Features.AbyssReaper.DeathAuthorityRules.WaveImpact;
            if (!SpriteFxClip.Finite(elapsed) || elapsed < 0 || elapsed >= ArtifactSpriteClips.DarkExplosion.Duration) return;
            SpriteFxScene scene;
            if (!Owner(project, player, out scene)) return;
            scene.PlayUnique(ArtifactSpriteClips.DarkExplosion, center + Vector3.UnitY, 8f, now, Quality,
                SpriteFxEvents.Key(player.Entity.EntityId, cast, 4, 0), elapsed, 4);
        }
    }
}
