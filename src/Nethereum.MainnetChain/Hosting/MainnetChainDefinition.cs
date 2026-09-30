using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.Consensus.LightClient;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Bootstrap;
using Nethereum.Model;
using Nethereum.Signer;

namespace Nethereum.MainnetChain.Hosting
{
    public sealed class MainnetChainDefinition : IChainDefinition
    {
        private readonly EthECKey _localKey;
        private readonly ITrustedHeaderProvider _trustedHeaderProvider;

        public MainnetChainDefinition(EthECKey localKey = null, ITrustedHeaderProvider trustedHeaderProvider = null)
        {
            _localKey = localKey;
            _trustedHeaderProvider = trustedHeaderProvider;
        }

        public async Task EnsureGenesisAsync(IChainStoreBundle bundle, CancellationToken ct)
        {
            if (!bundle.Metadata.IsGenesisLoaded())
            {
                await MainnetGenesisLoader.PopulateAsync(bundle.State).ConfigureAwait(false);
                bundle.Metadata.MarkGenesisLoaded();
            }

            var genesisHeader = await bundle.Blocks.GetByNumberAsync(BigInteger.Zero).ConfigureAwait(false);
            if (genesisHeader == null)
                await bundle.Blocks.SaveAsync(BuildGenesisHeader(), MainnetGenesisConstants.BlockHashHex.HexToByteArray())
                    .ConfigureAwait(false);
        }

        private static BlockHeader BuildGenesisHeader() => new BlockHeader
        {
            BlockNumber = 0,
            ParentHash = new byte[32],
            UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
            Coinbase = MainnetGenesisConstants.CoinbaseHex,
            StateRoot = MainnetGenesisConstants.StateRootHex.HexToByteArray(),
            TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
            ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
            LogsBloom = new byte[256],
            Difficulty = MainnetGenesisConstants.Difficulty,
            GasLimit = MainnetGenesisConstants.GasLimit,
            GasUsed = 0,
            Timestamp = (long)MainnetGenesisConstants.Timestamp,
            ExtraData = MainnetGenesisConstants.ExtraDataHex.HexToByteArray(),
            MixHash = MainnetGenesisConstants.MixHashHex.HexToByteArray(),
            Nonce = MainnetGenesisConstants.NonceHex.HexToByteArray(),
        };

        public Task<IChainProfile> CreateProfileAsync(IChainStoreBundle bundle) =>
            Task.FromResult<IChainProfile>(new MainnetChainProfile(_localKey, bundle));

        public ICanonicalStateRootSource CreateTip(PeerPoolManager pool)
        {
            var checkpoints = new MainnetKnownCheckpoints();
            if (_trustedHeaderProvider == null) return checkpoints;

            var lightClient = new LightClientCanonicalSource(_trustedHeaderProvider, useOptimistic: true);
            return new CompositeCanonicalStateRootSource(checkpoints, lightClient);
        }
    }
}
