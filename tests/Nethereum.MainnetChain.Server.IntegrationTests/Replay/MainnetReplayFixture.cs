using System.Numerics;
using Nethereum.Chain.TestData;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.MainnetChain.Configuration;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.MainnetChain.Server.IntegrationTests.Replay;

public sealed class MainnetReplayFixture
{
    public const ulong HeadNumber = 22_500_002;
    private const long HeadTimestamp = 1_750_000_000;

    public MainnetReplayFixture(string dataDir, IReadOnlyList<StoredReplayBlock> blocks)
    {
        DataDir = dataDir;
        Blocks = blocks;
    }

    public string DataDir { get; }
    public IReadOnlyList<StoredReplayBlock> Blocks { get; }
    public int BlockCount => Blocks.Count;

    public MainnetChainServerConfig Config(int flushCadenceBlocks = 4, bool statistics = false, int prefetchWorkers = 0, int? stateRootWorkers = null) => new()
    {
        DataDir = DataDir,
        FlushCadenceBlocks = flushCadenceBlocks,
        PrefetchWorkers = prefetchWorkers,
        StateRootWorkers = stateRootWorkers ?? Nethereum.CoreChain.ExecutionStackOptions.DefaultStateRootWorkers,
        EnableRocksDbStatistics = statistics,
        ReplayBlocks = (ulong)BlockCount,
    };

    public static async Task<MainnetReplayFixture> BuildAsync(
        string dataDir, ISyncVector vector, Func<int, bool>? omitBodyOfBlockIndex = null)
    {
        var accounts = ChainRoster.Named
            .Select(r => new ChainAccount(r.Address, r.PrivateKey, ChainGenesisData.RosterPrefundBalance))
            .ToList();
        var storeConfig = new MainnetChainServerConfig { DataDir = dataDir };

        await using var stored = ChainNodeStorage.Open(storeConfig.ToChainNodeConfig());
        var target = stored.Bundle;
        using var scratch = InMemoryChainStoreBundle.Open();

        await ChainGenesisData.ApplyAsync(scratch.State, "prague", accounts);
        await ChainGenesisData.ApplyAsync(target.State, "prague", accounts);

        var scratchCalculator = new IncrementalStateRootCalculator(scratch.State, scratch.TrieNodes);
        var headRoot = await scratchCalculator.ComputeStateRootAsync();
        var targetRoot = await new IncrementalStateRootCalculator(
            target.State, target.StateTrieNodes,
            emitTombstones: !ReferenceEquals(target.StateTrieNodes, target.TrieNodes)).ComputeStateRootAsync();
        if (!headRoot.AsSpan().SequenceEqual(targetRoot))
            throw new InvalidOperationException("the seeded head state root differs between the scratch and the RocksDB store");

        var head = HeadHeader(headRoot);
        var headHash = BlockHashCalculator.ForFork(head, HardforkName.Prague);
        await scratch.Blocks.SaveAsync(head, headHash);
        await target.Blocks.SaveAsync(head, headHash);
        target.Metadata.Commit(HeadNumber, headHash);

        var driver = new MainnetRulesVectorChainDriver(BuildProducer(scratch, scratchCalculator), HeadTimestamp);
        await vector.BuildAsync(driver);

        for (var index = 0; index < driver.Produced.Count; index++)
        {
            var block = driver.Produced[index];
            await target.Blocks.SaveAsync(block.Header, block.Hash);
            await target.Uncles.SaveAsync(block.Hash, new List<BlockHeader>());
            if (omitBodyOfBlockIndex?.Invoke(index) != true)
                await target.Transactions.SaveManyAsync(block.Hash, block.Header.BlockNumber, block.Transactions);
            await target.Withdrawals.SaveAsync(block.Hash, block.Withdrawals);
        }

        return new MainnetReplayFixture(dataDir, driver.Produced);
    }

    public async Task ReplaceStoredHeaderAsync(int blockIndex, Action<BlockHeader> tamper)
    {
        await using var stored = ChainNodeStorage.Open(Config().ToChainNodeConfig());
        var block = Blocks[blockIndex];
        var header = block.Header.ShallowCopy();
        tamper(header);
        await stored.Bundle.Blocks.SaveAsync(header, block.Hash);
    }

    private static CoreChain.BlockProducer BuildProducer(InMemoryChainStoreBundle scratch, IncrementalStateRootCalculator calculator)
    {
        var engine = new BlockExecutor(
            scratch.State, scratch.Blocks, MainnetChainActivations.Instance,
            chainConfigFactory: fork => new ChainConfig
            {
                ChainId = MainnetGenesisConstants.ChainId,
                BaseFee = BigInteger.Zero,
                Coinbase = AddressUtil.ZERO_ADDRESS,
                Hardfork = fork.ToString().ToLowerInvariant(),
            },
            hardforkConfigFactory: fork => MainnetChainHardforkRegistry.Instance.Get(fork),
            stateRootCalculator: calculator,
            rewardPolicy: EthereumProofOfWorkRewardPolicy.Instance,
            trieNodeStore: scratch.TrieNodes);

        return new CoreChain.BlockProducer(
            engine, scratch.Blocks, scratch.Transactions, scratch.Receipts, scratch.Logs, scratch.State,
            scratch.TrieNodes, calculator,
            withdrawalStore: scratch.Withdrawals,
            hardforkConfigFactory: fork => MainnetChainHardforkRegistry.Instance.Get(fork));
    }

    private static BlockHeader HeadHeader(byte[] stateRoot) => new()
    {
        ParentHash = new byte[32],
        UnclesHash = new Sha3Keccack().CalculateHash(Nethereum.RLP.RLP.EncodeList()),
        Coinbase = "0x" + new string('c', 40),
        StateRoot = stateRoot,
        TransactionsHash = DefaultValues.EMPTY_TRIE_HASH,
        ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
        LogsBloom = new byte[256],
        Difficulty = 0,
        BlockNumber = HeadNumber,
        GasLimit = 30_000_000,
        GasUsed = 0,
        Timestamp = HeadTimestamp,
        ExtraData = Array.Empty<byte>(),
        MixHash = new byte[32],
        Nonce = new byte[8],
        BaseFee = new EvmUInt256(1_000_000_000),
        WithdrawalsRoot = DefaultValues.EMPTY_TRIE_HASH,
        BlobGasUsed = 0,
        ExcessBlobGas = 0,
        ParentBeaconBlockRoot = new byte[32],
        RequestsHash = System.Security.Cryptography.SHA256.HashData(Array.Empty<byte>()),
    };
}
