using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Tracing;
using Nethereum.CoreChain.Validation;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.MainnetChain.Configuration;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using BlockWithTransactionHashes = Nethereum.RPC.Eth.DTOs.BlockWithTransactionHashes;
using CallInput = Nethereum.RPC.Eth.DTOs.CallInput;
using EthSimulateBlockResult = Nethereum.RPC.Eth.DTOs.EthSimulateBlockResult;
using EthSimulateInput = Nethereum.RPC.Eth.DTOs.EthSimulateInput;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class FollowerCoreE2ETests : IDisposable
    {
        private readonly string _dataDir;
        private IChainStoreBundle _activeBundle;

        public FollowerCoreE2ETests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"follower_core_e2e_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            try { _activeBundle?.Dispose(); } catch { }
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public ValidationAction Verdict { get; set; } = ValidationAction.RewindAndRetry;
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => Verdict;
        }

        [Fact]
        public async Task Fixture_LoadChainAndGenesis_SanityCheck()
        {
            Assert.True(HiveTestdataFixture.Chain.Count > 0,
                "chain.rlp should decode at least one block bundle");

            var firstBundle = HiveTestdataFixture.Chain[0];
            Assert.True(firstBundle.Header.BlockNumber > 0,
                "Hive chain.rlp first bundle is expected to be block 1+ (genesis lives in genesis.json)");

            Assert.True(HiveTestdataFixture.GenesisAllocRaw.Count > 0,
                "genesis.json alloc should contain at least one account");

            var stateStore = new InMemoryStateStore();
            await HiveTestdataFixture.PopulateGenesisAsync(stateStore);

            var firstAllocKey = HiveTestdataFixture.GenesisAllocRaw.Keys.First();
            var lookupAddr = firstAllocKey.StartsWith("0x") ? firstAllocKey : "0x" + firstAllocKey;
            var loaded = await stateStore.GetAccountAsync(lookupAddr);
            Assert.NotNull(loaded);
        }

        [Fact]
        public async Task ForwardSync_HiveChain_StateRootsMatchCanonical()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle => FollowerStackBuilder.Build(
                    bundle,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: System.Threading.CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(expectedBlocks, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir);
            Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Given_the_real_FollowerService_replays_HiveTestdataFixture_through_block_17_When_eth_getBlockByNumber_is_called_Then_withdrawals_are_served()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const int targetBlock = 17;
            var expectedWithdrawals = HiveTestdataFixture.Chain[targetBlock - 1].Withdrawals;
            Assert.True(expectedWithdrawals != null && expectedWithdrawals.Count > 0,
                "test setup: block 17 must carry a non-empty withdrawal list (verified against the live Hive fixture)");

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle => FollowerStackBuilder.Build(
                    bundle,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0, EndBlock: targetBlock),
                ct: CancellationToken.None);

            Assert.Equal((ulong)targetBlock, result.LastExecutedBlock);

            var node = new BundleBackedChainNode(_activeBundle);
            var context = new RpcContext(node, chainId: 1, services: new SingleServiceProvider(_activeBundle));

            var response = await new EthGetBlockByNumberHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockByNumber", $"0x{targetBlock:x}", false), context);

            Assert.False(response.HasError, response.Error?.Message);
            var block = Assert.IsType<BlockWithTransactionHashes>(response.ResultNewtonsoft);

            Assert.NotNull(block.Withdrawals);
            Assert.Equal(expectedWithdrawals.Count, block.Withdrawals.Length);
            for (var i = 0; i < expectedWithdrawals.Count; i++)
            {
                Assert.Equal(expectedWithdrawals[i].Index, (ulong)block.Withdrawals[i].Index.Value);
                Assert.Equal(expectedWithdrawals[i].ValidatorIndex, (ulong)block.Withdrawals[i].ValidatorIndex.Value);
                Assert.Equal(expectedWithdrawals[i].AmountInGwei, (ulong)block.Withdrawals[i].Amount.Value);
            }
        }

        private sealed class SingleServiceProvider : IServiceProvider
        {
            private readonly object _service;
            public SingleServiceProvider(object service) => _service = service;
            public object GetService(Type serviceType) => serviceType.IsInstanceOfType(_service) ? _service : null;
        }

        private sealed class BundleBackedChainNode : IChainNode
        {
            private readonly IChainStoreBundle _bundle;
            public BundleBackedChainNode(IChainStoreBundle bundle) => _bundle = bundle;

            public ChainConfig Config => throw new NotImplementedException();
            public IBlockStore Blocks => _bundle.Blocks;
            public ITransactionStore Transactions => _bundle.Transactions;
            public IUncleStore Uncles => _bundle.Uncles;
            public IReceiptStore Receipts => _bundle.Receipts;
            public ILogStore Logs => _bundle.Logs;
            public IStateStore State => _bundle.State;
            public IFilterStore Filters => throw new NotImplementedException();
            public ITrieNodeStore TrieNodes => _bundle.TrieNodes;
            public IBlobStore BlobStore => throw new NotImplementedException();
            public IBlockAccessListStore BlockAccessLists => _bundle.BlockAccessLists;
            public Nethereum.CoreChain.Services.IProofService ProofService => throw new NotImplementedException();

            public Task<BigInteger> GetBlockNumberAsync() => _bundle.Blocks.GetHeightAsync();
            public Task<BlockHeader> GetBlockByHashAsync(byte[] hash) => _bundle.Blocks.GetByHashAsync(hash);
            public Task<BlockHeader> GetBlockByNumberAsync(BigInteger number) => _bundle.Blocks.GetByNumberAsync(number);
            public Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber) => _bundle.Blocks.GetHashByNumberAsync(blockNumber);
            public Task<BlockHeader> GetLatestBlockAsync() => _bundle.Blocks.GetLatestAsync();

            public Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<Receipt> GetTransactionReceiptAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<BigInteger> GetBalanceAsync(string address) => throw new NotImplementedException();
            public Task<BigInteger> GetNonceAsync(string address) => throw new NotImplementedException();
            public Task<byte[]> GetCodeAsync(string address) => throw new NotImplementedException();
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot) => throw new NotImplementedException();
            public Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<CallResult> CallAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null, Dictionary<string, StateOverride> stateOverrides = null, List<Authorisation7702Signed> authorisationList = null) => throw new NotImplementedException();
            public Task<CallResult> CallAsync(string to, byte[] data, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null, Dictionary<string, StateOverride> stateOverrides = null, List<Authorisation7702Signed> authorisationList = null) => throw new NotImplementedException();
            public Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
            public Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
            public Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
            public Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
            public Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx) => throw new NotImplementedException();
            public Task<List<ISignedTransaction>> GetPendingTransactionsAsync() => throw new NotImplementedException();
            public Task<List<BlobSidecarRecord>> GetBlobSidecarsByBlockNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();
            public Task<OpcodeTraceResult> TraceTransactionAsync(string txHash, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<CallTraceResult> TraceTransactionCallTracerAsync(string txHash) => throw new NotImplementedException();
            public Task<PrestateTraceResult> TraceTransactionPrestateAsync(string txHash) => throw new NotImplementedException();
            public Task<byte[]> CaptureBlockWitnessAsync(long blockNumber) => throw new NotImplementedException();
            public Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, BigInteger blockNumber, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<List<EthSimulateBlockResult>> SimulateAsync(EthSimulateInput input, BigInteger? baseBlockNumber) => throw new NotImplementedException();
            public Task<List<Nethereum.RPC.DebugNode.Dtos.Tracing.BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByNumberAsync(BigInteger blockNumber, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<List<Nethereum.RPC.DebugNode.Dtos.Tracing.BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByHashAsync(byte[] blockHash, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<List<Nethereum.RPC.DebugNode.Dtos.Tracing.BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();
            public Task<List<Nethereum.RPC.DebugNode.Dtos.Tracing.BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByHashAsync(byte[] blockHash) => throw new NotImplementedException();
        }

        [Fact]
        public async Task ForwardSync_HiveChain_PathKeyed_StateRootsMatchCanonical()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var storageOptions = new RocksDbStorageOptions
            {
                DatabasePath = _dataDir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 0,
            };
            var manager = new RocksDbManager(storageOptions);
            try
            {
                using (var seedBundle = RocksDbChainStoreBundle.FromManager(manager, _dataDir, journalOptions: null, ownsManager: false))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
                }

                var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
                var follower = new FollowerService();
                var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
                ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
                ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

                RocksDbChainStoreBundle followBundle = null;
                var result = await follower.RunAsync(
                    source,
                    bundleFactory: () => followBundle = RocksDbChainStoreBundle.FromManager(manager, _dataDir, journalOptions: null, ownsManager: false),
                    executorFactory: bundle => FollowerStackBuilder.BuildPathKeyed(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory),
                    policy: policy,
                    canonical: null,
                    options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                    ct: CancellationToken.None);

                Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
                Assert.Equal(expectedBlocks, result.BlocksExecuted);
                Assert.Equal(0UL, result.RootMismatches);
                Assert.Equal(lastChainBlock, result.LastExecutedBlock);

                Assert.NotNull(followBundle);
                Assert.IsType<CapturingJournalingPathNodeStore>(followBundle.StateTrieNodes);
                Assert.NotNull(followBundle.NodeCommitBlockSource);

                followBundle.Dispose();
                followBundle = null;

                int nodeHistoryEntries = CountNodeHistoryEntries(manager);
                Assert.True(nodeHistoryEntries > 0,
                    "expected CF_NODE_HISTORY to contain entries after the path-keyed replay, proving the node-history journal ran (not a silent hash-mode fallback)");

                using var reopened = RocksDbChainStoreBundle.FromManager(manager, _dataDir, journalOptions: null, ownsManager: false);
                Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
            }
            finally
            {
                manager.Dispose();
            }
        }

        private static int CountNodeHistoryEntries(RocksDbManager manager)
        {
            using var it = manager.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                n++;
                it.Next();
            }
            return n;
        }

        private static Dictionary<string, byte[]> DumpCf(RocksDbManager manager, string cf)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = manager.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private static void AssertCfIdentical(RocksDbManager k1, RocksDbManager k2, string cf)
        {
            var dumpK1 = DumpCf(k1, cf);
            var dumpK2 = DumpCf(k2, cf);
            Assert.True(dumpK1.Count == dumpK2.Count,
                $"[{cf}] row count differs: K=1={dumpK1.Count} K=2={dumpK2.Count}");
            foreach (var kv in dumpK1)
            {
                Assert.True(dumpK2.TryGetValue(kv.Key, out var k2Val),
                    $"[{cf}] key {kv.Key} present at K=1, missing at K=2");
                Assert.True(kv.Value.AsSpan().SequenceEqual(k2Val),
                    $"[{cf}] key {kv.Key} value differs between K=1 and K=2\nK1={Convert.ToHexString(kv.Value)}\nK2={Convert.ToHexString(k2Val)}");
            }
        }

        private static void AssertBlockStateDiffsEqual(BlockStateDiff expected, BlockStateDiff actual)
        {
            Assert.Equal(expected.BlockNumber, actual.BlockNumber);
            Assert.Equal(expected.AccountDiffs.Count, actual.AccountDiffs.Count);
            var expectedAccounts = expected.AccountDiffs
                .OrderBy(a => a.Address, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var actualAccounts = actual.AccountDiffs
                .OrderBy(a => a.Address, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (int i = 0; i < expectedAccounts.Count; i++)
            {
                Assert.Equal(expectedAccounts[i].Address, actualAccounts[i].Address, ignoreCase: true);
                var expPre = expectedAccounts[i].PreValue;
                var actPre = actualAccounts[i].PreValue;
                if (expPre == null || actPre == null)
                {
                    Assert.True(expPre == null && actPre == null,
                        $"account {expectedAccounts[i].Address} pre-value nullness differs");
                    continue;
                }
                Assert.Equal(expPre.Nonce, actPre.Nonce);
                Assert.Equal(expPre.Balance, actPre.Balance);
                Assert.True((expPre.CodeHash ?? Array.Empty<byte>()).AsSpan().SequenceEqual(actPre.CodeHash ?? Array.Empty<byte>()),
                    $"account {expectedAccounts[i].Address} pre-value CodeHash differs");
            }

            Assert.Equal(expected.StorageDiffs.Count, actual.StorageDiffs.Count);
            var expectedStorage = expected.StorageDiffs
                .OrderBy(s => s.Address, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => Convert.ToHexString(s.SlotKey))
                .ToList();
            var actualStorage = actual.StorageDiffs
                .OrderBy(s => s.Address, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => Convert.ToHexString(s.SlotKey))
                .ToList();
            for (int i = 0; i < expectedStorage.Count; i++)
            {
                Assert.Equal(expectedStorage[i].Address, actualStorage[i].Address, ignoreCase: true);
                Assert.True(expectedStorage[i].SlotKey.AsSpan().SequenceEqual(actualStorage[i].SlotKey),
                    $"storage slot key differs for {expectedStorage[i].Address}");
                var expPre = expectedStorage[i].PreValue ?? Array.Empty<byte>();
                var actPre = actualStorage[i].PreValue ?? Array.Empty<byte>();
                Assert.True(expPre.AsSpan().SequenceEqual(actPre),
                    $"storage pre-value differs for {expectedStorage[i].Address}/{Convert.ToHexString(expectedStorage[i].SlotKey)}");
            }
        }

        [Fact]
        public async Task ForwardSync_HiveChain_PathKeyed_K2_MatchesK1_CfByteIdentical()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var defaults = new MainnetChainServerConfig();
            var journalOptions = new HistoricalStateOptions
            {
                MaxHistoryBlocks = defaults.JournalBlocks,
                EnablePruning = true,
                PruningIntervalBlocks = Math.Max(64, defaults.JournalBlocks / 16),
            };

            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            Assert.True(expectedBlocks >= 500, $"expected the full Hive battery (~600 blocks), got {expectedBlocks}");
            Assert.True(expectedBlocks % 2 == 0,
                "K=2 boundary rule (block % K == 0) requires an even-length replay to end fully flushed");

            var dirK1 = Path.Combine(Path.GetTempPath(), $"k2vsk1_k1_{Guid.NewGuid():N}");
            var dirK2 = Path.Combine(Path.GetTempPath(), $"k2vsk1_k2_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dirK1);
            Directory.CreateDirectory(dirK2);

            try
            {
                RocksDbStorageOptions StorageOptionsFor(string dir) => new RocksDbStorageOptions
                {
                    DatabasePath = dir,
                    PathKeyedState = defaults.PathKeyedState,
                    TrieNodeHistoryBlocks = defaults.TrieNodeHistoryBlocks,
                    TrieNodeHistoryIndex = defaults.TrieNodeHistoryIndex,
                };

                using var mgrK1 = new RocksDbManager(StorageOptionsFor(dirK1));
                using var mgrK2 = new RocksDbManager(StorageOptionsFor(dirK2));

                using var bundleK1 = RocksDbChainStoreBundle.FromManager(mgrK1, dirK1, journalOptions: journalOptions, ownsManager: false);
                using var bundleK2 = RocksDbChainStoreBundle.FromManager(mgrK2, dirK2, journalOptions: journalOptions, ownsManager: false);

                await HiveTestdataFixture.PopulateGenesisAsync(bundleK1.State);
                await HiveTestdataFixture.PopulateGenesisAsync(bundleK2.State);

                var executorK1 = FollowerStackBuilder.BuildPathKeyed(
                    bundleK1, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory, HiveTestdataFixture.ChainConfigFactory);
                var executorK2 = FollowerStackBuilder.BuildPathKeyed(
                    bundleK2, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory, HiveTestdataFixture.ChainConfigFactory,
                    flushCadence: new FixedIntervalFlushCadence(2));

                foreach (var b in HiveTestdataFixture.Chain)
                {
                    var withdrawals = b.Withdrawals;

                    var blockNumber = (ulong)b.Header.BlockNumber;

                    var resK1 = await executorK1.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
                    Assert.True(resK1.RootMatches, $"K=1 block {blockNumber} should match its header");

                    var resK2 = await executorK2.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
                    Assert.True(resK2.RootMatches, $"K=2 block {blockNumber} should match its header");

                    Assert.Equal(resK1.ComputedStateRoot, resK2.ComputedStateRoot);

                    bundleK1.Metadata.Commit(blockNumber, resK1.BlockHash);
                    bundleK2.Metadata.Commit(blockNumber, resK2.BlockHash);
                }

                await ((IAtomicBlockFlush)bundleK1).DrainAsync();
                await ((IAtomicBlockFlush)bundleK2).DrainAsync();

                ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

                Assert.Equal(lastChainBlock, bundleK1.Metadata.GetDurableStateBlock());
                Assert.Equal(lastChainBlock, bundleK2.Metadata.GetDurableStateBlock());
                Assert.Equal(lastChainBlock, bundleK1.Metadata.GetLastBlock());
                Assert.Equal(lastChainBlock, bundleK2.Metadata.GetLastBlock());

                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_TRIE_STORAGE);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_NODE_HISTORY);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_NODE_HISTORY_INDEX);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_METADATA);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_ACCOUNTS);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_STORAGE);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_CODE);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_HISTORY_STORAGE);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_HISTORY_META);
                AssertCfIdentical(mgrK1, mgrK2, RocksDbManager.CF_STATE_ROOT_INDEX);

                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_NODE_HISTORY).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_TRIE_ACCOUNT).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_ACCOUNTS).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_ROOT_INDEX).Count > 0);
            }
            finally
            {
                try { if (Directory.Exists(dirK1)) Directory.Delete(dirK1, recursive: true); } catch { }
                try { if (Directory.Exists(dirK2)) Directory.Delete(dirK2, recursive: true); } catch { }
            }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(24)]
        [InlineData(120)]
        public async Task ForwardSync_HiveChain_PathKeyed_LargerK_MatchesK1_CfByteIdentical(int k)
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var defaults = new MainnetChainServerConfig();
            var journalOptions = new HistoricalStateOptions
            {
                MaxHistoryBlocks = defaults.JournalBlocks,
                EnablePruning = true,
                PruningIntervalBlocks = Math.Max(64, defaults.JournalBlocks / 16),
            };

            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            Assert.True(expectedBlocks >= 500, $"expected the full Hive battery (~600 blocks), got {expectedBlocks}");
            Assert.True(expectedBlocks % (ulong)k == 0,
                $"K={k} boundary rule (block % K == 0) requires the replay length to be a multiple of K so both runs end fully flushed");

            var dirK1 = Path.Combine(Path.GetTempPath(), $"klargevsk1_k1_{k}_{Guid.NewGuid():N}");
            var dirK = Path.Combine(Path.GetTempPath(), $"klargevsk1_k_{k}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dirK1);
            Directory.CreateDirectory(dirK);

            try
            {
                RocksDbStorageOptions StorageOptionsFor(string dir) => new RocksDbStorageOptions
                {
                    DatabasePath = dir,
                    PathKeyedState = defaults.PathKeyedState,
                    TrieNodeHistoryBlocks = defaults.TrieNodeHistoryBlocks,
                    TrieNodeHistoryIndex = defaults.TrieNodeHistoryIndex,
                };

                using var mgrK1 = new RocksDbManager(StorageOptionsFor(dirK1));
                using var mgrK = new RocksDbManager(StorageOptionsFor(dirK));

                using var bundleK1 = RocksDbChainStoreBundle.FromManager(mgrK1, dirK1, journalOptions: journalOptions, ownsManager: false);
                using var bundleK = RocksDbChainStoreBundle.FromManager(mgrK, dirK, journalOptions: journalOptions, ownsManager: false);

                await HiveTestdataFixture.PopulateGenesisAsync(bundleK1.State);
                await HiveTestdataFixture.PopulateGenesisAsync(bundleK.State);

                var executorK1 = FollowerStackBuilder.BuildPathKeyed(
                    bundleK1, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory, HiveTestdataFixture.ChainConfigFactory);
                var executorK = FollowerStackBuilder.BuildPathKeyed(
                    bundleK, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory, HiveTestdataFixture.ChainConfigFactory,
                    flushCadence: new FixedIntervalFlushCadence((ulong)k));

                foreach (var b in HiveTestdataFixture.Chain)
                {
                    var withdrawals = b.Withdrawals;
                    var blockNumber = (ulong)b.Header.BlockNumber;

                    var resK1 = await executorK1.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
                    Assert.True(resK1.RootMatches, $"K=1 block {blockNumber} should match its header");

                    var resK = await executorK.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
                    Assert.True(resK.RootMatches, $"K={k} block {blockNumber} should match its header");

                    Assert.Equal(resK1.ComputedStateRoot, resK.ComputedStateRoot);

                    bundleK1.Metadata.Commit(blockNumber, resK1.BlockHash);
                    bundleK.Metadata.Commit(blockNumber, resK.BlockHash);
                }

                await ((IAtomicBlockFlush)bundleK1).DrainAsync();
                await ((IAtomicBlockFlush)bundleK).DrainAsync();

                ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

                Assert.Equal(lastChainBlock, bundleK1.Metadata.GetDurableStateBlock());
                Assert.Equal(lastChainBlock, bundleK.Metadata.GetDurableStateBlock());
                Assert.Equal(lastChainBlock, bundleK1.Metadata.GetLastBlock());
                Assert.Equal(lastChainBlock, bundleK.Metadata.GetLastBlock());

                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_TRIE_STORAGE);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_NODE_HISTORY);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_NODE_HISTORY_INDEX);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_METADATA);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_ACCOUNTS);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_STORAGE);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_CODE);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_HISTORY_STORAGE);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_HISTORY_META);
                AssertCfIdentical(mgrK1, mgrK, RocksDbManager.CF_STATE_ROOT_INDEX);

                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_NODE_HISTORY).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_TRIE_ACCOUNT).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_ACCOUNTS).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS).Count > 0);
                Assert.True(DumpCf(mgrK1, RocksDbManager.CF_STATE_ROOT_INDEX).Count > 0);
            }
            finally
            {
                try { if (Directory.Exists(dirK1)) Directory.Delete(dirK1, recursive: true); } catch { }
                try { if (Directory.Exists(dirK)) Directory.Delete(dirK, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task BoundaryKill_HeadCommitSkippedAfterAtomicBatch_ResumesCleanlyWithoutThrow()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var dbDir = Path.Combine(Path.GetTempPath(), $"boundary_kill_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dbDir);
            try
            {
                var storageOptions = new RocksDbStorageOptions
                {
                    DatabasePath = dbDir,
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = 128,
                    TrieNodeHistoryIndex = true,
                };
                var journalOptions = new HistoricalStateOptions
                {
                    MaxHistoryBlocks = 128,
                    EnablePruning = false,
                };

                ulong boundaryBlock = 0;

                using (var bundle = RocksDbChainStoreBundle.Open(dbDir, journalOptions: journalOptions, storageOptions: storageOptions))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);

                    var executor = FollowerStackBuilder.BuildPathKeyed(
                        bundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(2));

                    for (int i = 0; i < 2; i++)
                    {
                        var b = HiveTestdataFixture.Chain[i];
                        var withdrawals = b.Withdrawals;
                        var blockNumber = (ulong)b.Header.BlockNumber;

                        var res = await executor.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
                        Assert.True(res.RootMatches, $"block {blockNumber} should match its header");

                        bool isBoundary = blockNumber % 2 == 0;
                        if (!isBoundary)
                        {
                            bundle.Metadata.Commit(blockNumber, res.BlockHash);
                        }
                        else
                        {
                            boundaryBlock = blockNumber;
                        }
                    }

                    await ((IAtomicBlockFlush)bundle).DrainAsync();

                    Assert.True(boundaryBlock > 0, "test setup: expected to reach exactly one K=2 boundary");
                    Assert.Equal(boundaryBlock, bundle.Metadata.GetDurableStateBlock());
                }

                using var reopened = RocksDbChainStoreBundle.Open(dbDir, journalOptions: journalOptions, storageOptions: storageOptions);
                var messages = new List<string>();

                var (head, recovered) = await reopened.EnsureConsistentHeadAsync(messages.Add);

                Assert.Equal(boundaryBlock, head);
                Assert.False(recovered);
                Assert.Equal(boundaryBlock, reopened.Metadata.GetLastBlock());
                Assert.Equal(boundaryBlock, reopened.Metadata.GetDurableStateBlock());

                var recoveredDiff = await reopened.Diffs.GetBlockDiffAsync(boundaryBlock);
                Assert.NotNull(recoveredDiff);
                Assert.Equal((System.Numerics.BigInteger)boundaryBlock, recoveredDiff.BlockNumber);
                Assert.True(recoveredDiff.AccountDiffs.Count > 0 || recoveredDiff.StorageDiffs.Count > 0,
                    "boundary block's recovered reverse-diff should record at least one touched account/slot");

                var refDir = Path.Combine(Path.GetTempPath(), $"boundary_kill_ref_{Guid.NewGuid():N}");
                Directory.CreateDirectory(refDir);
                try
                {
                    BlockStateDiff referenceDiff;
                    using (var refBundle = RocksDbChainStoreBundle.Open(refDir, journalOptions: journalOptions, storageOptions: new RocksDbStorageOptions
                    {
                        DatabasePath = refDir,
                        PathKeyedState = true,
                        TrieNodeHistoryBlocks = 128,
                        TrieNodeHistoryIndex = true,
                    }))
                    {
                        await HiveTestdataFixture.PopulateGenesisAsync(refBundle.State);
                        var refExecutor = FollowerStackBuilder.BuildPathKeyed(
                            refBundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                            HiveTestdataFixture.ChainConfigFactory);
                        for (int i = 0; i < 2; i++)
                        {
                            var b = HiveTestdataFixture.Chain[i];
                            var withdrawals = b.Withdrawals;
                            var blockNumber = (ulong)b.Header.BlockNumber;
                            var res = await refExecutor.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
                            Assert.True(res.RootMatches, $"reference block {blockNumber} should match its header");
                            refBundle.Metadata.Commit(blockNumber, res.BlockHash);
                        }
                        referenceDiff = await refBundle.Diffs.GetBlockDiffAsync(boundaryBlock);
                    }

                    Assert.NotNull(referenceDiff);
                    AssertBlockStateDiffsEqual(referenceDiff, recoveredDiff);
                }
                finally
                {
                    try { if (Directory.Exists(refDir)) Directory.Delete(refDir, recursive: true); } catch { }
                }
            }
            finally
            {
                try { if (Directory.Exists(dbDir)) Directory.Delete(dbDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task MidWindowKill_NonBoundaryBlock_ResumesFromDurableCursor_MatchesUninterruptedK2Run()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            RocksDbStorageOptions StorageOptionsFor(string dir) => new RocksDbStorageOptions
            {
                DatabasePath = dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
                TrieNodeHistoryIndex = true,
            };
            var journalOptions = new HistoricalStateOptions
            {
                MaxHistoryBlocks = 128,
                EnablePruning = false,
            };

            const int blockCount = 4;
            Assert.True(HiveTestdataFixture.Chain.Count >= blockCount);

            var dirCrash = Path.Combine(Path.GetTempPath(), $"midwindow_kill_{Guid.NewGuid():N}");
            var dirControl = Path.Combine(Path.GetTempPath(), $"midwindow_control_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dirCrash);
            Directory.CreateDirectory(dirControl);
            try
            {
                using (var controlBundle = RocksDbChainStoreBundle.Open(dirControl, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dirControl)))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(controlBundle.State);
                    var controlExecutor = FollowerStackBuilder.BuildPathKeyed(
                        controlBundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(2));
                    for (int i = 0; i < blockCount; i++)
                        await ExecuteAndCommitMirrorAsync(controlBundle, controlExecutor, HiveTestdataFixture.Chain[i]);
                    await ((IAtomicBlockFlush)controlBundle).DrainAsync();
                    Assert.Equal(4UL, controlBundle.Metadata.GetLastBlock());
                    Assert.Equal(4UL, controlBundle.Metadata.GetDurableStateBlock());
                }

                using (var crashBundle = RocksDbChainStoreBundle.Open(dirCrash, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dirCrash)))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(crashBundle.State);
                    var crashExecutor = FollowerStackBuilder.BuildPathKeyed(
                        crashBundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(2));
                    for (int i = 0; i < 3; i++)
                        await ExecuteAndCommitMirrorAsync(crashBundle, crashExecutor, HiveTestdataFixture.Chain[i]);
                    await ((IAtomicBlockFlush)crashBundle).DrainAsync();
                    Assert.Equal(3UL, crashBundle.Metadata.GetLastBlock());
                    Assert.Equal(2UL, crashBundle.Metadata.GetDurableStateBlock());
                }

                using (var reopened = RocksDbChainStoreBundle.Open(dirCrash, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dirCrash)))
                {
                    var messages = new List<string>();
                    var (recoveredHead, recovered) = await reopened.EnsureConsistentHeadAsync(messages.Add);

                    Assert.Equal(2UL, recoveredHead);
                    Assert.True(recovered);
                    Assert.Equal(2UL, reopened.Metadata.GetLastBlock());
                    Assert.Equal(2UL, reopened.Metadata.GetDurableStateBlock());

                    var resumeExecutor = FollowerStackBuilder.BuildPathKeyed(
                        reopened, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(2));
                    for (int blockNumber = (int)recoveredHead + 1; blockNumber <= blockCount; blockNumber++)
                        await ExecuteAndCommitMirrorAsync(reopened, resumeExecutor, HiveTestdataFixture.Chain[blockNumber - 1]);
                    await ((IAtomicBlockFlush)reopened).DrainAsync();

                    Assert.Equal(4UL, reopened.Metadata.GetLastBlock());
                    Assert.Equal(4UL, reopened.Metadata.GetDurableStateBlock());
                }

                using var mgrCrash = new RocksDbManager(StorageOptionsFor(dirCrash));
                using var mgrControl = new RocksDbManager(StorageOptionsFor(dirControl));

                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_TRIE_STORAGE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_NODE_HISTORY);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_NODE_HISTORY_INDEX);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_METADATA);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_ACCOUNTS);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_STORAGE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_CODE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_STORAGE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_META);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_ROOT_INDEX);
                AssertCfIdentical(mgrControl, mgrCrash, HistoryColumnFamilies.BlockMeta);
            }
            finally
            {
                try { if (Directory.Exists(dirCrash)) Directory.Delete(dirCrash, recursive: true); } catch { }
                try { if (Directory.Exists(dirControl)) Directory.Delete(dirControl, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task RaggedTailKill_K16_MidTailDispose_ResumesFromDurableCursor_MatchesUninterruptedK16Run()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            const ulong k = 16;
            const ulong killAfterBlock = 596UL;

            var defaults = new MainnetChainServerConfig();
            var journalOptions = new HistoricalStateOptions
            {
                MaxHistoryBlocks = defaults.JournalBlocks,
                EnablePruning = true,
                PruningIntervalBlocks = Math.Max(64, defaults.JournalBlocks / 16),
            };
            RocksDbStorageOptions StorageOptionsFor(string dir) => new RocksDbStorageOptions
            {
                DatabasePath = dir,
                PathKeyedState = defaults.PathKeyedState,
                TrieNodeHistoryBlocks = defaults.TrieNodeHistoryBlocks,
                TrieNodeHistoryIndex = defaults.TrieNodeHistoryIndex,
            };

            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;
            Assert.True(lastChainBlock >= 500, $"expected the full Hive battery (~600 blocks), got {lastChainBlock}");
            Assert.True(lastChainBlock % (ulong)k != 0,
                $"test setup: K={k} must NOT divide the replay length, so it genuinely ends in a ragged tail");
            ulong lastBoundaryBelowEnd = (lastChainBlock / (ulong)k) * (ulong)k;
            Assert.True(killAfterBlock > lastBoundaryBelowEnd && killAfterBlock < lastChainBlock,
                "test setup: the kill point must sit strictly inside the ragged tail");

            var dirCrash = Path.Combine(Path.GetTempPath(), $"ragged16_crash_{Guid.NewGuid():N}");
            var dirControl = Path.Combine(Path.GetTempPath(), $"ragged16_control_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dirCrash);
            Directory.CreateDirectory(dirControl);
            try
            {
                using (var controlBundle = RocksDbChainStoreBundle.Open(dirControl, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dirControl)))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(controlBundle.State);
                    var controlExecutor = FollowerStackBuilder.BuildPathKeyed(
                        controlBundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(k));
                    foreach (var b in HiveTestdataFixture.Chain)
                        await ExecuteAndCommitMirrorAsync(controlBundle, controlExecutor, b);
                    await ((IAtomicBlockFlush)controlBundle).DrainAsync();
                    Assert.Equal(lastChainBlock, controlBundle.Metadata.GetLastBlock());
                    Assert.Equal(lastBoundaryBelowEnd, controlBundle.Metadata.GetDurableStateBlock());
                }

                using (var crashBundle = RocksDbChainStoreBundle.Open(dirCrash, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dirCrash)))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(crashBundle.State);
                    var crashExecutor = FollowerStackBuilder.BuildPathKeyed(
                        crashBundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(k));
                    foreach (var b in HiveTestdataFixture.Chain)
                    {
                        var blockNumber = (ulong)b.Header.BlockNumber;
                        if (blockNumber > killAfterBlock) break;
                        await ExecuteAndCommitMirrorAsync(crashBundle, crashExecutor, b);
                    }
                    await ((IAtomicBlockFlush)crashBundle).DrainAsync();
                    Assert.Equal(killAfterBlock, crashBundle.Metadata.GetLastBlock());
                    Assert.Equal(lastBoundaryBelowEnd, crashBundle.Metadata.GetDurableStateBlock());
                }

                using (var reopened = RocksDbChainStoreBundle.Open(dirCrash, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dirCrash)))
                {
                    var messages = new List<string>();
                    var (recoveredHead, recovered) = await reopened.EnsureConsistentHeadAsync(messages.Add);

                    Assert.Equal(lastBoundaryBelowEnd, recoveredHead);
                    Assert.True(recovered);
                    Assert.Equal(lastBoundaryBelowEnd, reopened.Metadata.GetLastBlock());
                    Assert.Equal(lastBoundaryBelowEnd, reopened.Metadata.GetDurableStateBlock());

                    var resumeExecutor = FollowerStackBuilder.BuildPathKeyed(
                        reopened, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(k));
                    foreach (var b in HiveTestdataFixture.Chain)
                    {
                        var blockNumber = (ulong)b.Header.BlockNumber;
                        if (blockNumber <= recoveredHead) continue;
                        await ExecuteAndCommitMirrorAsync(reopened, resumeExecutor, b);
                    }
                    await ((IAtomicBlockFlush)reopened).DrainAsync();

                    Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
                    Assert.Equal(lastBoundaryBelowEnd, reopened.Metadata.GetDurableStateBlock());

                    foreach (var b in HiveTestdataFixture.Chain)
                    {
                        var blockNumber = (ulong)b.Header.BlockNumber;
                        var stored = await reopened.Withdrawals.GetByBlockNumberAsync(blockNumber);
                        if (b.Withdrawals != null && b.Withdrawals.Count > 0)
                            AssertWithdrawalsEqual(b.Withdrawals, stored, blockNumber);
                    }
                }

                using var mgrCrash = new RocksDbManager(StorageOptionsFor(dirCrash));
                using var mgrControl = new RocksDbManager(StorageOptionsFor(dirControl));

                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_TRIE_STORAGE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_NODE_HISTORY);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_NODE_HISTORY_INDEX);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_METADATA);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_ACCOUNTS);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_STORAGE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_CODE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_ACCOUNTS);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_STORAGE);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_HISTORY_META);
                AssertCfIdentical(mgrControl, mgrCrash, RocksDbManager.CF_STATE_ROOT_INDEX);
                AssertCfIdentical(mgrControl, mgrCrash, HistoryColumnFamilies.BlockMeta);
            }
            finally
            {
                try { if (Directory.Exists(dirCrash)) Directory.Delete(dirCrash, recursive: true); } catch { }
                try { if (Directory.Exists(dirControl)) Directory.Delete(dirControl, recursive: true); } catch { }
            }
        }

        private static async Task<byte[]> ExecuteAndCommitMirrorAsync(
            IChainStoreBundle bundle, IBlockExecutor executor, BlockBundle b,
            bool armWithdrawals = true, bool skipWithdrawalsWrite = false)
        {
            var blockNumber = (ulong)b.Header.BlockNumber;
            var withdrawals = b.Withdrawals;
            if (armWithdrawals)
                (bundle as IAtomicBlockFlush)?.ArmWithdrawals(blockNumber, b.Withdrawals);
            var res = await executor.ProcessBlockAsync(b.Header, b.Transactions, b.Uncles, withdrawals, CancellationToken.None);
            Assert.True(res.RootMatches, $"block {blockNumber} should match its header");
            bool ownedByStagedFlush = bundle is IAtomicBlockFlush atomicFlushCursor && atomicFlushCursor.BlockOwnedByStagedFlush(blockNumber);
            if (!ownedByStagedFlush)
            {
                if (bundle is IAtomicBlockFlush atomicFlushDrain)
                    await atomicFlushDrain.DrainAsync();
                if (bundle.Metadata.GetLastBlock() < blockNumber)
                    bundle.Metadata.Commit(blockNumber, res.BlockHash);
            }
            if (!skipWithdrawalsWrite)
            {
                bool alreadyFolded = bundle is IAtomicBlockFlush atomicFlush && atomicFlush.WithdrawalsFoldedFor(blockNumber);
                if (!alreadyFolded && b.Withdrawals != null && b.Withdrawals.Count > 0)
                    await bundle.Withdrawals.SaveAsync(res.BlockHash, b.Withdrawals);
            }
            return res.BlockHash;
        }

        private static void AssertWithdrawalsEqual(IList<Withdrawal> expected, IList<Withdrawal> actual, ulong block)
        {
            Assert.True((expected == null) == (actual == null), $"block {block}: withdrawals nullness differs");
            if (expected == null) return;
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Index, actual[i].Index);
                Assert.Equal(expected[i].ValidatorIndex, actual[i].ValidatorIndex);
                Assert.True(expected[i].Address.AsSpan().SequenceEqual(actual[i].Address), $"block {block} withdrawal[{i}] address mismatch");
                Assert.Equal(expected[i].AmountInGwei, actual[i].AmountInGwei);
            }
        }

        private static async Task AssertNeverDurableWithoutWithdrawalsAsync(
            RocksDbChainStoreBundle bundle, ulong block, IList<Withdrawal> expectedWhenDurable)
        {
            var durable = bundle.Metadata.GetDurableStateBlock();
            if (durable < block) return;
            var stored = await bundle.Withdrawals.GetByBlockNumberAsync(block);
            Assert.True(stored != null,
                $"#78 CORE INVARIANT VIOLATED: durable cursor ({durable}) >= block {block} but its withdrawals are ABSENT " +
                "(block is already durable and will never be re-executed — this withdrawal list is permanently lost)");
            AssertWithdrawalsEqual(expectedWhenDurable, stored, block);
        }

        [Fact]
        public async Task BoundaryWithdrawalsKill_K1_Block17_NeverDurableWithoutWithdrawals()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            RocksDbStorageOptions StorageOptionsFor(string dir) => new RocksDbStorageOptions
            {
                DatabasePath = dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
                TrieNodeHistoryIndex = true,
            };
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 128, EnablePruning = false };

            const int targetBlock = 17;
            Assert.True(HiveTestdataFixture.Chain.Count >= targetBlock);
            var targetBundle = HiveTestdataFixture.Chain[targetBlock - 1];
            Assert.True(targetBundle.Withdrawals != null && targetBundle.Withdrawals.Count > 0,
                "test setup: block 17 must carry a non-empty withdrawal list (verified against the live Hive fixture)");

            var dbDir = Path.Combine(Path.GetTempPath(), $"boundary_wd_kill_k1_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dbDir);
            try
            {
                using (var bundle = RocksDbChainStoreBundle.Open(dbDir, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dbDir)))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);
                    var executor = FollowerStackBuilder.BuildPathKeyed(
                        bundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);

                    for (int i = 0; i < targetBlock - 1; i++)
                        await ExecuteAndCommitMirrorAsync(bundle, executor, HiveTestdataFixture.Chain[i]);

                    await ExecuteAndCommitMirrorAsync(bundle, executor, targetBundle, armWithdrawals: true, skipWithdrawalsWrite: true);
                    await ((IAtomicBlockFlush)bundle).DrainAsync();

                    Assert.Equal((ulong)targetBlock, bundle.Metadata.GetDurableStateBlock());
                    await AssertNeverDurableWithoutWithdrawalsAsync(bundle, (ulong)targetBlock, targetBundle.Withdrawals);
                }

                using (var reopened = RocksDbChainStoreBundle.Open(dbDir, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dbDir)))
                {
                    var messages = new List<string>();
                    var (head, recovered) = await reopened.EnsureConsistentHeadAsync(messages.Add);
                    Assert.Equal((ulong)targetBlock, head);
                    Assert.Equal((ulong)targetBlock, reopened.Metadata.GetDurableStateBlock());

                    await AssertNeverDurableWithoutWithdrawalsAsync(reopened, (ulong)targetBlock, targetBundle.Withdrawals);
                    var stored = await reopened.Withdrawals.GetByBlockNumberAsync(targetBlock);
                    AssertWithdrawalsEqual(targetBundle.Withdrawals, stored, targetBlock);
                }
            }
            finally
            {
                try { if (Directory.Exists(dbDir)) Directory.Delete(dbDir, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task BoundaryWithdrawalsKill_K4Boundary_Block28_NeverDurableWithoutWithdrawals()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            RocksDbStorageOptions StorageOptionsFor(string dir) => new RocksDbStorageOptions
            {
                DatabasePath = dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
                TrieNodeHistoryIndex = true,
            };
            var journalOptions = new HistoricalStateOptions { MaxHistoryBlocks = 128, EnablePruning = false };

            const int targetBlock = 28;
            Assert.True(HiveTestdataFixture.Chain.Count >= targetBlock);
            Assert.True(targetBlock % 4 == 0, "test setup: block 28 must be a real K=4 boundary");
            var targetBundle = HiveTestdataFixture.Chain[targetBlock - 1];
            Assert.True(targetBundle.Withdrawals != null && targetBundle.Withdrawals.Count > 0,
                "test setup: block 28 must carry a non-empty withdrawal list (verified against the live Hive fixture)");

            var dbDir = Path.Combine(Path.GetTempPath(), $"boundary_wd_kill_k4_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dbDir);
            try
            {
                using (var bundle = RocksDbChainStoreBundle.Open(dbDir, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dbDir)))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(bundle.State);
                    var executor = FollowerStackBuilder.BuildPathKeyed(
                        bundle, HiveTestdataFixture.ChainActivations, HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory, flushCadence: new FixedIntervalFlushCadence(4));

                    for (int i = 0; i < targetBlock - 1; i++)
                        await ExecuteAndCommitMirrorAsync(bundle, executor, HiveTestdataFixture.Chain[i]);
                    for (ulong n = 1; n < (ulong)targetBlock; n++)
                        await AssertNeverDurableWithoutWithdrawalsAsync(bundle, n, HiveTestdataFixture.Chain[(int)n - 1].Withdrawals);

                    await ExecuteAndCommitMirrorAsync(bundle, executor, targetBundle, armWithdrawals: true, skipWithdrawalsWrite: true);
                    await ((IAtomicBlockFlush)bundle).DrainAsync();

                    Assert.Equal((ulong)targetBlock, bundle.Metadata.GetDurableStateBlock());
                    await AssertNeverDurableWithoutWithdrawalsAsync(bundle, (ulong)targetBlock, targetBundle.Withdrawals);
                }

                using (var reopened = RocksDbChainStoreBundle.Open(dbDir, journalOptions: journalOptions, storageOptions: StorageOptionsFor(dbDir)))
                {
                    var messages = new List<string>();
                    var (head, recovered) = await reopened.EnsureConsistentHeadAsync(messages.Add);
                    Assert.Equal((ulong)targetBlock, head);
                    Assert.Equal((ulong)targetBlock, reopened.Metadata.GetDurableStateBlock());

                    await AssertNeverDurableWithoutWithdrawalsAsync(reopened, (ulong)targetBlock, targetBundle.Withdrawals);
                    var stored = await reopened.Withdrawals.GetByBlockNumberAsync(targetBlock);
                    AssertWithdrawalsEqual(targetBundle.Withdrawals, stored, targetBlock);
                }
            }
            finally
            {
                try { if (Directory.Exists(dbDir)) Directory.Delete(dbDir, recursive: true); } catch { }
            }
        }

        private sealed class AtomicFlushProbeBundle : IChainStoreBundle, IAtomicBlockFlush
        {
            private readonly IChainStoreBundle _inner;
            private readonly IAtomicBlockFlush _innerFlush;
            private int _flushCalls;

            public AtomicFlushProbeBundle(IChainStoreBundle inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _innerFlush = inner as IAtomicBlockFlush
                    ?? throw new InvalidOperationException(
                        $"{inner.GetType().Name} does not implement {nameof(IAtomicBlockFlush)} — the probe has nothing to count.");
            }

            private int _discardCalls;

            public int FlushCalls => _flushCalls;
            public int DiscardCalls => _discardCalls;

            Task IAtomicBlockFlush.FlushBlockAsync(FlatStateBatch flat, ulong block, byte[] hash)
            {
                Interlocked.Increment(ref _flushCalls);
                return _innerFlush.FlushBlockAsync(flat, block, hash);
            }

            void IAtomicBlockFlush.DiscardCapturedBlock()
            {
                Interlocked.Increment(ref _discardCalls);
                _innerFlush.DiscardCapturedBlock();
            }

            void IAtomicBlockFlush.ArmWithdrawals(ulong block, IList<Nethereum.Model.Withdrawal> withdrawals)
                => _innerFlush.ArmWithdrawals(block, withdrawals);
            bool IAtomicBlockFlush.WithdrawalsFoldedFor(ulong block)
                => _innerFlush.WithdrawalsFoldedFor(block);

            bool IAtomicBlockFlush.BlockOwnedByStagedFlush(ulong block)
                => _innerFlush.BlockOwnedByStagedFlush(block);
            Task IAtomicBlockFlush.DrainAsync()
                => _innerFlush.DrainAsync();

            public IStateStore State => _inner.State;
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
                => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
                => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
                => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();

            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        [Fact]
        public async Task ForwardSync_HiveChain_PathKeyed_ProductionConfig_AtomicFlush_StateRootsMatchCanonical()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            var defaults = new MainnetChainServerConfig();
            Assert.True(defaults.PathKeyedState, "test assumes the documented production default PathKeyedState=true");
            Assert.True(defaults.JournalBlocks > 0, "test assumes the documented production default JournalBlocks>0 (journal armed)");

            var storageOptions = new RocksDbStorageOptions
            {
                DatabasePath = _dataDir,
                PathKeyedState = defaults.PathKeyedState,
                TrieNodeHistoryBlocks = defaults.TrieNodeHistoryBlocks,
                TrieNodeHistoryIndex = defaults.TrieNodeHistoryIndex,
            };
            var journalOptions = new HistoricalStateOptions
            {
                MaxHistoryBlocks = defaults.JournalBlocks,
                EnablePruning = true,
                PruningIntervalBlocks = Math.Max(64, defaults.JournalBlocks / 16),
            };

            var manager = new RocksDbManager(storageOptions);
            try
            {
                using (var seedBundle = RocksDbChainStoreBundle.FromManager(manager, _dataDir, journalOptions: journalOptions, ownsManager: false))
                {
                    await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
                }

                var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
                var follower = new FollowerService();
                var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
                ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
                ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;
                Assert.True(expectedBlocks >= 500, $"expected the full Hive battery (~600 blocks), got {expectedBlocks}");

                RocksDbChainStoreBundle followBundle = null;
                AtomicFlushProbeBundle probe = null;
                var result = await follower.RunAsync(
                    source,
                    bundleFactory: () => followBundle = RocksDbChainStoreBundle.FromManager(manager, _dataDir, journalOptions: journalOptions, ownsManager: false),
                    executorFactory: bundle =>
                    {
                        probe = new AtomicFlushProbeBundle(bundle);
                        return FollowerStackBuilder.BuildPathKeyed(
                            probe,
                            HiveTestdataFixture.ChainActivations,
                            HiveTestdataFixture.HardforkConfigFactory,
                            HiveTestdataFixture.ChainConfigFactory);
                    },
                    policy: policy,
                    canonical: null,
                    options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                    ct: CancellationToken.None);

                Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
                Assert.Equal(expectedBlocks, result.BlocksExecuted);
                Assert.Equal(0UL, result.RootMismatches);
                Assert.Equal(lastChainBlock, result.LastExecutedBlock);

                Assert.NotNull(followBundle);
                Assert.IsType<HistoricalStateStore>(followBundle.State);
                Assert.IsType<CapturingJournalingPathNodeStore>(followBundle.StateTrieNodes);
                Assert.NotNull(followBundle.NodeCommitBlockSource);

                Assert.NotNull(probe);
                Assert.Equal((int)expectedBlocks, probe.FlushCalls);

                Assert.Equal(lastChainBlock, followBundle.Metadata.GetDurableStateBlock());

                followBundle.Dispose();
                followBundle = null;

                int nodeHistoryEntries = CountNodeHistoryEntries(manager);
                Assert.True(nodeHistoryEntries > 0,
                    "expected CF_NODE_HISTORY to contain entries after the production-config replay, proving the node-history journal ran");

                using var reopened = RocksDbChainStoreBundle.FromManager(manager, _dataDir, journalOptions: null, ownsManager: false);
                Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
                Assert.Equal(lastChainBlock, reopened.Metadata.GetDurableStateBlock());
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Fact]
        public async Task ForwardSync_WithCheckpointEvery50_CreatesMetadataAndSnapshotDirs()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;
            const ulong checkpointEvery = 50UL;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle => FollowerStackBuilder.Build(
                    bundle,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: checkpointEvery, AnchorEvery: 0),
                ct: System.Threading.CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(expectedBlocks, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());

            var expectedCheckpointBlocks = new System.Collections.Generic.List<ulong>();
            for (ulong b = checkpointEvery; b <= lastChainBlock; b += checkpointEvery)
            {
                expectedCheckpointBlocks.Add(b);
            }

            Assert.True(expectedCheckpointBlocks.Count > 0,
                "Hive chain should be long enough to produce at least one checkpoint at CheckpointEvery=50");

            foreach (var cpBlock in expectedCheckpointBlocks)
            {
                var snapshotDir = reopened.ResolveCheckpointSnapshotPath(cpBlock);
                Assert.True(Directory.Exists(snapshotDir),
                    $"Expected .cp/ snapshot dir at {snapshotDir} for checkpoint block {cpBlock}");

                var cp = reopened.Metadata.GetCheckpoint(cpBlock);
                Assert.NotNull(cp);
                Assert.Equal(cpBlock, cp!.Value.BlockNumber);
                Assert.NotNull(cp.Value.StateRoot);
                Assert.NotEmpty(cp.Value.StateRoot);

                ulong nonCheckpointBlock = cpBlock + 1;
                if (nonCheckpointBlock <= lastChainBlock && nonCheckpointBlock % checkpointEvery != 0)
                {
                    Assert.Null(reopened.Metadata.GetCheckpoint(nonCheckpointBlock));
                }
            }

            var listed = await reopened.ListCheckpointsAsync();
            var listedBlocks = listed.Select(c => c.BlockNumber).OrderBy(b => b).ToList();
            Assert.Equal(expectedCheckpointBlocks, listedBlocks);

            var metadataBlocks = reopened.Metadata.ListCheckpointBlockNumbers().OrderBy(b => b).ToList();
            Assert.Equal(expectedCheckpointBlocks, metadataBlocks);

            var archiveDir = Path.Combine(_dataDir, ".cp");
            Assert.True(Directory.Exists(archiveDir), $".cp archive dir should exist at {archiveDir}");
            var snapshotDirsOnDisk = Directory.EnumerateDirectories(archiveDir)
                .Select(p => ulong.Parse(Path.GetFileName(p)))
                .OrderBy(b => b)
                .ToList();
            Assert.Equal(expectedCheckpointBlocks, snapshotDirsOnDisk);
        }

        private sealed class PredicateTamperExecutor : IBlockExecutor
        {
            private readonly IBlockExecutor _inner;
            private readonly Func<ulong, bool> _shouldTamper;
            private int _tampered;

            public PredicateTamperExecutor(IBlockExecutor inner, Func<ulong, bool> shouldTamper)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _shouldTamper = shouldTamper ?? throw new ArgumentNullException(nameof(shouldTamper));
            }

            public int TamperedCalls => _tampered;

            public async Task<BlockImporterResult> ProcessBlockAsync(
                Nethereum.Model.BlockHeader header,
                System.Collections.Generic.IList<Nethereum.Model.ISignedTransaction> transactions,
                System.Collections.Generic.IList<Nethereum.Model.BlockHeader> uncles,
                System.Collections.Generic.IList<Withdrawal> withdrawals,
                CancellationToken ct)
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                    .ConfigureAwait(false);

                if (_shouldTamper((ulong)header.BlockNumber))
                {
                    Interlocked.Increment(ref _tampered);
                    result = new BlockImporterResult
                    {
                        Fork = result.Fork,
                        ComputedStateRoot = new byte[32],
                        ExpectedStateRoot = result.ExpectedStateRoot,
                        StateRootMismatch = true,
                        TransactionsExecuted = result.TransactionsExecuted,
                        MinerRewardCredited = result.MinerRewardCredited,
                        WithdrawalsCredited = result.WithdrawalsCredited,
                        BlockHash = result.BlockHash,
                        ErrorMessage = result.ErrorMessage,
                        Exception = result.Exception,
                        ExecutionResults = result.ExecutionResults,
                    };
                }

                return result;
            }
        }

        private sealed class LyingOnceExecutor : IBlockExecutor
        {
            private readonly IBlockExecutor _inner;
            private readonly ulong _targetBlock;
            private int _tampered;

            public LyingOnceExecutor(IBlockExecutor inner, ulong targetBlock)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _targetBlock = targetBlock;
            }

            public int TamperedCalls => _tampered;

            public async Task<BlockImporterResult> ProcessBlockAsync(
                Nethereum.Model.BlockHeader header,
                System.Collections.Generic.IList<Nethereum.Model.ISignedTransaction> transactions,
                System.Collections.Generic.IList<Nethereum.Model.BlockHeader> uncles,
                System.Collections.Generic.IList<Withdrawal> withdrawals,
                CancellationToken ct)
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                    .ConfigureAwait(false);

                if ((ulong)header.BlockNumber == _targetBlock
                    && Interlocked.CompareExchange(ref _tampered, 1, 0) == 0)
                {
                    result = new BlockImporterResult
                    {
                        Fork = result.Fork,
                        ComputedStateRoot = new byte[32],
                        ExpectedStateRoot = result.ExpectedStateRoot,
                        StateRootMismatch = true,
                        TransactionsExecuted = result.TransactionsExecuted,
                        MinerRewardCredited = result.MinerRewardCredited,
                        WithdrawalsCredited = result.WithdrawalsCredited,
                        BlockHash = result.BlockHash,
                        ErrorMessage = result.ErrorMessage,
                        Exception = result.Exception,
                        ExecutionResults = result.ExecutionResults,
                    };
                }

                return result;
            }
        }

        [Fact]
        public async Task Mismatch_PolicyRewindAndRetry_JournalUsed_FreshCalculatorMatches()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;

            Assert.Contains(HiveTestdataFixture.Chain, b => (ulong)b.Header.BlockNumber == targetMismatchBlock);

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

            LyingOnceExecutor lyingExecutor = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    if (lyingExecutor == null)
                    {
                        lyingExecutor = new LyingOnceExecutor(realExecutor, targetMismatchBlock);
                        return lyingExecutor;
                    }
                    return realExecutor;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(1UL, result.RewindCyclesUsed);
            Assert.Equal(1, lyingExecutor!.TamperedCalls);
            Assert.Equal(expectedBlocks + 1UL, result.BlocksExecuted);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Mismatch_PolicyFatal_ReturnsFatalVerdict_NoStateCorruption()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;
            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.Fatal };
            LyingOnceExecutor lyingExecutor = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    lyingExecutor ??= new LyingOnceExecutor(realExecutor, targetMismatchBlock);
                    return lyingExecutor;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(targetMismatchBlock - 1, result.LastExecutedBlock);
            Assert.Equal(targetMismatchBlock - 1, result.BlocksExecuted);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(0UL, result.RewindCyclesUsed);
            Assert.Equal(1, lyingExecutor!.TamperedCalls);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(targetMismatchBlock, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Mismatch_PolicyContinue_DoesNotCommitTamperedBlock()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;
            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.Continue };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;
            LyingOnceExecutor lyingExecutor = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    lyingExecutor ??= new LyingOnceExecutor(realExecutor, targetMismatchBlock);
                    return lyingExecutor;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(0UL, result.RewindCyclesUsed);
            Assert.Equal(1, lyingExecutor!.TamperedCalls);
            Assert.Equal(expectedBlocks - 1UL, result.BlocksExecuted);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);
        }

        [Fact]
        public async Task Mismatch_NoJournalNoSnapshot_ReturnsRewindUnavailable()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;
            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            PredicateTamperExecutor tamper = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    tamper ??= new PredicateTamperExecutor(realExecutor, bn => bn == targetMismatchBlock);
                    return tamper;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.RewindUnavailable, result.ExitReason);
            Assert.Equal(targetMismatchBlock - 1, result.LastExecutedBlock);
            Assert.Equal(targetMismatchBlock - 1, result.BlocksExecuted);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(1UL, result.RewindCyclesUsed);
            Assert.Equal(1, tamper!.TamperedCalls);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(targetMismatchBlock, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Mismatch_MaxConsecutiveDivergencesExceeded_ReturnsFatalVerdict()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong tamperStart = 10UL;
            const int maxConsecutive = 3;
            const int totalTampers = maxConsecutive + 1;
            ulong tamperEnd = tamperStart + (ulong)totalTampers - 1;

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.Continue };
            PredicateTamperExecutor tamper = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    tamper ??= new PredicateTamperExecutor(
                        realExecutor,
                        bn => bn >= tamperStart && bn <= tamperEnd);
                    return tamper;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(
                    StartBlock: 1,
                    CheckpointEvery: 0,
                    AnchorEvery: 0,
                    MaxConsecutiveDivergences: maxConsecutive),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(tamperStart - 1, result.LastExecutedBlock);
            Assert.Equal((ulong)totalTampers, result.RootMismatches);
            Assert.Equal(totalTampers, tamper!.TamperedCalls);
            Assert.Equal(0UL, result.RewindCyclesUsed);
            Assert.Contains("max consecutive divergences", result.Detail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Rewind_SnapshotUsed_ReturnsSnapshotRestoreRequested()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong checkpointEvery = 5UL;
            const ulong tamperBlock = 7UL;
            ulong expectedSnapshotBlock = (tamperBlock - 1UL) / checkpointEvery * checkpointEvery;

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            PredicateTamperExecutor tamper = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    tamper ??= new PredicateTamperExecutor(realExecutor, bn => bn == tamperBlock);
                    return tamper;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: checkpointEvery, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.SnapshotRestoreRequested, result.ExitReason);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(1UL, result.RewindCyclesUsed);
            Assert.Equal(1, tamper!.TamperedCalls);
            Assert.NotNull(result.SnapshotRestoreTarget);
            Assert.Equal(expectedSnapshotBlock, result.SnapshotRestoreTarget!.Value.BlockNumber);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            var snapshotDir = reopened.ResolveCheckpointSnapshotPath(expectedSnapshotBlock);
            Assert.True(Directory.Exists(snapshotDir),
                $"Snapshot dir for block {expectedSnapshotBlock} must remain on disk for the caller to consume");
        }

        [Fact]
        public async Task Rewind_MaxRewindCyclesExceeded_ReturnsFatalVerdict()
        {
            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong tamperBlock = 10UL;
            const int maxRewindCycles = 3;
            int factoryInvocations = 0;

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default),
                executorFactory: bundle =>
                {
                    factoryInvocations++;
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    return new PredicateTamperExecutor(realExecutor, bn => bn == tamperBlock);
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(
                    StartBlock: 1,
                    CheckpointEvery: 0,
                    AnchorEvery: 0,
                    MaxConsecutiveDivergences: maxRewindCycles + 10,
                    MaxRewindCycles: maxRewindCycles),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Contains("MaxRewindCycles", result.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Equal((ulong)(maxRewindCycles + 1), result.RewindCyclesUsed);
            Assert.Equal((ulong)(maxRewindCycles + 1), result.RootMismatches);
            Assert.Equal(maxRewindCycles + 1, factoryInvocations);
            Assert.Equal(tamperBlock - 1, result.LastExecutedBlock);
        }

        [Fact]
        public async Task ImporterMismatch_DoesNotPersistHeaderOrAdvanceHeight()
        {
            _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default);
            await HiveTestdataFixture.PopulateGenesisAsync(_activeBundle.State);

            var executor = FollowerStackBuilder.Build(
                _activeBundle,
                HiveTestdataFixture.ChainActivations,
                HiveTestdataFixture.HardforkConfigFactory,
                HiveTestdataFixture.ChainConfigFactory);

            const ulong targetBlock = 6UL;
            Assert.Contains(HiveTestdataFixture.Chain, b => (ulong)b.Header.BlockNumber == targetBlock);
            foreach (var b in HiveTestdataFixture.Chain)
            {
                if ((ulong)b.Header.BlockNumber >= targetBlock) break;
                var ok = await executor.ProcessBlockAsync(
                    b.Header, b.Transactions, b.Uncles,
                    b.Withdrawals, CancellationToken.None);
                Assert.True(ok.RootMatches, $"prefix block {b.Header.BlockNumber} should match its header");
            }

            Assert.Equal((BigInteger)(targetBlock - 1), await _activeBundle.Blocks.GetHeightAsync());

            var target = HiveTestdataFixture.Chain.First(b => (ulong)b.Header.BlockNumber == targetBlock);
            var provider = Nethereum.Model.RlpBlockEncodingProvider.Instance;
            var tamperedHeader = provider.DecodeBlockHeader(provider.EncodeBlockHeader(target.Header));
            tamperedHeader.StateRoot = new byte[32];

            var result = await executor.ProcessBlockAsync(
                tamperedHeader, target.Transactions, target.Uncles,
                target.Withdrawals, CancellationToken.None);

            Assert.True(result.StateRootMismatch);
            Assert.False(result.RootMatches);
            Assert.Null(result.BlockHash);

            Assert.Null(await _activeBundle.Blocks.GetByNumberAsync((BigInteger)targetBlock));
            Assert.Equal((BigInteger)(targetBlock - 1), await _activeBundle.Blocks.GetHeightAsync());
            var latest = await _activeBundle.Blocks.GetLatestAsync();
            Assert.NotNull(latest);
            Assert.Equal(targetBlock - 1, (ulong)latest.BlockNumber);
        }
    }
}
