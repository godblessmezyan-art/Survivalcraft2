namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Engine-independent equipment bonuses for the true Mjolnir.</summary>
    public static class StormhammerEquipmentRules
    {
        /// <summary>Equipped ground movement multiplier.</summary>
        public const float MovementFactor = 1.2f;
        /// <summary>Equipped native melee frequency multiplier.</summary>
        public const float AttackSpeedFactor = 1.1f;
        /// <summary>x25.01.17 ComponentMiner native melee gate, in seconds.</summary>
        public const double NativeAttackInterval = 0.6600000262260437;
        /// <summary>Resulting interval after applying the attack-speed bonus.</summary>
        public static double AttackInterval => NativeAttackInterval / AttackSpeedFactor;
        /// <summary>Timestamp adjustment applied once after an accepted melee attempt.</summary>
        public static double LastHitTimeAdvance => NativeAttackInterval - AttackInterval;
    }
}
