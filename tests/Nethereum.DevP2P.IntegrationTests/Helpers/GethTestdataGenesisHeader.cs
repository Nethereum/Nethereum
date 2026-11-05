using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Newtonsoft.Json.Linq;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public static class GethTestdataGenesisHeader
    {
        public static async Task<byte[]> SeedAsync(
            string testdata, IBlockStore blocks, IStateStore state, ITrieNodeStore stateTrieNodes, bool emitTombstones = false)
        {
            var genesis = JObject.Parse(File.ReadAllText(Path.Combine(testdata, "genesis.json")));
            var calculator = new IncrementalStateRootCalculator(state, stateTrieNodes, emitTombstones: emitTombstones);
            var stateRoot = await calculator.ComputeStateRootWithoutPersistAsync(null);
            await calculator.PersistPendingStateAsync();

            var header = new BlockHeader
            {
                ParentHash = new byte[32],
                UnclesHash = DefaultValues.EMPTY_UNCLES_HASH,
                Coinbase = genesis["coinbase"].ToString(),
                StateRoot = stateRoot,
                TransactionsHash = DefaultValues.EMPTY_TRIE_ROOT,
                ReceiptHash = DefaultValues.EMPTY_TRIE_ROOT,
                LogsBloom = new byte[256],
                Difficulty = EvmUInt256BigIntegerExtensions.FromBigInteger(new HexBigInteger(genesis["difficulty"].ToString()).Value),
                BlockNumber = 0,
                GasLimit = (long)new HexBigInteger(genesis["gasLimit"].ToString()).Value,
                GasUsed = 0,
                Timestamp = (long)new HexBigInteger(genesis["timestamp"].ToString()).Value,
                ExtraData = genesis["extraData"].ToString().HexToByteArray(),
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = EvmUInt256BigIntegerExtensions.FromBigInteger(new HexBigInteger(genesis["baseFeePerGas"].ToString()).Value),
                WithdrawalsRoot = DefaultValues.EMPTY_TRIE_ROOT
            };

            var chainBytes = await File.ReadAllBytesAsync(Path.Combine(testdata, "chain.rlp"));
            var firstBlock = (RLPCollection)RLP.RLP.DecodeFirstElement(chainBytes, 0);
            var firstHeader = new BlockHeaderEncoder().Decode(RlpStreamHelpers.ReEncodeAsList((RLPCollection)firstBlock[0]));
            await blocks.SaveAsync(header, firstHeader.ParentHash);
            return stateRoot;
        }
    }
}
