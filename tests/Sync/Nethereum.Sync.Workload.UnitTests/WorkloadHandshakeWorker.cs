using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.Chain.TestData.UnitTests
{
    public sealed class WorkloadHandshakeWorker : IPeerHandshakeWorker
    {
        private readonly byte[] _genesisHash;
        private readonly ulong _networkId;
        private readonly bool _advertiseSnap2;

        public WorkloadHandshakeWorker(byte[] genesisHash, ulong networkId, bool advertiseSnap2 = false)
        {
            _genesisHash = genesisHash;
            _networkId = networkId;
            _advertiseSnap2 = advertiseSnap2;
        }

        public async Task<IEthPeer> HandshakeAsync(string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            => await SyncPeerSession.ConnectAsync(enode, timeout, ct, _genesisHash, _networkId, minPeerLatestBlock,
                advertiseSnap2: _advertiseSnap2);
    }
}
