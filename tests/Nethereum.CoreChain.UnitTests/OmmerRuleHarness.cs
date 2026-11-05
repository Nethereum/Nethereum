using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;

namespace Nethereum.CoreChain.UnitTests
{
    internal sealed class OmmerRuleHarness
    {
        public const HardforkName Fork = HardforkName.Berlin;
        public const long TipNumber = 1000;
        public const long TipTimestamp = 1_700_000_000;
        public const long AncestorCount = 9;
        public const long GasLimit = 30_000_000;

        private readonly Dictionary<long, BlockHeader> _chain;
        private readonly Dictionary<long, byte[]> _hashes;

        private OmmerRuleHarness(
            Dictionary<long, BlockHeader> chain, Dictionary<long, byte[]> hashes,
            BlockImporter importer, InMemoryUncleStore uncleStore)
        {
            _chain = chain;
            _hashes = hashes;
            Importer = importer;
            UncleStore = uncleStore;
        }

        public BlockImporter Importer { get; }
        public InMemoryUncleStore UncleStore { get; }

        public static async Task<OmmerRuleHarness> CreateAsync(System.Func<long> clock = null, bool withUncleStore = true)
        {
            var stateStore = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, Fork);
            var blockStore = new InMemoryBlockStore();
            var uncleStore = new InMemoryUncleStore(blockStore);
            var config = new ChainConfig { ChainId = 1337, BlockGasLimit = GasLimit, BaseFee = 0 };
            var trieNodeStore = new InMemoryContentNodeStore();

            var engine = new BlockExecutor(
                stateStore, blockStore, new FixedChainActivations(Fork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: config.ConfigForFork,
                stateRootCalculator: new IncrementalStateRootCalculator(stateStore, trieNodeStore),
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore,
                logger: null,
                blockRootsProvider: null,
                authorResolver: null,
                validation: new BlockValidationOptions { UncleStore = withUncleStore ? uncleStore : null, UnixClock = clock });

            var chain = new Dictionary<long, BlockHeader>();
            var hashes = new Dictionary<long, byte[]>();
            var previousHash = new byte[32];
            for (var number = TipNumber - AncestorCount + 1; number <= TipNumber; number++)
            {
                var header = ShapedHeaders.Create(
                    Fork, number, TipTimestamp - 12 * (TipNumber - number), GasLimit, 0, 0,
                    difficulty: HeaderRuleHarness.ParentDifficulty);
                header.ParentHash = previousHash;
                previousHash = BlockHashCalculator.ForFork(header, Fork);
                chain[number] = header;
                hashes[number] = previousHash;
                await blockStore.SaveAsync(header, previousHash);
            }

            return new OmmerRuleHarness(
                chain, hashes, new BlockImporter(engine, blockStore, stateStore, uncleStore: uncleStore), uncleStore);
        }

        public BlockHeader AncestorAt(long depth) => _chain[TipNumber + 1 - depth];

        public byte[] AncestorHashAt(long depth) => _hashes[TipNumber + 1 - depth];

        public BlockHeader Child(IList<BlockHeader> uncles = null)
        {
            var parent = _chain[TipNumber];
            var timestamp = parent.Timestamp + 12;
            var child = ShapedHeaders.Create(Fork, TipNumber + 1, timestamp, GasLimit, 0, 0);
            child.ParentHash = _hashes[TipNumber];
            child.Difficulty = (EvmUInt256)EthashDifficulty.Calculate(Fork, (ulong)timestamp, parent);
            child.UnclesHash = HashOfUncles(uncles ?? new List<BlockHeader>());
            return child;
        }

        public BlockHeader UncleOfAncestorAt(long parentDepth, string marker = "a")
        {
            var parent = AncestorAt(parentDepth);
            var timestamp = parent.Timestamp + 5;
            var uncle = ShapedHeaders.Create(Fork, (long)(ulong)parent.BlockNumber + 1, timestamp, GasLimit, 0, 0);
            uncle.ParentHash = AncestorHashAt(parentDepth);
            uncle.Difficulty = (EvmUInt256)EthashDifficulty.Calculate(Fork, (ulong)timestamp, parent);
            uncle.Coinbase = marker == "a"
                ? "0x00000000000000000000000000000000000000a1"
                : "0x00000000000000000000000000000000000000b2";
            return uncle;
        }

        public Task<BlockImporterResult> ImportAsync(BlockHeader header, IList<BlockHeader> uncles) =>
            Importer.ImportAsync(header, new List<ISignedTransaction>(), uncles, null, CancellationToken.None);

        public static byte[] HashOfUncles(IList<BlockHeader> uncles) =>
            Sha3Keccack.Current.CalculateHash(
                RLP.RLP.EncodeList(uncles.Select(u => BlockHeaderCodecs.ForFork(Fork).Encode(u)).ToArray()));
    }
}
