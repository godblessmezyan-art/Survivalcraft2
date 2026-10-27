using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using SCIEPort.Features.Stormhammer;
using TemplatesDatabase;
#pragma warning disable 1591

namespace SCIEPort.Features.FourSaints
{
    /// <summary>World-scoped, authoritative runtime for the four independent divine clothes.</summary>
    public sealed class SubsystemFourSaints : SubsystemBlockBehavior, IUpdateable
    {
        internal sealed class State
        {
            internal ComponentPlayer Player;
            internal ushort RequestSequence, SentSequence;
            internal uint Revision, ReceivedRevision;
            internal byte Equipment;
            internal double NextHeal, AwakenEnd, CooldownEnd, RebirthEnd, NextSnapshot;
            internal float JumpDelta;
            internal int OriginalHealthBars = 10;
        }

        [ThreadStatic] private static bool s_replayingInjury;
        private readonly Dictionary<ushort, State> m_states = new Dictionary<ushort, State>();
        private readonly List<ushort> m_remove = new List<ushort>();
        private SubsystemPlayers m_players;
        private SubsystemTime m_time;
        private bool Authority => CommonLib.WorkType != WorkType.Client;
        internal double Now => m_time.GameTime;
        public override int[] HandledBlocks => new int[0];
        public UpdateOrder UpdateOrder => UpdateOrder.Locomotion;

        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
        }

        internal static bool Wears(ComponentPlayer player, ClothingSlot slot, int clothingIndex)
        {
            if (player == null || !player.IsAddedToProject || player.ComponentClothing == null) return false;
            foreach (int value in player.ComponentClothing.GetClothes(slot))
                if (Terrain.ExtractContents(value) == ClothingBlock.Index && ClothingBlock.GetClothingIndex(Terrain.ExtractData(value)) == clothingIndex) return true;
            return false;
        }

        internal static byte Equipment(ComponentPlayer player)
        {
            byte mask = 0;
            if (Wears(player, ClothingSlot.Head, FourSaintsRules.QinglongIndex)) mask |= 1;
            if (Wears(player, ClothingSlot.Torso, FourSaintsRules.XuanwuIndex)) mask |= 2;
            if (Wears(player, ClothingSlot.Legs, FourSaintsRules.BaihuIndex)) mask |= 4;
            if (Wears(player, ClothingSlot.Feet, FourSaintsRules.ZhuqueIndex)) mask |= 8;
            return mask;
        }

        private State Get(ComponentPlayer player)
        {
            State state;
            if (!m_states.TryGetValue(player.Entity.EntityId, out state))
            {
                state = new State { Player = player, Equipment = Equipment(player), NextHeal = Now + FourSaintsRules.QinglongHealPeriod };
                if (player.ComponentGui?.HealthBarWidget != null) state.OriginalHealthBars = player.ComponentGui.HealthBarWidget.BarsCount;
                m_states.Add(player.Entity.EntityId, state);
            }
            return state;
        }

        internal bool IsAwakened(ComponentPlayer player)
        {
            State state;
            return player != null && m_states.TryGetValue(player.Entity.EntityId, out state) && (state.Equipment & 4) != 0 && Now < state.AwakenEnd;
        }

        internal bool IsActionLocked(ComponentPlayer player)
        {
            State state;
            return player != null && m_states.TryGetValue(player.Entity.EntityId, out state) && Now < state.RebirthEnd;
        }

        internal void GetBaihuTimes(ComponentPlayer player, out float active, out float cooldown)
        {
            State state = Get(player);
            active = (float)Math.Max(0, state.AwakenEnd - Now);
            cooldown = (float)Math.Max(0, state.CooldownEnd - Now);
        }

        internal void RefreshLocalHealthBar(ComponentPlayer player)
        {
            if (player == null || player.ComponentGui?.HealthBarWidget == null) return;
            State state = Get(player);
            bool qinglong = (Equipment(player) & 1) != 0;
            float maximum = qinglong ? FourSaintsRules.QinglongMaximumHealth : FourSaintsRules.NativeMaximumHealth;
            player.ComponentGui.HealthBarWidget.BarsCount = qinglong ? 15 : state.OriginalHealthBars;
            player.ComponentGui.HealthBarWidget.Value = MathUtils.Saturate(player.ComponentHealth.Health / maximum);
        }

        internal float AttackFrequency(ComponentPlayer player)
        {
            bool baihu = Wears(player, ClothingSlot.Legs, FourSaintsRules.BaihuIndex);
            return FourSaintsRules.AttackFrequency(baihu, baihu && IsAwakened(player), SubsystemAwakenedStormhammerBlockBehavior.IsEquipped(player));
        }

