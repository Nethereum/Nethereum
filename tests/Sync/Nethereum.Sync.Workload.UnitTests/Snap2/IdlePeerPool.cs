using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;

namespace Nethereum.Chain.TestData.UnitTests
{
    internal sealed class IdlePeerPool : IPeerPool
    {
        public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();

        public int TargetPeerCount => 0;

        public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }

        public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;

        public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;

        public void ReportSuccess(Guid peerId) { }

        public Task ClearAllBansAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
