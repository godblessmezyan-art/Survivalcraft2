namespace BuilderAssistant.Core.Safety
{
    /// <summary>
    /// Game-free mirror of the three SCNET <c>Game.NetWork.WorkType</c> roles.
    /// WorldEditRuntime maps the live game enum into this one so the
    /// "who may write terrain" decision can be unit-tested offline, without
    /// referencing the game assemblies.
    /// </summary>
    internal enum WorldAuthority
    {
        /// <summary>This device owns and runs the world alone (single player).</summary>
        SinglePlayer,

        /// <summary>This device is the authoritative host of a multiplayer world.</summary>
        Host,

        /// <summary>This device is a connected, non-authoritative remote client.</summary>
        RemoteClient,
    }

    /// <summary>
    /// Decides whether the current network role is allowed to mutate terrain at
    /// all. Only the world-authoritative roles (single player and host) may do
    /// so; a remote client must never attempt local terrain writes, because its
    /// changes are neither authoritative nor guaranteed to sync to the host.
    /// This is deliberately independent of any mod presence on other peers: the
    /// host writing vanilla blocks is what makes generated buildings visible to
    /// non-mod clients, so nothing here requires the peers to share the mod.
    /// </summary>
    internal static class TerrainAuthority
    {
        /// <summary>True when terrain writes are permitted for this role.</summary>
        public static bool IsWritable(WorldAuthority authority)
        {
            return authority == WorldAuthority.SinglePlayer
                || authority == WorldAuthority.Host;
        }
    }
}
