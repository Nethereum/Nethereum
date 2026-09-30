using System;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Serving;

namespace Nethereum.DevChain.Hosting
{
    public sealed class DevChainComposedNode : IAsyncDisposable
    {
        public required DevChainNode Node { get; init; }

        public required IChainStoreBundle Bundle { get; init; }

        public required IChainProfile Profile { get; init; }

        public required ChainNodeMempool Mempool { get; init; }

        public PeerListener Listener { get; init; }

        public ChainNodeSyncStack Sync { get; init; }

        public async ValueTask DisposeAsync()
        {
            Mempool?.Dispose();
            if (Sync != null) await Sync.DisposeAsync().ConfigureAwait(false);
            if (Listener != null) await Listener.DisposeAsync().ConfigureAwait(false);
        }
    }
}
