using System;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Forks;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.AppChain.IntegrationTests
{
    internal static class TestGenesis
    {
        public static async Task<byte[]> WriteAsync(IChainStoreBundle bundle)
        {
            var existing = await bundle.Blocks.GetHashByNumberAsync(0).ConfigureAwait(false);
            if (existing != null && existing.Length > 0) return existing;

            var calculator = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var stateRoot = await calculator.ComputeFullStateRootAsync().ConfigureAwait(false);

            var genesis = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = stateRoot ?? new byte[32],
                TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                LogsBloom = new byte[256],
                Difficulty = Nethereum.Util.EvmUInt256.One,
                BlockNumber = 0,
                GasLimit = 30_000_000,
                GasUsed = 0,
                Timestamp = 1700000000,
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
