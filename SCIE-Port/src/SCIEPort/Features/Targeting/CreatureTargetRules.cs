using System;

namespace SCIEPort.Features.Targeting
{
    /// <summary>
    /// Engine-independent hostile-target policy shared by world-wide artifact skills.
    /// The native engine has no single "is enemy" flag: <c>ComponentCreature.Category</c> is the
    /// only authoritative classification, so the mapping from category to hostility lives here and
    /// is unit-tested without loading a game assembly.
    /// </summary>
    internal static class CreatureTargetRules
    {
        /// <summary>Native <c>CreatureCategory</c> bits mirrored so rules stay engine-free.</summary>
        internal const int LandPredator = 1, LandOther = 2, WaterPredator = 4, WaterOther = 8, Bird = 16;

        /// <summary>
        /// A valid hostile target: a living, loaded predator that is neither the caster, another
        /// player, nor a tamed/passive animal. Neutral animals and livestock are never included.
        /// </summary>
        /// <summary>Automatic area scans never touch a chunk that is not loaded.</summary>
        internal static bool AcceptsTarget(int category, bool isPlayer, bool isSelf, bool dead, bool loaded, bool friendlyFire)
            => loaded && IsHostile(category, isPlayer, isSelf, dead, friendlyFire);

        internal static bool IsHostile(int category, bool isPlayer, bool isSelf, bool dead, bool friendlyFire)
        {
            if (isSelf || dead) return false;
            if (isPlayer) return friendlyFire;
            if (category < 0) return false;
            // Only true predators are enemies. LandOther/WaterOther/Bird cover livestock, tamed
            // animals and ambient wildlife, which must never be hit by an automatic world scan.
            return (category & (LandPredator | WaterPredator)) != 0
                && (category & (LandOther | WaterOther)) == 0;
        }
    }
}
