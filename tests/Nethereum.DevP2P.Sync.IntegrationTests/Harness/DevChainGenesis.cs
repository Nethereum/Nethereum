using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Chain.TestData;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.IntegrationTests.Harness
{
    public static class DevChainGenesis
    {
        public const string Coinbase = "0x0000000000000000000000000000000000000000";
        public const long BlockGasLimit = 500_000_000;
        public const long Timestamp = 1700000000;

        public static async Task<byte[]> EnsureAsync(IChainStoreBundle bundle, long chainId)
        {
            var existing = await bundle.Blocks.GetHashByNumberAsync(0).ConfigureAwait(false);
            if (existing != null && existing.Length > 0) return existing;

            await ChainGenesisData.ApplyAsync(bundle.State).ConfigureAwait(false);

            var calculator = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var stateRoot = await calculator.ComputeFullStateRootAsync().ConfigureAwait(false);

            var genesis = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                Coinbase = Coinbase,
                StateRoot = stateRoot ?? new byte[32],
                TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                LogsBloom = new byte[256],
                Difficulty = Nethereum.Util.EvmUInt256.One,
                BlockNumber = 0,
                GasLimit = BlockGasLimit,
                GasUsed = 0,
                Timestamp = Timestamp,
                ExtraData = Array.Empty<byte>(),
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            GenesisHeaderFields.Apply(genesis, HardforkNames.Parse("prague"));

            var hash = BlockHashCalculator.ForHeader(genesis);
            await bundle.Blocks.SaveAsync(genesis, hash).ConfigureAwait(false);
            return hash;
        }
    }
}