        internal void RequestAwaken(ComponentPlayer player)
        {
            if (!Wears(player, ClothingSlot.Legs, FourSaintsRules.BaihuIndex) || IsActionLocked(player)) return;
            State state = Get(player);
            ushort sequence = ++state.SentSequence;
            if (Authority) AcceptAwaken(player, sequence);
            else CommonLib.Net.QueuePackage(new FourSaintsPackage { Kind = 0, PlayerId = player.Entity.EntityId, Sequence = sequence });
        }

        internal void AcceptAwaken(ComponentPlayer player, ushort sequence)
        {
            if (!Authority || player == null || !Wears(player, ClothingSlot.Legs, FourSaintsRules.BaihuIndex) || player.ComponentHealth.Health <= 0f || IsActionLocked(player)) return;
            State state = Get(player);
            if (sequence != 0 && (ushort)(sequence - state.RequestSequence) >= 0x8000) return;
            state.RequestSequence = sequence;
            if (Now < state.CooldownEnd) return;
            state.AwakenEnd = Now + FourSaintsRules.BaihuDuration;
            state.CooldownEnd = Now + FourSaintsRules.BaihuCooldown;
            Publish(state);
        }

        internal void Receive(FourSaintsPackage package)
        {
            ComponentPlayer player = Find(package.PlayerId);
            if (player == null) return;
            State state = Get(player);
            if (package.Revision <= state.ReceivedRevision) return;
            state.ReceivedRevision = package.Revision;
            state.Equipment = package.Equipment;
            state.AwakenEnd = Now + package.AwakenRemaining;
            state.CooldownEnd = Now + package.CooldownRemaining;
            state.RebirthEnd = Now + package.RebirthRemaining;
        }

