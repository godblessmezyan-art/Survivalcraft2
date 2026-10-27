using System;
using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    internal enum StormhammerEffectQuality
    {
        Low,
        Medium,
        High
    }

    internal static class StormhammerOptions
    {
        public static StormhammerEffectQuality Quality = StormhammerEffectQuality.High;
        public static bool ReducedFlashes;
        public static bool CameraShake;
        public static bool Diagnostics;
    }

    /// <summary>
    /// Client presentation for Skywrath V4. Owns the storm timeline, the deterministic backdrop
    /// schedule, the sky flash impulse and the impact shake. It never writes damage, weather,
    /// time or terrain; the authoritative subsystem publishes the events it reacts to.
    /// </summary>
    internal static class AwakenedStormhammerVisuals
    {
        /// <summary>
        /// Seconds from cast start to the main bolt landing. The server publishes exactly this
        /// value, so the bolt and the damage share one clock. Short on purpose: Mjolnir is meant to
        /// hit the ground the moment it is swung, not after a five second wind-up.
        /// </summary>
        public const float MainStrikeTime = 0.28f;

        /// <summary>First heavy bolt after the main strike, and the spacing between them.</summary>
        public const float GreatBoltFirst = 0.55f;

        /// <summary>Last heavy bolt of the window.</summary>
        public const float GreatBoltLast = 11.20f;

        /// <summary>Seconds the storm sky and backdrop stay active (matches the Thunder God window).</summary>
        public const float StormReleaseTime = 12.0f;

        /// <summary>Extra seconds the sky needs to fade out after the window closes.</summary>
        public const float StormFadeTail = 1.6f;

        private sealed class StormState
        {
            public uint CastId;
            public Vector3 Centre;
            public Vector3 Forward;
            public double Start;
            public double End;
            public uint Seed;
            public bool LocalCaster;
            public LightningRules.StormStrike[] Schedule;
            public int Cursor;
            public LightningRules.StormStrike[] GreatSchedule;
            public int GreatCursor;
            public double NextSkyFlash;
            public bool MainSpawned;
        }

        private sealed class AvatarGlow
        {
            public SubsystemGlow Subsystem;
            public readonly List<GlowPoint> Points = new List<GlowPoint>();
            public Vector4? OriginalEmission;
            public int LastFrame;
            public double NextArc;
            public double NextFootstep;
            public Vector3 LastCenter;
            public Vector3 LastFootstep;
        }

        private static readonly Dictionary<Project, List<StormState>> s_storms = new Dictionary<Project, List<StormState>>();
        private static readonly Dictionary<Project, Dictionary<ulong, double>> s_seenEvents = new Dictionary<Project, Dictionary<ulong, double>>();
        private static readonly Dictionary<Project, float> s_flash = new Dictionary<Project, float>();
        private static readonly Dictionary<ComponentModel, AvatarGlow> s_avatars = new Dictionary<ComponentModel, AvatarGlow>();
        private static readonly Dictionary<Project, LightningScene> s_scenes = new Dictionary<Project, LightningScene>();
        private static readonly Dictionary<Project, Dictionary<ushort, double>> s_overcharge =
            new Dictionary<Project, Dictionary<ushort, double>>();

        /// <summary>The bolt pool owned by the project, or null before the subsystem loads.</summary>
        public static LightningScene GetScene(Project project)
        {
            if (project == null) return null;
            LightningScene scene;
            return s_scenes.TryGetValue(project, out scene) ? scene : null;
        }

        /// <summary>Registers the geometry pool owned by the project's subsystem.</summary>
        public static void RegisterScene(Project project, LightningScene scene)
        {
            if (project == null) return;
            if (scene == null) s_scenes.Remove(project);
            else s_scenes[project] = scene;
        }

        /// <summary>Adds the lightning scene of a project so events can spawn geometry.</summary>
        public static void Apply(Project project, StormhammerSkillPackage package, LightningScene scene, double now)
        {
            if (package.CastId != 0u && !AcceptEvent(project, package)) return;
            SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
            switch (package.Event)
            {
                case StormhammerSkillEvent.JudgmentCharge:
                    EmitCharge(project, package.Position, scene, now, 0.42f);
                    audio?.PlaySound("Audio/Stormhammer/Charge", 0.85f, -0.06f, package.Position, 10f, false);
                    break;
                case StormhammerSkillEvent.JudgmentBurst:
                    EmitJudgment(project, package.Position, package.CastId, scene, now, package.Step == 1);
                    audio?.PlaySound("Audio/Stormhammer/Strike", 1f, 0.04f, package.Position, 26f, false);
                    break;
                case StormhammerSkillEvent.UltimateStart:
                    BeginStorm(project, package, scene, now);
                    audio?.PlaySound("Audio/Stormhammer/Charge", 1f, -0.22f, package.Position, 26f, false);
                    break;
                case StormhammerSkillEvent.UltimateMainWarning:
                    if (scene != null) scene.SpawnRune(package.Position, 1.2f, 9.5f, now, 1.45f, new Color(128, 168, 255, 235));
                    break;
                case StormhammerSkillEvent.UltimateMainStrike:
                    EmitMainStrike(project, package, scene, now);
                    break;
                case StormhammerSkillEvent.UltimateAftershock:
                    EmitAftershock(project, package.Position, package.Step, scene, now);
                    break;
                case StormhammerSkillEvent.UltimateEnd:
                    EndStorm(project, package.CastId, now);
                    audio?.PlaySound("Audio/Stormhammer/Rumble", 0.7f, -0.34f, package.Position, 40f, true);
                    break;
                case StormhammerSkillEvent.ThunderGodStart:
                    SetOvercharge(project, package.CasterId, now + MathUtils.Max(package.Value, 1f));
                    break;
                case StormhammerSkillEvent.ThunderGodEnd:
                    ClearOvercharge(project, package.CasterId);
                    break;
            }
        }

        private static bool AcceptEvent(Project project, StormhammerSkillPackage package)
        {
            Dictionary<ulong, double> seen;
            if (!s_seenEvents.TryGetValue(project, out seen))
            {
                seen = new Dictionary<ulong, double>();
                s_seenEvents.Add(project, seen);
            }

            double now = Time.RealTime;
            if (seen.Count > 192)
            {
                List<ulong> expired = new List<ulong>();
                foreach (KeyValuePair<ulong, double> pair in seen)
                    if (pair.Value <= now) expired.Add(pair.Key);
                foreach (ulong key in expired) seen.Remove(key);
            }

            ulong eventKey = package.CastId;
            eventKey = eventKey * 1099511628211UL ^ (uint)package.Event;
            eventKey = eventKey * 1099511628211UL ^ (uint)package.Step;
            eventKey = eventKey * 1099511628211UL ^ (uint)package.TargetId;
            double expiry;
            if (seen.TryGetValue(eventKey, out expiry) && expiry > now) return false;
            seen[eventKey] = now + 20.0;
            return true;
        }

        // ------------------------------------------------------------------
        // Skill 2: radial thunder
        // ------------------------------------------------------------------

        private static void EmitCharge(Project project, Vector3 centre, LightningScene scene, double now, float duration)
        {
            if (scene != null)
            {
                scene.SpawnRune(centre, 0.5f, 2.2f, now, duration, new Color(150, 205, 255, 210));
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * MathUtils.PI * 0.5f + (float)now * 2.1f;
                    Vector3 from = centre + Vector3.UnitY * 0.2f + new Vector3(MathUtils.Cos(angle) * 1.9f, 0f, MathUtils.Sin(angle) * 1.9f);
                    scene.Spawn(LightningBoltKind.Radial, from, centre + Vector3.UnitY * 1.15f,
                        LightningRules.Mix((uint)(now * 977.0), (uint)i), now, 0.20, 0.10f, 0.34f,
                        new Color(214, 240, 255, 210), new Color(72, 170, 255, 120), 0, 0f, false);
                }
            }

            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateBurst(centre + Vector3.UnitY * 1.2f, 1.1f));
        }

        private static void EmitJudgment(Project project, Vector3 centre, uint castId, LightningScene scene, double now, bool boosted)
        {
            int baseCount = StormhammerOptions.Quality == StormhammerEffectQuality.High ? 12
                : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 10 : 8;
            int count = boosted ? baseCount + 2 : baseCount;
            float coreWidth = boosted ? 0.70f : 0.52f;
            float haloWidth = boosted ? 1.95f : 1.45f;
            int branches = boosted ? 3 : 2;
            Vector3 origin = centre + Vector3.UnitY * 1.15f;
            if (scene != null)
            {
                scene.SpawnRune(centre, 1.4f, LightningRules.JudgmentRadius, now, 0.75f, new Color(126, 206, 255, 235));
                for (int i = 0; i < count; i++)
                {
                    float angle = 2f * MathUtils.PI * i / count + (castId % 17) * 0.031f;
                    float reach = 8.4f + ((i % 3) - 1) * 0.45f;
                    Vector3 end = centre + new Vector3(MathUtils.Cos(angle) * reach, 0.12f, MathUtils.Sin(angle) * reach);
                    scene.Spawn(LightningBoltKind.Radial, origin, end,
                        LightningRules.Mix(castId, (uint)(0x40 + i)), now, boosted ? 0.72 : 0.62,
                        coreWidth, haloWidth,
                        new Color(238, 250, 255, 250), new Color(64, 168, 255, 190),
                        branches, 0f, false);
                }

                scene.SpawnRune(centre, 2.6f, 5.6f, now, 0.5f, new Color(206, 244, 255, 220));
                scene.Spawn(LightningBoltKind.Radial,
                    origin, centre + Vector3.UnitY * 3.4f,
                    LightningRules.Mix(castId, 0x77u), now, 0.30, boosted ? 1.05f : 0.8f, boosted ? 2.7f : 2.1f,
                    new Color(240, 252, 255, 250), new Color(80, 180, 255, 170), 0, 0f, true);
                if (boosted)
                {
                    scene.SpawnRune(centre, 3.2f, 10.5f, now, 0.85f, new Color(150, 210, 255, 210));
                }
            }

            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateBurst(origin, boosted ? 2.9f : 2.2f));
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateRing(centre, boosted ? 5.4f : 4.2f, new Color(205, 248, 255, 245), 0.45f, boosted ? 52 : 40));
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateRing(centre + Vector3.UnitY * 0.18f, 9f, new Color(46, 158, 255, 200), 0.8f, 52));
            ShakeNearby(project, centre, boosted ? 20f : 14f, boosted ? 0.5f : 0.35f);
            AddFlash(project, boosted ? 0.85f : 0.55f);
        }

        // ------------------------------------------------------------------
        // Skill 3: the single main bolt plus the backdrop storm
        // ------------------------------------------------------------------

        private static void BeginStorm(Project project, StormhammerSkillPackage package, LightningScene scene, double now)
        {
            List<StormState> list;
            if (!s_storms.TryGetValue(project, out list))
            {
                list = new List<StormState>();
                s_storms.Add(project, list);
            }

            list.RemoveAll(s => s.CastId == package.CastId);
            Vector3 forward = package.Direction;
            forward.Y = 0f;
            if (forward.LengthSquared() < 0.01f) forward = Vector3.UnitZ;
            forward = Vector3.Normalize(forward);

            // Density phases: build up, hold, hush just before the main bolt, climax on impact,
            // then a long sustain so the backdrop covers the whole 12 s window.
            float density = StormhammerOptions.Quality == StormhammerEffectQuality.High ? 1f
                : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 0.62f : 0.34f;
            // No hush window any more: the main bolt lands at 0.28 s, so the storm opens on the
            // climax instead of building to it, then keeps hammering for the rest of the window.
            var phases = new[]
            {
                new LightningRules.StormPhase(0.05f, 1.10f, Scale(4, density), Scale(10, density)),
                new LightningRules.StormPhase(1.10f, 3.20f, Scale(7, density), Scale(26, density)),
                new LightningRules.StormPhase(3.20f, 6.60f, Scale(10, density), Scale(40, density)),
                new LightningRules.StormPhase(6.60f, 11.60f, Scale(7, density), Scale(28, density))
            };
            int great = StormhammerOptions.Quality == StormhammerEffectQuality.High ? 11
                : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 8 : 5;
            uint seed = LightningRules.Mix(package.CastId, 0x5F356495u);
            list.Add(new StormState
            {
                CastId = package.CastId,
                Centre = package.Position,
                Forward = forward,
                Start = now,
                End = now + StormReleaseTime + StormFadeTail,
                Seed = seed,
                LocalCaster = IsLocalCaster(project, package.CasterId),
                Schedule = LightningRules.BuildStormSchedule(seed, phases),
                GreatSchedule = LightningRules.BuildGreatBoltSchedule(
                    LightningRules.Mix(seed, 0x2B7E1516u), GreatBoltFirst, GreatBoltLast, great, 8f, 18f),
                NextSkyFlash = now + 0.16
            });

            if (scene != null)
            {
                scene.SpawnRune(package.Position, 1.0f, 9.5f, now, 4.0f, new Color(112, 138, 255, 175));
                scene.SpawnRune(package.Position, 0.6f, 6.4f, now, 3.4f, new Color(96, 214, 255, 150));
            }

            SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
            audio?.PlaySound("Audio/Stormhammer/Rumble", 0.65f, -0.3f, package.Position, 44f, true);
            AddFlash(project, 0.2f);
        }

        private static int Scale(int count, float density)
        {
            return MathUtils.Max(1, (int)Math.Round(count * density));
        }

        private static void SetOvercharge(Project project, ushort casterId, double endTime)
        {
            Dictionary<ushort, double> map;
            if (!s_overcharge.TryGetValue(project, out map))
            {
                map = new Dictionary<ushort, double>();
                s_overcharge.Add(project, map);
            }

            map[casterId] = endTime;
        }

        private static void ClearOvercharge(Project project, ushort casterId)
        {
            Dictionary<ushort, double> map;
            if (s_overcharge.TryGetValue(project, out map)) map.Remove(casterId);
        }

        /// <summary>True while the entity still owns an unexpired Thunder God window.</summary>
        private static bool IsOvercharged(Project project, ushort entityId)
        {
            Dictionary<ushort, double> map;
            if (!s_overcharge.TryGetValue(project, out map)) return false;
            double end;
            if (!map.TryGetValue(entityId, out end)) return false;
            SubsystemTime time = project.FindSubsystem<SubsystemTime>(false);
            return (time?.GameTime ?? Time.RealTime) < end;
        }

        /// <summary>
        /// Used by the held weapon model: is this weapon in the hands of an overcharged player?
        /// Keeps the hammer core in sync with the body glow without a second state source.
        /// </summary>
        public static bool IsOverchargedNear(Project project, Vector3 worldPosition)
        {
            Dictionary<ushort, double> map;
            if (project == null || !s_overcharge.TryGetValue(project, out map) || map.Count == 0) return false;
            SubsystemPlayers players = project.FindSubsystem<SubsystemPlayers>(false);
            if (players == null) return false;
            ReadOnlyList<ComponentPlayer> list = players.ComponentPlayers;
            for (int i = 0; i < list.Count; i++)
            {
                ComponentPlayer player = list[i];
                if (player?.ComponentBody == null) continue;
                if (!map.ContainsKey(player.Entity.EntityId)) continue;
                if (Vector3.DistanceSquared(player.ComponentBody.BoundingBox.Center(), worldPosition) > 9f) continue;
                if (IsOvercharged(project, player.Entity.EntityId)) return true;
            }

            return false;
        }

        private static bool IsLocalCaster(Project project, ushort casterId)
        {
            SubsystemPlayers players = project.FindSubsystem<SubsystemPlayers>(false);
            if (players == null) return false;
            ReadOnlyList<PlayerData> data = players.PlayersData;
            for (int i = 0; i < data.Count; i++)
            {
                PlayerData player = data[i];
                if (player?.ComponentPlayer == null) continue;
                if (player.ComponentPlayer.Entity.EntityId == casterId) return player.IsMainPlayer;
            }

            return false;
        }

        private static void EmitMainStrike(Project project, StormhammerSkillPackage package, LightningScene scene, double now)
        {
            Vector3 impact = package.Position;
            Vector3 start = new Vector3(impact.X, SkyStartHeight(project, impact), impact.Z);
            if (scene != null)
            {
                scene.Spawn(LightningBoltKind.Main, start, impact,
                    LightningRules.Mix(package.CastId, 0xC0FFEEu), now, 1.05,
                    2.45f, 5.6f,
                    new Color(250, 254, 255, 255), new Color(70, 158, 255, 225),
                    7, 9.5f, true);
                scene.SpawnRune(impact, 1.2f, 12.5f, now, 1.30f, new Color(214, 236, 255, 235));
                scene.SpawnRune(impact, 2.0f, 8.4f, now, 1.00f, new Color(96, 186, 255, 200));
                // Visual-only shockwave rings; they carry no damage and never touch terrain.
                scene.SpawnRune(impact, 3.0f, 15.5f, now, 1.55f, new Color(120, 176, 255, 175));
                scene.SpawnRune(impact, 2.2f, 10.0f, now, 1.15f, new Color(190, 226, 255, 160));
            }

            SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
            audio?.PlaySound("Audio/Stormhammer/Strike", 1f, -0.3f, impact, 56f, false);
            audio?.PlaySound("Audio/Stormhammer/Rumble", 0.9f, -0.16f, impact, 64f, true);
            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateBurst(impact + Vector3.UnitY * 0.4f, 3.9f));
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateRing(impact, 6.4f, new Color(230, 248, 255, 250), 0.62f, 64));
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateRing(impact + Vector3.UnitY * 0.2f, 11.5f, new Color(58, 166, 255, 215), 1.0f, 64));
            particles?.AddParticleSystem(StormhammerParticleSystem.CreateRing(impact + Vector3.UnitY * 0.1f, 15.5f, new Color(96, 150, 240, 170), 1.35f, 64));
            ShakeNearby(project, impact, 34f, 0.85f);
            AddFlash(project, 1f);

            List<StormState> list;
            if (s_storms.TryGetValue(project, out list))
            {
                foreach (StormState state in list)
                {
                    if (state.CastId == package.CastId) state.MainSpawned = true;
                }
            }
        }

        private static float SkyStartHeight(Project project, Vector3 impact)
        {
            SubsystemTerrain terrain = project.FindSubsystem<SubsystemTerrain>(false);
            float groundTop = impact.Y;
            if (terrain != null)
            {
                int x = Terrain.ToCell(impact.X);
                int z = Terrain.ToCell(impact.Z);
                TerrainChunk chunk = terrain.Terrain.GetChunkAtCell(x, z);
                if (chunk != null && chunk.State >= TerrainChunkState.InvalidLight)
                {
                    for (int y = 254; y > 0; y--)
                    {
                        if (Terrain.ExtractContents(terrain.Terrain.GetCellValue(x, y, z)) != 0)
                        {
                            groundTop = MathUtils.Max(groundTop, y + 1);
                            break;
                        }
                    }
                }
            }

            return MathUtils.Clamp(groundTop + 34f, impact.Y + 22f, 250f);
        }

        private static void EmitAftershock(Project project, Vector3 centre, byte step, LightningScene scene, double now)
        {
            if (scene == null) return;
            float radius = MathUtils.Max(2.2f, 8.6f - step * 1.5f);
            scene.SpawnRune(centre, MathUtils.Max(0.8f, radius * 0.4f), radius, now, 0.55f, new Color(88, 172, 255, 190));
            for (int i = 0; i < 3; i++)
            {
                float angle = i * MathUtils.PI * 2f / 3f + step * 0.37f;
                Vector3 end = centre + new Vector3(MathUtils.Cos(angle) * radius * 0.85f, 0.1f, MathUtils.Sin(angle) * radius * 0.85f);
                scene.Spawn(LightningBoltKind.Radial, centre + Vector3.UnitY * 0.9f, end,
                    LightningRules.Mix(0xA17E5u + step, (uint)i), now, 0.26, 0.16f, 0.55f,
                    new Color(212, 240, 255, 210), new Color(66, 150, 255, 140), 1, 0f, false);
            }
        }

        /// <summary>Advances every storm: backdrop bolts, sky flash, expiry.</summary>
        public static void Update(Project project, double now, LightningScene scene)
        {
            List<StormState> list;
            if (s_storms.TryGetValue(project, out list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    StormState state = list[i];
                    if (now >= state.End)
                    {
                        list.RemoveAt(i);
                        continue;
                    }

                    if (!state.LocalCaster || scene == null || state.Schedule == null)
                    {
                        continue;
                    }

                    float elapsed = (float)(now - state.Start);
                    while (state.Cursor < state.Schedule.Length && state.Schedule[state.Cursor].Time <= elapsed)
                    {
                        LightningRules.StormStrike strike = state.Schedule[state.Cursor];
                        state.Cursor++;
                        SpawnBackdropStrike(project, scene, state, strike, now);
                    }

                    // Heavy layer: full sized, branched bolts in the near field, held back until the
                    // main strike has landed so the first one cannot upstage it.
                    if (state.MainSpawned && state.GreatSchedule != null)
                    {
                        while (state.GreatCursor < state.GreatSchedule.Length
                            && state.GreatSchedule[state.GreatCursor].Time <= elapsed)
                        {
                            LightningRules.StormStrike strike = state.GreatSchedule[state.GreatCursor];
                            state.GreatCursor++;
                            SpawnBackdropStrike(project, scene, state, strike, now);
                        }
                    }

                    if (now >= state.NextSkyFlash && elapsed < 11.6f)
                    {
                        state.NextSkyFlash = now + (StormhammerOptions.Quality == StormhammerEffectQuality.High ? 0.28 : 0.55);
                        AddFlash(project, 0.18f);
                    }
                }

                if (list.Count == 0) s_storms.Remove(project);
            }

            List<ComponentModel> stale = null;
            foreach (KeyValuePair<ComponentModel, AvatarGlow> pair in s_avatars)
            {
                if (pair.Key.Project == project && Time.FrameIndex - pair.Value.LastFrame > 3) (stale ?? (stale = new List<ComponentModel>())).Add(pair.Key);
            }

            if (stale != null) foreach (ComponentModel model in stale) ReleaseAvatar(model);
        }

        private static void SpawnBackdropStrike(Project project, LightningScene scene, StormState state, LightningRules.StormStrike strike, double now)
        {
            float angle = MathUtils.Atan2(state.Forward.Z, state.Forward.X) + strike.Angle;
            var direction = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
            Vector3 ground = state.Centre + direction * strike.Distance;
            ground.Y = GroundHeight(project, ground);

            // Never force chunk loads for a backdrop effect: if the column is not resident,
            // terminate the bolt in the air instead of punching a hole into unknown terrain.
            bool resident = IsResident(project, ground);
            Vector3 impact = resident ? ground : new Vector3(ground.X, state.Centre.Y + 6f, ground.Z);
            Vector3 start = new Vector3(ground.X, state.Centre.Y + strike.Height, ground.Z);

            if (strike.Great)
            {
                // A heavy bolt: the same shape family as the main strike, scaled so it reads as a
                // real Mjolnir bolt landing in the near field instead of another backdrop tick.
                scene.Spawn(
                    LightningBoltKind.Main,
                    start,
                    impact,
                    strike.Seed,
                    now,
                    0.85,
                    1.15f,
                    3.10f,
                    new Color(248, 252, 255, 245),
                    new Color(66, 150, 255, 210),
                    4,
                    5.5f,
                    resident);
                scene.SpawnRune(impact, 0.8f, 5.2f, now, 0.90f, new Color(190, 226, 255, 215));
                AddFlash(project, 0.42f);
                SubsystemAudio heavyAudio = project.FindSubsystem<SubsystemAudio>(false);
                heavyAudio?.PlaySound("Audio/Stormhammer/Strike", 0.55f, -0.18f, ground, 52f, false);
                SubsystemParticles heavyParticles = project.FindSubsystem<SubsystemParticles>(false);
                heavyParticles?.AddParticleSystem(StormhammerParticleSystem.CreateBurst(impact + Vector3.UnitY * 0.4f, 2.2f));
                return;
            }

            bool near = strike.Near;
            float core = near ? 0.30f : 0.13f;
            float halo = near ? 1.05f : 0.5f;
            Color coreColor = near ? new Color(236, 250, 255, 235) : new Color(196, 226, 255, 200);
            Color haloColor = near ? new Color(72, 168, 255, 165) : new Color(58, 132, 240, 130);
            scene.Spawn(
                near ? LightningBoltKind.Near : LightningBoltKind.Far,
                start,
                impact,
                strike.Seed,
                now,
                near ? 0.46 : 0.34,
                core,
                halo,
                coreColor,
                haloColor,
                near ? 2 : 0,
                0f,
                false);

            if (near)
            {
                AddFlash(project, 0.16f);
                SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
                // Staggered volume: never one maximum-volume thunder per backdrop bolt.
                audio?.PlaySound("Audio/Stormhammer/Rumble", 0.34f, MathUtils.Lerp(0.35f, -0.25f, (float)LightningRules.NextFloat(strike.Seed)), ground, 60f, true);
            }
        }

        private static float GroundHeight(Project project, Vector3 point)
        {
            SubsystemTerrain terrain = project.FindSubsystem<SubsystemTerrain>(false);
            if (terrain == null) return point.Y;
            int x = Terrain.ToCell(point.X);
            int z = Terrain.ToCell(point.Z);
            TerrainChunk chunk = terrain.Terrain.GetChunkAtCell(x, z);
            if (chunk == null || chunk.State < TerrainChunkState.InvalidLight) return point.Y;
            for (int y = 250; y > 0; y--)
            {
                if (Terrain.ExtractContents(terrain.Terrain.GetCellValue(x, y, z)) != 0) return y + 1;
            }

            return point.Y;
        }

        private static bool IsResident(Project project, Vector3 point)
        {
            SubsystemTerrain terrain = project.FindSubsystem<SubsystemTerrain>(false);
            if (terrain == null) return true;
            TerrainChunk chunk = terrain.Terrain.GetChunkAtCell(Terrain.ToCell(point.X), Terrain.ToCell(point.Z));
            return chunk != null && chunk.State >= TerrainChunkState.InvalidLight;
        }

        // ------------------------------------------------------------------
        // Sky, flash and shake
        // ------------------------------------------------------------------

        public static float GetStormBlend()
        {
            float combined = 0f;
            foreach (KeyValuePair<Project, List<StormState>> pair in s_storms)
            {
                SubsystemTime time = pair.Key.FindSubsystem<SubsystemTime>(false);
                if (time == null) continue;
                foreach (StormState state in pair.Value)
                {
                    if (!state.LocalCaster) continue;
                    double now = time.GameTime;
                    float fadeIn = MathUtils.Saturate((float)((now - state.Start) / 1.25));
                    float fadeOut = MathUtils.Saturate((float)((state.End - now) / 1.6));
                    combined = 1f - (1f - combined) * (1f - MathUtils.Min(fadeIn, fadeOut));
                }
            }

            return MathUtils.Saturate(combined);
        }

        /// <summary>Additive sky/fog luminance from recent strikes, 0..1, damped by the flash setting.</summary>
        public static float GetFlash()
        {
            float flash = 0f;
            foreach (KeyValuePair<Project, float> pair in s_flash)
            {
                flash = MathUtils.Max(flash, pair.Value);
            }

            return StormhammerOptions.ReducedFlashes ? flash * 0.35f : flash;
        }

        private static void AddFlash(Project project, float amount)
        {
            float current;
            s_flash.TryGetValue(project, out current);
            s_flash[project] = MathUtils.Clamp(current + amount, 0f, 1f);
        }

        /// <summary>Decays the flash impulse. Called once per frame from the update hook.</summary>
        public static void DecayFlash(Project project, float dt)
        {
            float current;
            if (!s_flash.TryGetValue(project, out current)) return;
            s_flash[project] = MathUtils.Max(0f, current - dt * 3.6f);
        }

        private static void ShakeNearby(Project project, Vector3 centre, float radius, float strength)
        {
            if (!StormhammerOptions.CameraShake) return;
            SubsystemBodies bodies = project.FindSubsystem<SubsystemBodies>(false);
            if (bodies == null) return;
            var found = new DynamicArray<ComponentBody>();
            bodies.FindBodiesInArea(new Vector2(centre.X - radius, centre.Z - radius), new Vector2(centre.X + radius, centre.Z + radius), found);
            for (int i = 0; i < found.Count; i++)
            {
                ComponentBody body = found.Array[i];
                if (body == null || body.Player == null) continue;
                float distance = Vector3.Distance(body.Position, centre);
                if (distance > radius) continue;
                body.ApplyShaking(strength * (1f - distance / radius));
            }
        }

        private static void EndStorm(Project project, uint castId, double now)
        {
            List<StormState> list;
            if (!s_storms.TryGetValue(project, out list)) return;
            foreach (StormState state in list)
            {
                if (state.CastId == castId) state.End = MathUtils.Min(state.End, now + 1.4);
            }
        }

        /// <summary>Drops every storm/flash/glow for a project. Used on world change and dispose.</summary>
        public static void Reset(Project project)
        {
            s_storms.Remove(project);
            s_scenes.Remove(project);
            s_overcharge.Remove(project);
            s_flash.Remove(project);
            s_seenEvents.Remove(project);
            List<ComponentModel> stale = new List<ComponentModel>();
            foreach (KeyValuePair<ComponentModel, AvatarGlow> pair in s_avatars)
            {
                if (pair.Key.Project == project) stale.Add(pair.Key);
            }

            foreach (ComponentModel model in stale) ReleaseAvatar(model);
        }

        // ------------------------------------------------------------------
        // Avatar glow (unchanged behaviour, preserved from V3.2)
        // ------------------------------------------------------------------

        public static void TouchAvatar(ComponentModel model, Camera camera)
        {
            ComponentHumanModel human = model as ComponentHumanModel;
            if (human == null || model.Entity == null) return;
            ComponentPlayer player = model.Entity.FindComponent<ComponentPlayer>(false);
            if (!SubsystemAwakenedStormhammerBlockBehavior.IsEquipped(player) || player.ComponentHealth.Health <= 0f)
            {
                ReleaseAvatar(model);
                return;
            }

            AvatarGlow state;
            if (!s_avatars.TryGetValue(model, out state))
            {
                SubsystemGlow glow = model.Project.FindSubsystem<SubsystemGlow>(false);
                if (glow == null) return;
                state = new AvatarGlow { Subsystem = glow, OriginalEmission = model.EmissionColor };
                int count = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 3 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 5 : 7;
                for (int i = 0; i < count; i++)
                {
                    GlowPoint point = glow.AddGlowPoint();
                    point.Type = GlowPointType.Round;
                    state.Points.Add(point);
                }

                s_avatars.Add(model, state);
            }

            state.LastFrame = Time.FrameIndex;
            float pulse = 0.78f + 0.22f * MathUtils.Sin((float)Time.RealTime * 2.15f + player.Entity.EntityId * 0.11f);
            float ultimate = GetStormBlend();
            model.EmissionColor = new Vector4(0.035f + 0.025f * ultimate, 0.13f + 0.12f * ultimate, 0.22f + 0.16f * ultimate, 0f) * pulse;
            Vector3 center = player.ComponentBody.BoundingBox.Center();
            Matrix bodyMatrix = player.ComponentBody.Matrix;
            Vector3[] anchors =
            {
                BoneWorld(human, human.m_headBone != null ? human.m_headBone.Index : -1, camera, center + bodyMatrix.Up * 0.78f),
                BoneWorld(human, human.m_hand1Bone != null ? human.m_hand1Bone.Index : -1, camera, center - bodyMatrix.Right * 0.34f + bodyMatrix.Up * 0.38f),
                BoneWorld(human, human.m_hand2Bone != null ? human.m_hand2Bone.Index : -1, camera, center + bodyMatrix.Right * 0.34f + bodyMatrix.Up * 0.38f),
                BoneWorld(human, human.m_bodyBone != null ? human.m_bodyBone.Index : -1, camera, center + bodyMatrix.Up * 0.14f),
                BoneWorld(human, human.m_leg1Bone != null ? human.m_leg1Bone.Index : -1, camera, center - bodyMatrix.Right * 0.20f - bodyMatrix.Up * 0.42f),
                BoneWorld(human, human.m_leg2Bone != null ? human.m_leg2Bone.Index : -1, camera, center + bodyMatrix.Right * 0.20f - bodyMatrix.Up * 0.42f),
                center - bodyMatrix.Up * 0.68f
            };
            for (int i = 0; i < state.Points.Count; i++)
            {
                Vector3 p = anchors[i];
                GlowPoint glow = state.Points[i];
                glow.Position = p;
                glow.Right = bodyMatrix.Right;
                glow.Up = bodyMatrix.Up;
                glow.Forward = bodyMatrix.Forward;
                glow.Size = (i == 0 ? 0.34f : 0.24f) * pulse * (1f + 0.35f * ultimate);
                glow.FarSize = glow.Size * 1.5f;
                glow.FarDistance = 14f;
                glow.Color = new Color(62, 198, 255, StormhammerOptions.ReducedFlashes ? 55 : 82);
            }

            LightningScene scene;
            if (!s_scenes.TryGetValue(model.Project, out scene) || scene == null) return;
            bool overload = IsOvercharged(model.Project, player.Entity.EntityId);
            double interval = StormhammerOptions.Quality == StormhammerEffectQuality.High ? 0.16
                : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 0.28 : 0.5;
            if (overload) interval *= StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 0.55 : 0.42;
            double now = Time.RealTime;
            if (now >= state.NextArc)
            {
                int a = (Time.FrameIndex + player.Entity.EntityId) % state.Points.Count;
                int b = (a + 2 + Time.FrameIndex % 3) % state.Points.Count;
                uint seed = LightningRules.Mix((uint)(now * 733.0), player.Entity.EntityId);
                scene.Spawn(LightningBoltKind.Radial, state.Points[a].Position, state.Points[b].Position, seed, now, 0.16,
                    overload ? 0.13f : 0.07f, overload ? 0.36f : 0.19f,
                    new Color(226, 248, 255, 220), new Color(70, 176, 255, 140), overload ? 1 : 0, 0f, false);
                if (overload)
                {
                    // Shoulder/torso harness arcs: the silhouette reads as overloaded even at range.
                    int shoulder = 0;
                    int hip = 4;
                    scene.Spawn(LightningBoltKind.Radial, state.Points[shoulder].Position, state.Points[hip].Position,
                        LightningRules.Mix(seed, 11u), now, 0.18, 0.08f, 0.24f,
                        new Color(236, 251, 255, 225), new Color(86, 190, 255, 150), 1, 0f, false);
                    scene.Spawn(LightningBoltKind.Radial, state.Points[1].Position, state.Points[2].Position,
                        LightningRules.Mix(seed, 12u), now, 0.14, 0.06f, 0.20f,
                        new Color(236, 251, 255, 220), new Color(86, 190, 255, 140), 0, 0f, false);
                }

                if (Vector3.DistanceSquared(center, state.LastCenter) > 0.03f)
                {
                    scene.Spawn(LightningBoltKind.Radial, state.LastCenter, center, LightningRules.Mix(seed, 3u), now, 0.12,
                        overload ? 0.09f : 0.05f, overload ? 0.26f : 0.15f,
                        new Color(200, 236, 255, 180), new Color(60, 150, 255, 120), 0, 0f, false);
                }

                state.NextArc = now + interval;
                state.LastCenter = center;
            }

            if (overload && now >= state.NextFootstep)
            {
                state.NextFootstep = now + 0.42;
                if (Vector3.DistanceSquared(center, state.LastFootstep) > 0.16f)
                {
                    state.LastFootstep = center;
                    Vector3 foot = center - bodyMatrix.Up * 0.85f;
                    scene.SpawnRune(foot, 0.18f, 0.95f, now, 0.42f, new Color(120, 196, 255, 200));
                    scene.Spawn(LightningBoltKind.Radial, foot + bodyMatrix.Up * 0.75f, foot,
                        LightningRules.Mix((uint)(now * 311.0), player.Entity.EntityId), now, 0.14,
                        0.06f, 0.18f, new Color(220, 244, 255, 210), new Color(70, 170, 255, 130), 0, 0f, false);
                }
            }
        }

        private static void ReleaseAvatar(ComponentModel model)
        {
            AvatarGlow state;
            if (!s_avatars.TryGetValue(model, out state)) return;
            foreach (GlowPoint point in state.Points) state.Subsystem.RemoveGlowPoint(point);
            model.EmissionColor = state.OriginalEmission;
            s_avatars.Remove(model);
        }

        private static Vector3 BoneWorld(ComponentHumanModel model, int index, Camera camera, Vector3 fallback)
        {
            Matrix[] transforms = model.AbsoluteBoneTransformsForCamera;
            if (index < 0 || transforms == null || index >= transforms.Length) return fallback;
            return Vector3.Transform(transforms[index].Translation, camera.InvertedViewMatrix);
        }
    }
}
