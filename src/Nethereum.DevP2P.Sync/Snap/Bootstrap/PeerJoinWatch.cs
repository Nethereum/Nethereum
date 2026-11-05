using System;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class PeerJoinWatch : IDisposable
    {
        private readonly IPeerPool? _pool;
        private readonly IFetchRequestScheduler? _scheduler;
        private readonly TaskCompletionSource<IEthPeer> _joined =
            new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);

        public PeerJoinWatch(IPeerPool? pool, IFetchRequestScheduler? scheduler)
        {
            _pool = pool;
            _scheduler = scheduler;
            if (_pool != null) _pool.PeerAdded += OnPeerAdded;
        }

        public Task<IEthPeer> Joined => _joined.Task;

        private void OnPeerAdded(object? sender, IEthPeer peer)
        {
            if (_scheduler == null || _scheduler.IsSnapStateServing(peer))
                _joined.TrySetResult(peer);
        }

        public void Dispose()
        {
            if (_pool != null) _pool.PeerAdded -= OnPeerAdded;
        }
    }
}