        public void Update(float dt)
        {
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (player == null || !player.IsAddedToProject) continue;
                State state = Get(player);
                if (player.ComponentHealth.Health <= 0f) { state.AwakenEnd = Now; state.RebirthEnd = Now; }
                byte equipment = Equipment(player);
                if (equipment != state.Equipment)
                {
                    bool gainedQinglong = (equipment & 1) != 0 && (state.Equipment & 1) == 0;
                    state.Equipment = equipment;
                    if (gainedQinglong) state.NextHeal = Now + FourSaintsRules.QinglongHealPeriod;
                    if ((equipment & 4) == 0) state.AwakenEnd = Now;
                    state.NextSnapshot = 0;
                }
                ApplyHealth(player, state);
                ApplyZhuque(player, state);
                ApplyJump(player, state);
                ApplyLock(player, state);
                if (Authority && Now >= state.NextSnapshot)
                {
                    Publish(state);
                    state.NextSnapshot = Now + ((Now < state.AwakenEnd || Now < state.RebirthEnd) ? .1 : .5);
                }
            }
            m_remove.Clear();
            foreach (KeyValuePair<ushort, State> pair in m_states)
                if (pair.Value.Player == null || !pair.Value.Player.IsAddedToProject) m_remove.Add(pair.Key);
            foreach (ushort id in m_remove) { Restore(m_states[id]); m_states.Remove(id); }
        }

        private void ApplyHealth(ComponentPlayer player, State state)
        {
            bool qinglong = (state.Equipment & 1) != 0;
            float maximum = qinglong ? FourSaintsRules.QinglongMaximumHealth : FourSaintsRules.NativeMaximumHealth;
            if (Authority && qinglong && player.ComponentHealth.Health > 0f && Now >= state.NextHeal)
            {
                double periods = Math.Floor((Now - state.NextHeal) / FourSaintsRules.QinglongHealPeriod) + 1.0;
                state.NextHeal += periods * FourSaintsRules.QinglongHealPeriod;
                if (player.ComponentHealth.Health < maximum)
                {
                    player.ComponentHealth.Health = MathUtils.Min(maximum, player.ComponentHealth.Health + FourSaintsRules.QinglongHealAmount);
                    CommonLib.Net.QueuePackage(new ComponentHealthPackage(player.ComponentHealth));
                }
            }
            if (Authority && !qinglong && player.ComponentHealth.Health > FourSaintsRules.NativeMaximumHealth)
            {
                player.ComponentHealth.Health = FourSaintsRules.NativeMaximumHealth;
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(player.ComponentHealth));
            }
            if (player.PlayerData.IsMainPlayer && player.ComponentGui?.HealthBarWidget != null)
            {
                player.ComponentGui.HealthBarWidget.BarsCount = qinglong ? 15 : state.OriginalHealthBars;
                player.ComponentGui.HealthBarWidget.Value = MathUtils.Saturate(player.ComponentHealth.Health / maximum);
            }
        }

        private void ApplyZhuque(ComponentPlayer player, State state)
        {
            if ((state.Equipment & 8) == 0) return;
            ComponentOnFire fire = player.Entity.FindComponent<ComponentOnFire>(false);
            if (fire != null) { fire.Extinguish(); fire.TouchesFire = false; }
        }

        private void ApplyJump(ComponentPlayer player, State state)
        {
            ComponentLocomotion locomotion = player.ComponentLocomotion;
            locomotion.JumpSpeed -= state.JumpDelta;
            state.JumpDelta = 0f;
            if ((state.Equipment & 4) == 0 || player.ComponentHealth.Health <= 0f || locomotion.IsCreativeFlyEnabled || player.ComponentRider.Mount != null) return;
            float desired = FourSaintsRules.JumpSpeedForOneExtraBlock(locomotion.JumpSpeed, player.ComponentLevel.SpeedFactor);
            state.JumpDelta = MathUtils.Max(0f, desired - locomotion.JumpSpeed);
            locomotion.JumpSpeed += state.JumpDelta;
        }

        private void ApplyLock(ComponentPlayer player, State state)
        {
            if (Now >= state.RebirthEnd) return;
            ComponentLocomotion locomotion = player.ComponentLocomotion;
            locomotion.WalkOrder = Vector2.Zero; locomotion.FlyOrder = Vector3.Zero; locomotion.SwimOrder = Vector3.Zero; locomotion.JumpOrder = 0f;
            Vector3 velocity = player.ComponentBody.Velocity; velocity.X = 0f; velocity.Z = 0f; player.ComponentBody.Velocity = velocity;
        }

        internal bool HandleInjury(ComponentHealth health, float amount, ComponentCreature attacker, bool ignoreInvulnerability, string cause)
        {
            if (s_replayingInjury || !Authority || health == null) return false;
            ComponentPlayer victim = health.Entity.FindComponent<ComponentPlayer>(false);
            if (victim == null) return false;
            State victimState = Get(victim);
            bool qinglong = (victimState.Equipment & 1) != 0;
            bool xuanwu = (victimState.Equipment & 2) != 0;
            bool zhuque = (victimState.Equipment & 8) != 0;
            if (qinglong && cause == LanguageControl.Get(ComponentHealth.fName, 2)) return true;
            if (xuanwu && cause == LanguageControl.Get(ComponentHealth.fName, 7)) return true;
            if (zhuque && (cause == LanguageControl.Get(ComponentHealth.fName, 1) || cause == LanguageControl.Get(ComponentHealth.fName, 5))) return true;
            if (Now < victimState.RebirthEnd && !ignoreInvulnerability) return true;

            ComponentPlayer source = attacker?.Entity.FindComponent<ComponentPlayer>(false);
            bool fireDamage = cause == LanguageControl.Get(ComponentHealth.fName, 5);
            bool boosted = source != null && !fireDamage && IsAwakened(source);
            float adjusted = FourSaintsRules.EffectiveDamage(amount, boosted, xuanwu && attacker != null);
            if (zhuque && !ignoreInvulnerability && adjusted > 0f && health.Health - adjusted <= FourSaintsRules.NativeHealthPerPoint)
            {
                health.Health = MathUtils.Min(qinglong ? FourSaintsRules.QinglongMaximumHealth : FourSaintsRules.NativeMaximumHealth, 1f);
                victimState.RebirthEnd = Now + FourSaintsRules.ZhuqueLockDuration;
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(health));
                Publish(victimState);
                return true;
            }
            if (Math.Abs(adjusted - amount) < .00001f) return false;
            if (adjusted <= 0f) return true;
            try { s_replayingInjury = true; health.Injure(adjusted, attacker, ignoreInvulnerability, cause); }
            finally { s_replayingInjury = false; }
            return true;
        }

        private void Publish(State state)
        {
            if (CommonLib.WorkType != WorkType.Server) return;
            CommonLib.Net.QueuePackage(new FourSaintsPackage
            {
                Kind = 1, PlayerId = state.Player.Entity.EntityId, Equipment = state.Equipment, Revision = ++state.Revision,
                AwakenRemaining = (float)Math.Max(0, state.AwakenEnd - Now), CooldownRemaining = (float)Math.Max(0, state.CooldownEnd - Now), RebirthRemaining = (float)Math.Max(0, state.RebirthEnd - Now)
            });
        }

        private ComponentPlayer Find(ushort id)
        {
            foreach (ComponentPlayer player in m_players.ComponentPlayers) if (player.Entity.EntityId == id) return player;
            return null;
        }

        private void Restore(State state)
        {
            if (state.Player == null || !state.Player.IsAddedToProject) return;
            state.Player.ComponentLocomotion.JumpSpeed -= state.JumpDelta;
            if (state.Player.ComponentGui?.HealthBarWidget != null) state.Player.ComponentGui.HealthBarWidget.BarsCount = state.OriginalHealthBars;
        }

        public override void Dispose()
        {
            foreach (State state in m_states.Values) Restore(state);
            m_states.Clear(); FourSaintsHud.Clear(); base.Dispose();
        }
    }
}
#pragma warning restore 1591
