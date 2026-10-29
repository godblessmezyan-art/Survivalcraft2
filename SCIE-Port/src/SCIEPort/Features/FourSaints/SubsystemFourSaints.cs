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
            internal double QinglongPulseEnd, XuanwuPulseEnd, NextXuanwuPulse;
            internal float JumpDelta, WalkDelta;
            internal readonly float[] RepairCredit = new float[4];
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

        internal bool TryGetVisualState(ComponentPlayer player, out float qinglong, out float xuanwu, out float baihu, out float zhuque)
        {
            qinglong = xuanwu = baihu = zhuque = 0f;
            State state;
            if (player == null || !m_states.TryGetValue(player.Entity.EntityId, out state)) return false;
            qinglong = MathUtils.Saturate((float)((state.QinglongPulseEnd - Now) / FourSaintsVisualRules.QinglongPulseSeconds));
            xuanwu = MathUtils.Saturate((float)((state.XuanwuPulseEnd - Now) / FourSaintsVisualRules.XuanwuPulseSeconds));
            baihu = MathUtils.Saturate((float)(state.AwakenEnd - Now));
            zhuque = MathUtils.Saturate((float)((state.RebirthEnd - Now) / FourSaintsRules.ZhuqueLockDuration));
            return qinglong > 0f || xuanwu > 0f || baihu > 0f || zhuque > 0f;
        }

        internal void RefreshLocalHealthBar(ComponentPlayer player)
        {
            if (player == null || player.ComponentGui?.HealthBarWidget == null) return;
            State state = Get(player);
            byte equipment = Equipment(player);
            float maximum = FourSaintsRules.MaximumHealth(equipment);
            player.ComponentGui.HealthBarWidget.BarsCount = (equipment & 1) != 0 ? (int)(maximum * 10f) : state.OriginalHealthBars;
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
                    for (int i = 0; i < 4; i++)
                        if ((equipment & 1) == 0 || (equipment & (1 << i)) == 0) state.RepairCredit[i] = 0f;
                    state.Equipment = equipment;
                    if (gainedQinglong) state.NextHeal = Now + FourSaintsRules.QinglongHealPeriod;
                    if ((equipment & 4) == 0) state.AwakenEnd = Now;
                    state.NextSnapshot = 0;
                }
                ApplyHealth(player, state);
                ApplyZhuque(player, state);
                ApplyTemperature(player, state);
                ApplyJump(player, state);
                ApplyMovement(player, state);
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
            float maximum = FourSaintsRules.MaximumHealth(state.Equipment);
            if (Authority && qinglong && player.ComponentHealth.Health > 0f && Now >= state.NextHeal)
            {
                double periods = Math.Floor((Now - state.NextHeal) / FourSaintsRules.QinglongHealPeriod) + 1.0;
                state.NextHeal += periods * FourSaintsRules.QinglongHealPeriod;
                RepairEquipment(player, state);
                if (player.ComponentHealth.Health < maximum)
                {
                    player.ComponentHealth.Health = MathUtils.Min(maximum, player.ComponentHealth.Health + FourSaintsRules.QinglongHealAmount);
                    state.QinglongPulseEnd = Now + FourSaintsVisualRules.QinglongPulseSeconds;
                    CommonLib.Net.QueuePackage(new ComponentHealthPackage(player.ComponentHealth));
                }
            }
            if (Authority && player.ComponentHealth.Health > maximum)
            {
                player.ComponentHealth.Health = maximum;
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(player.ComponentHealth));
            }
            if (player.PlayerData.IsMainPlayer && player.ComponentGui?.HealthBarWidget != null)
            {
                player.ComponentGui.HealthBarWidget.BarsCount = qinglong ? (int)(maximum * 10f) : state.OriginalHealthBars;
                player.ComponentGui.HealthBarWidget.Value = MathUtils.Saturate(player.ComponentHealth.Health / maximum);
            }
        }

        private void ApplyZhuque(ComponentPlayer player, State state)
        {
            if ((state.Equipment & 8) == 0) return;
            ComponentOnFire fire = player.Entity.FindComponent<ComponentOnFire>(false);
            if (fire != null) { fire.Extinguish(); fire.TouchesFire = false; }
        }

        private void ApplyTemperature(ComponentPlayer player, State state)
        {
            if (!Authority) return;
            bool xuanwu = (state.Equipment & 2) != 0;
            bool zhuque = (state.Equipment & 8) != 0;
            if (!xuanwu && !zhuque) return;
            ComponentVitalStats vital = player.ComponentVitalStats;
            if (vital == null) return;
            float current = vital.Temperature;
            float clamped = FourSaintsRules.ClampTemperature(current, xuanwu, zhuque);
            if (Math.Abs(clamped - current) > 0.0001f) vital.Temperature = clamped;
        }

        private void ApplyJump(ComponentPlayer player, State state)
        {
            ComponentLocomotion locomotion = player.ComponentLocomotion;
            locomotion.JumpSpeed -= state.JumpDelta;
            state.JumpDelta = 0f;
            if ((state.Equipment & 4) == 0 || player.ComponentHealth.Health <= 0f || locomotion.IsCreativeFlyEnabled || player.ComponentRider.Mount != null) return;
            float desired = FourSaintsRules.JumpSpeedForExtraBlocks(locomotion.JumpSpeed, player.ComponentLevel.SpeedFactor, IsAwakened(player) ? 2f : 1f);
            state.JumpDelta = MathUtils.Max(0f, desired - locomotion.JumpSpeed);
            locomotion.JumpSpeed += state.JumpDelta;
        }

        private void ApplyMovement(ComponentPlayer player, State state)
        {
            ComponentLocomotion locomotion = player.ComponentLocomotion;
            locomotion.WalkSpeed -= state.WalkDelta;
            state.WalkDelta = 0f;
            if ((state.Equipment & 4) != 0 && IsAwakened(player) && player.ComponentHealth.Health > 0f)
            {
                state.WalkDelta = MathUtils.Max(0f, locomotion.WalkSpeed) * (2f / FourSaintsRules.BaihuMovementSpeedFactor - 1f);
                locomotion.WalkSpeed += state.WalkDelta;
            }
        }

        private void RepairEquipment(ComponentPlayer player, State state)
        {
            var slots = new[] { ClothingSlot.Head, ClothingSlot.Torso, ClothingSlot.Legs, ClothingSlot.Feet };
            Block block = BlocksManager.Blocks[ClothingBlock.Index];
            int levels = block.Durability + 1;
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var values = new List<int>(player.ComponentClothing.GetClothes(slots[slotIndex]));
                bool changed = false, found = false;
                for (int i = 0; i < values.Count; i++)
                {
                    int value = values[i];
                    if (Terrain.ExtractContents(value) != ClothingBlock.Index) continue;
                    int index = ClothingBlock.GetClothingIndex(Terrain.ExtractData(value));
                    if (index != FourSaintsRules.QinglongIndex + slotIndex) continue;
                    found = true;
                    int damage = block.GetDamage(value);
                    if (damage <= 0) { state.RepairCredit[slotIndex] = 0f; continue; }
                    state.RepairCredit[slotIndex] += FourSaintsRules.RepairPerPeriod;
                    int steps = FourSaintsRules.RepairSteps(state.RepairCredit[slotIndex], FourSaintsRules.Sturdiness(index), levels, damage);
                    if (steps > 0)
                    {
                        state.RepairCredit[slotIndex] -= steps * FourSaintsRules.Sturdiness(index) / levels;
                        values[i] = block.SetDamage(value, damage - steps);
                        if (damage == steps) state.RepairCredit[slotIndex] = 0f;
                        changed = true;
                    }
                    break;
                }
                if (!found) state.RepairCredit[slotIndex] = 0f;
                if (changed) { player.ComponentClothing.SetClothes(slots[slotIndex], values); player.ComponentClothing.OnSlotChange((int)slots[slotIndex]); }
            }
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
            if (FourSaintsRules.IsXuanwuEnvironmentalImmunity(
                xuanwu,
                cause,
                LanguageControl.Get(ComponentHealth.fName, 7),
                LanguageControl.Get(ComponentVitalStats.fName, 25),
                LanguageControl.Get(ComponentVitalStats.fName, 26)))
            {
                PulseXuanwu(victimState);
                return true;
            }
            if (zhuque && (cause == LanguageControl.Get(ComponentHealth.fName, 1) || cause == LanguageControl.Get(ComponentHealth.fName, 5))) return true;
            if (Now < victimState.RebirthEnd && !ignoreInvulnerability) return true;

            float adjusted = FourSaintsRules.EffectiveDamage(amount, false, xuanwu && attacker != null);
            if (zhuque && !ignoreInvulnerability && adjusted > 0f && health.Health - adjusted <= FourSaintsRules.NativeHealthPerPoint)
            {
                health.Health = MathUtils.Min(qinglong ? FourSaintsRules.QinglongMaximumHealth : FourSaintsRules.NativeMaximumHealth, 1f);
                victimState.RebirthEnd = Now + FourSaintsRules.ZhuqueLockDuration;
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(health));
                Publish(victimState);
                return true;
            }
            if (xuanwu && attacker != null && adjusted < amount) PulseXuanwu(victimState);
            if (Math.Abs(adjusted - amount) < .00001f) return false;
            if (adjusted <= 0f) return true;
            try { s_replayingInjury = true; health.Injure(adjusted, attacker, ignoreInvulnerability, cause); }
            finally { s_replayingInjury = false; }
            return true;
        }

        private void PulseXuanwu(State state)
        {
            if (Now < state.NextXuanwuPulse) return;
            state.XuanwuPulseEnd = Now + FourSaintsVisualRules.XuanwuPulseSeconds;
            state.NextXuanwuPulse = Now + FourSaintsVisualRules.XuanwuThrottleSeconds;
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
            state.Player.ComponentLocomotion.WalkSpeed -= state.WalkDelta;
            if (state.Player.ComponentGui?.HealthBarWidget != null) state.Player.ComponentGui.HealthBarWidget.BarsCount = state.OriginalHealthBars;
        }

        public override void Dispose()
        {
            foreach (State state in m_states.Values) Restore(state);
            m_states.Clear(); FourSaintsHud.Clear(); FourSaintsVisuals.Reset(Project); base.Dispose();
        }
    }
}
#pragma warning restore 1591
