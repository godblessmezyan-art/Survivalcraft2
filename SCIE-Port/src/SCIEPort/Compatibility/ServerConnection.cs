using System;
using Game.NetWork;

namespace SCIEPort.Compatibility
{
    // x25.01.17 ClientPackage.SyncList keeps the bootstrap Client in node.Server,
    // but registers a different Client (sharing its NetPeer) in node.Clients[0].
    // PackageManager.DecodePackages assigns that registered object to IPackage.From.
    internal static class ServerConnection
    {
        internal static Client Current(NetNode node)
        {
            var anchor = node?.Server;
            if (anchor == null) return null;
            Client registered;
            if (node.Clients != null && node.Clients.TryGetValue(0, out registered))
            {
                if (ReferenceEquals(registered, anchor)) return registered;
                if (registered != null && registered.ID == 0 && anchor.ID == 0
                    && anchor.Peer != null && ReferenceEquals(registered.Peer, anchor.Peer)) return registered;
                return null;
            }
            return anchor;
        }

        internal static bool IsServer(NetNode node, Client sender)
            => sender != null && ReferenceEquals(sender, Current(node));

        internal static Guid Token(NetNode node) => Current(node)?.TokenId ?? Guid.Empty;
    }
}
