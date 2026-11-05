using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;

namespace Nethereum.CoreChain.UnitTests
{
    internal static class ShapedHeaders
    {
        public const long GenesisTimestamp = 1_699_999_000;
        public const string Coinbase = "0x0000000000000000000000000000000000009999";

        public static BlockHeader Create(
            HardforkName fork, long number, long timestamp, long gasLimit, long gasUsed, long baseFee,
            long difficulty = 0)
        {
            var codec = BlockHeaderCodecs.ForFork(fork);
            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
                Coinbase = Coinbase,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                TransactionsHash = DefaultValues.EMPTY_TRIE_ROOT,
                ReceiptHash = DefaultValues.EMPTY_TRIE_ROOT,
                LogsBloom = new byte[256],
                Difficulty = (EvmUInt256)difficulty,
                BlockNumber = (EvmUInt256)number,
                GasLimit = gasLimit,
                GasUsed = gasUsed,
                Timestamp = timestamp,
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8]
            };

            if (codec.CarriesBaseFee) header.BaseFee = (EvmUInt256)baseFee;
            if (codec.CarriesWithdrawalsRoot) header.WithdrawalsRoot = DefaultValues.EMPTY_TRIE_ROOT;
            if (codec.CarriesBlobFieldsAndBeaconRoot)
            {
                header.BlobGasUsed = 0;
                header.ExcessBlobGas = 0;
                header.ParentBeaconBlockRoot = new byte[32];
            }
            if (codec.CarriesRequestsHash) header.RequestsHash = new byte[32];
            if (codec.CarriesBlockAccessList)
            {
                header.BlockAccessListHash = new byte[32];
                header.SlotNumber = (ulong)number;
            }
            return header;
        }

        public static BlockHeader Genesis(HardforkName fork, long gasLimit, long baseFee = 0) =>
            Create(fork, 0, GenesisTimestamp, gasLimit, 0, baseFee);

        public static async Task<byte[]> SaveGenesisAsync(
            IBlockStore blockStore, HardforkName fork, long gasLimit, long baseFee = 0)
        {
            var genesis = Genesis(fork, gasLimit, baseFee);
            var hash = BlockHashCalculator.ForFork(genesis, fork);
            await blockStore.SaveAsync(genesis, hash);
            return hash;
        }
    }
}
