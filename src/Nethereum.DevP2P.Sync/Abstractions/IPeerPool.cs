using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IPeerPool : IAsyncDisposable
    {
        IReadOnlyCollection<IEthPeer> ActivePeers { get; }

        IReadOnlyList<IEthPeer> ActivePeersByPreference
            => ActivePeers.OrderByDescending(p => p.IsTrusted).ToList();

        bool IsPeerActive(Guid peerId)
        {
            foreach (var peer in ActivePeers)
            {
                if (peer.Id == peerId) return true;
            }
            return false;
        }

        bool EnqueueCandidate(string enode) => false;

        int TargetPeerCount { get; }

        event EventHandler<IEthPeer> PeerAdded;

        event EventHandler<IEthPeer> PeerRemoved;

        Task StartAsync(CancellationToken ct);

        Task BanAndDropAsync(string enode, string reason, CancellationToken ct);

        Task DropAsync(Guid peerId, string reason, CancellationToken ct);

        void ReportSuccess(Guid peerId);

        Task ClearAllBansAsync();
    }
}
