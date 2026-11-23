using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>
    /// Authority-side combat abilities for the pyramid guardians. Bonus melee power is added inside
    /// the native AttackBody hook on every peer so the damage number stays identical; the snare stun,
    /// armour-bypassing curse ticks and the delayed sun verdict only run on Local/Server authority.
    /// </summary>
    internal static class PyramidGuardianSkills
    {
        internal const string Mummy = "SCIEPortMummy", Pharaoh = "SCIEPortPharaoh";
        // Passive melee bonus applied to every native hit.
        internal const float MummyMeleeBonus = 4f, PharaohMeleeBonus = 6f;
        // Shroud snare: extra root on top of the native stun.
        internal const float SnareStun = .7f; internal const double SnareCooldown = 8;
        // Curse: three armour-bypassing ticks.
        internal const float CursePower = 7f; internal const int CurseTicks = 3;
        internal const double CurseInterval = 2, CurseCooldown = 10;
        // Sun verdict: one delayed area burst, full power at the centre and half at the rim.
        internal const float VerdictPower = 25f, VerdictEdge = 10f, VerdictRadius = 4f;
        internal const double VerdictDelay = .8, VerdictCooldown = 14;
        // Undead command aura: nearby mummies hit harder.
        internal const float AuraBonus = .2f, AuraRadius = 12f;

        private sealed class Curse
        {
            internal ComponentPlayer Target;
            internal ComponentCreature Caster;
            internal int Ticks;
            internal double Next;
        }
        private struct Verdict
        {
            internal ComponentCreature Caster;
            internal Vector3 Center;
            internal double At;
        }

        private static readonly Dictionary<int, double> snareReady = new Dictionary<int, double>();
        private static readonly Dictionary<int, double> curseReady = new Dictionary<int, double>();
        private static readonly Dictionary<int, double> verdictReady = new Dictionary<int, double>();
        private static readonly Dictionary<int, Curse> curses = new Dictionary<int, Curse>();
        private static readonly List<Verdict> verdicts = new List<Verdict>();
        private static readonly Dictionary<int, ComponentMiner> mummies = new Dictionary<int, ComponentMiner>();
        private static readonly Dictionary<int, ComponentCreature> pharaohs = new Dictionary<int, ComponentCreature>();
        private static readonly Dictionary<int, float> auraDelta = new Dictionary<int, float>();
        private static readonly List<int> stale = new List<int>();
        private static readonly List<Vector3> auraCenters = new List<Vector3>();
        private static double nextScan;

        /// <summary>Native melee hook. Only guardians hitting a player are affected.</summary>
        internal static void OnAttackBody(ComponentBody target, ComponentCreature attacker, ref float attackPower, bool isMeleeAttack)
        {
            if (!isMeleeAttack || attackPower <= 0f || attacker == null || target == null || target.Player == null) return;
            string name = attacker.Entity?.ValuesDictionary?.DatabaseObject?.Name;
            if (name == Mummy)
            {
                attackPower += MummyMeleeBonus;
                if (CommonLib.WorkType != WorkType.Client) TriggerSnare(attacker, target.Player);
            }
            else if (name == Pharaoh)
            {
                attackPower += PharaohMeleeBonus;
                if (CommonLib.WorkType != WorkType.Client) TriggerPharaoh(attacker, target.Player);
            }
        }

        /// <summary>Called once per loader update from the authoritative side.</summary>
        internal static void Update()
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            Project project = GameManager.Project;
            if (project == null) return;
            double now = Now(project);
            TickCurses(now);
            TickVerdicts(project, now);
            if (now < nextScan) return;
            nextScan = now + .5;
            Scan();
        }

        private static void Register(ComponentCreature attacker, string name)
        {
            int id = attacker.Entity.EntityId;
            if (name == Mummy)
            {
                if (!mummies.ContainsKey(id))
                {
                    ComponentMiner miner = attacker.Entity.FindComponent<ComponentMiner>(false);
                    if (miner != null) mummies[id] = miner;
                }
            }
            else if (!pharaohs.ContainsKey(id)) pharaohs[id] = attacker;
        }

        private static void TriggerSnare(ComponentCreature attacker, ComponentPlayer player)
        {
            double now = Now(attacker.Project);
            int id = attacker.Entity.EntityId;
            Register(attacker, Mummy);
            if (snareReady.TryGetValue(id, out double ready) && now < ready) return;
            snareReady[id] = now + SnareCooldown;
            ComponentLocomotion move = player.ComponentLocomotion;
            if (move != null) move.StunTime = MathUtils.Max(move.StunTime, SnareStun);
        }

        private static void TriggerPharaoh(ComponentCreature attacker, ComponentPlayer player)
        {
            double now = Now(attacker.Project);
            int id = attacker.Entity.EntityId;
            Register(attacker, Pharaoh);
            if (!curseReady.TryGetValue(id, out double ready) || now >= ready)
            {
                curseReady[id] = now + CurseCooldown;
                curses[player.Entity.EntityId] = new Curse { Target = player, Caster = attacker, Ticks = CurseTicks, Next = now + CurseInterval };
            }
            if (!verdictReady.TryGetValue(id, out ready) || now >= ready)
            {
                verdictReady[id] = now + VerdictCooldown;
                verdicts.Add(new Verdict { Caster = attacker, Center = player.ComponentBody.BoundingBox.Center(), At = now + VerdictDelay });
                if (verdicts.Count > 6) verdicts.RemoveAt(0);
            }
        }

        private static void TickCurses(double now)
        {
            if (curses.Count == 0) return;
            stale.Clear();
            foreach (KeyValuePair<int, Curse> pair in curses)
            {
                Curse curse = pair.Value;
                if (curse.Target == null || !curse.Target.IsAddedToProject || curse.Target.ComponentHealth.Health <= 0f) { stale.Add(pair.Key); continue; }
                ComponentHealth health = curse.Target.ComponentHealth;
                while (curse.Ticks > 0 && now >= curse.Next)
                {
                    curse.Ticks--;
                    curse.Next += CurseInterval;
                    if (health.Health <= 0f) break;
                    ComponentCreature caster = curse.Caster != null && curse.Caster.IsAddedToProject ? curse.Caster : null;
                    health.Injure(CursePower / MathUtils.Max(health.AttackResilience, .001f), caster, false, "法老诅咒");
                }
                if (curse.Ticks <= 0) stale.Add(pair.Key);
            }
            foreach (int id in stale) curses.Remove(id);
        }

        private static void TickVerdicts(Project project, double now)
        {
            if (verdicts.Count == 0) return;
            for (int i = verdicts.Count - 1; i >= 0; i--)
            {
                Verdict verdict = verdicts[i];
                if (now < verdict.At) continue;
                verdicts.RemoveAt(i);
                if (verdict.Caster == null || !verdict.Caster.IsAddedToProject || verdict.Caster.ComponentHealth.Health <= 0f) continue;
                SubsystemPlayers players = project.FindSubsystem<SubsystemPlayers>(false);
                if (players == null) continue;
                foreach (ComponentPlayer player in players.ComponentPlayers)
                {
                    if (player == null || !player.IsAddedToProject || player.ComponentHealth.Health <= 0f) continue;
                    Vector3 center = player.ComponentBody.BoundingBox.Center();
                    float distance = Vector3.Distance(verdict.Center, center);
                    if (distance > VerdictRadius) continue;
                    float power = MathUtils.Lerp(VerdictPower, VerdictEdge, MathUtils.Saturate(distance / VerdictRadius));
                    ComponentMiner.AttackBody(player.ComponentBody, verdict.Caster, center, Vector3.UnitY, power, false);
                }
            }
        }

        private static void Scan()
        {
            if (mummies.Count == 0 || pharaohs.Count == 0) return;
            auraCenters.Clear();
            stale.Clear();
            foreach (KeyValuePair<int, ComponentCreature> pair in pharaohs)
            {
                ComponentCreature pharaoh = pair.Value;
                if (pharaoh == null || !pharaoh.IsAddedToProject || pharaoh.ComponentHealth.Health <= 0f) { stale.Add(pair.Key); continue; }
                auraCenters.Add(pharaoh.ComponentBody.BoundingBox.Center());
            }
            foreach (int id in stale) pharaohs.Remove(id);
            stale.Clear();
            foreach (KeyValuePair<int, ComponentMiner> pair in mummies)
            {
                ComponentMiner miner = pair.Value;
                ComponentCreature mummy = miner?.ComponentCreature;
                if (miner == null || mummy == null || !mummy.IsAddedToProject || mummy.ComponentHealth.Health <= 0f)
                {
                    if (miner != null && auraDelta.TryGetValue(pair.Key, out float leftover)) { miner.AttackPower -= leftover; auraDelta.Remove(pair.Key); }
                    stale.Add(pair.Key); continue;
                }
                if (auraDelta.TryGetValue(pair.Key, out float old)) { miner.AttackPower -= old; auraDelta[pair.Key] = 0f; }
                Vector3 position = mummy.ComponentBody.BoundingBox.Center();
                bool inside = false;
                foreach (Vector3 center in auraCenters)
                    if (Vector3.DistanceSquared(position, center) <= AuraRadius * AuraRadius) { inside = true; break; }
                if (inside)
                {
                    float delta = miner.AttackPower * AuraBonus;
                    miner.AttackPower += delta;
                    auraDelta[pair.Key] = delta;
                }
            }
            foreach (int id in stale) mummies.Remove(id);
            stale.Clear();
            if (snareReady.Count + curseReady.Count + verdictReady.Count > 64) { snareReady.Clear(); curseReady.Clear(); verdictReady.Clear(); }
            for (int i = verdicts.Count - 1; i >= 0; i--)
                if (verdicts[i].Caster == null || !verdicts[i].Caster.IsAddedToProject) verdicts.RemoveAt(i);
        }

        private static double Now(Project project) => project?.FindSubsystem<SubsystemTime>(false)?.GameTime ?? 0;
    }
}
