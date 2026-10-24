using System;
using System.Collections.Generic;
using Engine;
using Game.NetWork;

namespace SCIEPort.Features.Vehicles
{
    /// <summary>
    /// Process-wide authorization state for the SCIE-Port vehicle activation gate.
    /// A "session" is one real player's own control of a vehicle. Every machine has
    /// exactly one local operating player, so the local session is keyed by the
    /// all-zeros GUID. When a client asks to operate, the request is routed to the
    /// host (<see cref="VehicleActivationPackage"/>) which is the only authority;
    /// the host records each remote player separately by its network GUID. The
    /// local grant on a client therefore flips to true only after the host replies,
    /// which is what makes host verification meaningful rather than a client-side
    /// self-check.
    ///
    /// All UI, timing and network surfaces used here were verified against the
    /// x25.01.17 assemblies: <c>Engine.Input.Keyboard.ShowKeyboard(string,string,
    /// string,bool,Action&lt;string&gt;,Action)</c> (the same native on-screen text
    /// entry the vanilla sign editor uses, with <c>passwordMode</c> masking),
    /// <c>Engine.Time.RealTime</c> (static double seconds) and <c>Client.GUID</c>.
    /// The popup is wrapped in try/catch because its 7723 behaviour is still
    /// unverified; a thrown exception leaves the session denied (fail-safe) rather
    /// than crash the drive/spawn loop.
    /// </summary>
    public static class VehicleActivationService
    {
        private static readonly Guid LocalKey = Guid.Empty;
        private static readonly Dictionary<Guid, bool> s_grants = new Dictionary<Guid, bool>();
        private static readonly Dictionary<Guid, double> s_lastPrompt = new Dictionary<Guid, double>();

        /// <summary>True when the given session key has already been authorized.</summary>
        public static bool IsGranted(Guid key)
        {
            bool granted;
            return s_grants.TryGetValue(key, out granted) && granted;
        }

        /// <summary>True when this machine's own player has been authorized.</summary>
        public static bool IsLocalGranted()
        {
            return IsGranted(LocalKey);
        }

        /// <summary>Marks a session authorized for the rest of the process.</summary>
        public static void Grant(Guid key)
        {
            s_grants[key] = true;
        }

        /// <summary>
        /// Gate used at every vehicle entry point (spawn, drive, ride, fire). Returns
        /// true only when the local session is already authorized. When it is not, a
        /// throttled password prompt is attempted once per <see
        /// cref="VehicleActivationRules.PromptInterval"/> seconds and the call returns
        /// false so the caller rejects the operation without consuming any item or
        /// applying any control.
        /// </summary>
        public static bool EnsureActivated()
        {
            if (IsLocalGranted())
            {
                return true;
            }

            TryPrompt();
            return false;
        }

        private static void TryPrompt()
        {
            double now = Engine.Time.RealTime;
            double last;
            if (!s_lastPrompt.TryGetValue(LocalKey, out last))
            {
                last = -1.0;
            }

            if (!VehicleActivationRules.ShouldPrompt(now, last))
            {
                return;
            }

            s_lastPrompt[LocalKey] = now;

            try
            {
                Engine.Input.Keyboard.ShowKeyboard(
                    "载具激活",
                    "请输入 6 位激活密码以解锁 SCIE 载具。",
                    string.Empty,
                    true,
                    OnCodeEntered,
                    OnPromptCancelled);
            }
            catch (Exception exception)
            {
                // The native keyboard surface compiles against x25.01.17 but its
                // behaviour on the 7723 Android client is not yet observed. Never let a
                // popup failure crash the update loop; stay denied and retry after the
                // throttle window.
                Log.Error("[SCIEPort] Vehicle activation prompt failed: " + exception);
            }
        }

        private static void OnCodeEntered(string code)
        {
            if (CommonLib.WorkType == WorkType.Client)
            {
                // A client has no authority to grant itself. Forward the entered code to
                // the host; the local session unlocks only when the host's affirmative
                // reply arrives (see VehicleActivationPackage.Handle).
                if (CommonLib.Net != null)
                {
                    CommonLib.Net.QueuePackage(new VehicleActivationPackage(code, isRequest: true));
                }

                return;
            }

            // Local or host process: this player is the authority for their own session.
            if (VehicleActivationRules.ShouldGrantActivation(VehicleActivationRules.IsValidCode(code)))
            {
                Grant(LocalKey);
                Log.Information("[SCIEPort] Vehicle activation granted for the local session.");
            }
        }

        private static void OnPromptCancelled()
        {
            // Cancelling intentionally does nothing: the session stays denied so the
            // caller continues to reject the operation without consuming items.
        }
    }
}
