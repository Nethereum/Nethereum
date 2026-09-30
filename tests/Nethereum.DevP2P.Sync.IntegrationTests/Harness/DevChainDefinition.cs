using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Signer;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public sealed class DevChainDefinition : IChainDefinition
    {
        private readonly ulong _chainId;
        private readonly EthECKey _key;
        private byte[] _genesisHash;

        public DevChainDefinition(ulong chainId, EthECKey key)
        {
            _chainId = chainId;
            _key = key;
        }

        public async Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct) =>
            _genesisHash = await DevChainGenesis.EnsureAsync(bundle, (long)_chainId).ConfigureAwait(false);

        public Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle) =>
            Task.FromResult<IChainProfile>(new DevChainProfile(_chainId, _genesisHash, _key));

        public ICanonicalStateRootSource CreateTip(PeerPoolManager pool) => null;

        private sealed class DevChainProfile : IChainProfile
        {
            private readonly EthECKey _key;

            public DevChainProfile(ulong networkId, byte[] genesisHash, EthECKey key)
            {
                NetworkId = networkId;
                GenesisHash = genesisHash;
                _key = key;
            }

            public ulong NetworkId { get; }

            public byte[] GenesisHash { get; }

            public (ulong[] BlockHeights, ulong[] Timestamps) ForkThresholds =>
                (Array.Empty<ulong>(), Array.Empty<ulong>());

            public IReadOnlyList<string> Bootnodes => Array.Empty<string>();

            public IPeerHandshakeWorker CreateHandshakeWorker(ILoggerFactory loggerFactory, bool advertiseSnap2) =>
                new ChainPeerHandshakeWorker(
                    GenesisHash, NetworkId, ForkThresholds, ourHead: null,
                    logger: null, localKey: _key, advertiseSnap2: advertiseSnap2);
        }
    }
}
