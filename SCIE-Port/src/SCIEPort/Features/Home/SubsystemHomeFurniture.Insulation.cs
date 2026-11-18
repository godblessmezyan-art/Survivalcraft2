using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Home
{
    public sealed partial class SubsystemHomeFurniture
    {
        private SubsystemUpdate bedUpdates;
        private BedInsulationUpdate bedInsulation;
        // The native vital-stat component runs at Default. Run before it, including
        // each accelerated sleep substep; the existing loader callback runs after it.
        private sealed class BedInsulationUpdate : IUpdateable
        {
            private readonly SubsystemHomeFurniture home;
            internal BedInsulationUpdate(SubsystemHomeFurniture home) { this.home = home; }
            public UpdateOrder UpdateOrder => UpdateOrder.Input;
            public void Update(float dt) { home.UpdateBedInsulation(); }
        }
        private bool IsBedSleeper(ComponentPlayer player)
        {
            if (player == null || !player.IsAddedToProject || player.Project != Project
                || player.ComponentSleep?.IsSleeping != true || player.ComponentHealth == null || player.ComponentHealth.Health <= 0
                || player.ComponentVitalStats == null || player.ComponentBody == null) return false;
            Vector3 position = player.ComponentBody.Position;
            if (!HomeSeatRules.Finite(position)) return false;
            Point3 cell = new Point3(Terrain.ToCell(position.X), Terrain.ToCell(position.Y - .1f), Terrain.ToCell(position.Z));
            if (!Ready(cell) || position.Y < cell.Y + .5f || position.Y > cell.Y + 1.15f) return false;
            int value = Value(cell), id = Terrain.ExtractContents(value);
            if (id == HomeRules.Bed) return true;
            Point3 root;
            return id == HomeRules.BedPart && TryFootprintRoot(cell, value, out root);
        }
        private static void BedComfort(ComponentPlayer player)
        {
            var vital = player.ComponentVitalStats;
            vital.Temperature = 12f;
            vital.m_lastTemperature = 12f;
            vital.m_temperatureBlackoutDuration = 0;
            vital.m_temperatureBlackoutFactor = 0;
            if (player.ComponentScreenOverlays != null) player.ComponentScreenOverlays.IceFactor = 0;
            // Keep native environment temperature, clothing and wetness untouched.
            // Waking or leaving the bed immediately resumes natural heat exchange.
        }
        internal void UpdateBedInsulation()
        {
            var players = Project.FindSubsystem<SubsystemPlayers>(false);
            if (players == null) return;
            foreach (var player in players.ComponentPlayers)
                if ((CommonLib.WorkType != WorkType.Client || player.PlayerData?.IsMainPlayer == true) && IsBedSleeper(player)) BedComfort(player);
        }
        internal bool ProtectBedTemperature(ComponentHealth health, ComponentCreature attacker, bool ignoreInvulnerability, string cause)
        {
            if (attacker != null || ignoreInvulnerability || string.IsNullOrEmpty(cause)) return false;
            var player = health?.Entity.FindComponent<ComponentPlayer>(false);
            if (!IsBedSleeper(player)) return false;
            // Only the three exact native cold/heat injury causes. Fire, lava,
            // attacks, drowning and other hazards continue through the normal hooks.
            if (cause != LanguageControl.Get(ComponentVitalStats.fName, 25)
                && cause != LanguageControl.Get(ComponentVitalStats.fName, 26)
                && cause != LanguageControl.Get(ComponentVitalStats.fName, 35)) return false;
            BedComfort(player);
            return true;
        }
    }
}
