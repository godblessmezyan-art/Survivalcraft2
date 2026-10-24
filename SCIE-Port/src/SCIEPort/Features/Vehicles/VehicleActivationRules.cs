using System;

namespace SCIEPort.Features.Vehicles
{
    /// <summary>
    /// Pure activation decision rules for the SCIE-Port vehicle activation gate.
    /// Deliberately game-independent so the net8 offline test project can link
    /// this source without any x25.01.17 assembly reference.
    /// </summary>
    public static class VehicleActivationRules
    {
        /// <summary>Unified activation code shared by host and every client session.</summary>
        public const string ActivationCode = "123456";

        /// <summary>Minimum seconds between activation popups or failure hints for one player.</summary>
        public const double PromptInterval = 6.0;

        /// <summary>True when the entered text matches the activation code after trimming whitespace.</summary>
        public static bool IsValidCode(string entered)
        {
            if (entered == null)
            {
                return false;
            }

            return string.Equals(
                entered.Trim(),
                ActivationCode,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// True when a popup or hint may be shown: nothing shown yet, or the previous
        /// interaction is older than <see cref="PromptInterval"/> seconds. Guards the
        /// drive loop, which runs every frame, against dialog and message spam.
        /// </summary>
        public static bool ShouldPrompt(double now, double lastPromptTime)
        {
            if (lastPromptTime < 0.0)
            {
                return true;
            }

            return now - lastPromptTime >= PromptInterval;
        }

        /// <summary>
        /// A session is keyed by the real player's GUID. The all-zeros GUID is the
        /// documented fallback for the local (single-player) player, which has no
        /// network client identity but still needs one activation session.
        /// </summary>
        public static bool IsUsableSessionKey(Guid playerGuid)
        {
            return true;
        }

        /// <summary>
        /// The host accepts a client activation request only when the carried code
        /// is valid; the reply then unlocks exactly the requesting player's session.
        /// </summary>
        public static bool ShouldGrantActivation(bool codeValid)
        {
            return codeValid;
        }

        /// <summary>
        /// Authority-side gate: a player may operate (spawn, drive, ride, fire) only
        /// after their activation session has been granted, by local entry or by a
        /// host-verified reply.
        /// </summary>
        public static bool CanOperate(bool isGranted)
        {
            return isGranted;
        }
    }
}
