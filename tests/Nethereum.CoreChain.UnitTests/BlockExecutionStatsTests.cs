using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockExecutionStatsTests
    {
        private const string ContractAddress = "0x00000000000000000000000000000000000000c1";
        private const string ReadsTwoSlotsCode = "6000546000545050600154" + "5000";
        private const string CallsIdentityPrecompileAddress = "0x00000000000000000000000000000000000000c2";
        private const string CallsIdentityPrecompileCode = "6000600060006000600060045af150" + "00";
        private const HardforkName Fork = HardforkName.Prague;

        private sealed class ReadRecordingStateStore : IStateStore
        {
            private readonly IStateStore _inner;
            public HashSet<string> Accounts { get; } = new();
            public HashSet<(string, BigInteger)> Slots { get; } = new();

            public ReadRecordingStateStore(IStateStore inner) { _inner = inner; }

            private static string Key(string address) => AddressUtil.Current.ConvertToValid20ByteAddressLowerCase(address);

            public Task<Account> GetAccountAsync(string address) { Accounts.Add(Key(address)); return _inner.GetAccountAsync(address); }
            public Task<Account> GetAccountAsync(EvmAddress address) { Accounts.Add(Key(address.ToHexLower())); return _inner.GetAccountAsync(address); }
            public Task<bool> AccountExistsAsync(string address) { Accounts.Add(Key(address)); return _inner.AccountExistsAsync(address); }
            public Task<bool> AccountExistsAsync(EvmAddress address) { Accounts.Add(Key(address.ToHexLower())); return _inner.AccountExistsAsync(address); }
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) { Slots.Add((Key(address), slot)); return _inner.GetStorageAsync(address, slot); }
            public Task<byte[]> GetStorageAsync(EvmAddress address, BigInteger slot) { Slots.Add((Key(address.ToHexLower()), slot)); return _inner.GetStorageAsync(address, slot); }
            public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task SaveAccountAsync(EvmAddress address, Account account) => _inner.SaveAccountAsync(address, account);
            public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
            public Task DeleteAccountAsync(EvmAddress address) => _inner.DeleteAccountAsync(address);
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageAsync(EvmAddress address, BigInteger slot, byte[] value) => _inner.SaveStorageAsync(address, slot, value);
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task SaveStorageByKeccakAsync(EvmAddress address, byte[] slotKeccak, byte[] value) => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(EvmAddress address) => _inner.GetAllStorageAsync(address);
            public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
            public Task ClearStorageAsync(EvmAddress address) => _inner.ClearStorageAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
        }

        private sealed class CapturingLogger : ILogger<BlockImporter>
        {
            public List<string> Messages { get; } = new();
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                lock (Messages) Messages.Add(formatter(state, exception));
            }
        }

        private sealed class Pipeline
        {
            public InMemoryStateStore State { get; init; }
            public ReadRecordingStateStore Spy { get; init; }
            public InMemoryBlockStore Blocks { get; init; }
            public BufferedFlatStateStore Buffered { get; init; }
            public BlockExecutor Engine { get; init; }
            public BlockImporter Importer { get; init; }
        }

        private static async Task SeedAsync(IStateStore store)
        {
            await store.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });
            var code = ReadsTwoSlotsCode.HexToByteArray();
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            await store.SaveCodeAsync(codeHash, code);
            await store.SaveAccountAsync(ContractAddress, new Account { Balance = 0, Nonce = 1, CodeHash = codeHash });
            var callerCode = CallsIdentityPrecompileCode.HexToByteArray();
            var callerCodeHash = Sha3Keccack.Current.CalculateHash(callerCode);
            await store.SaveCodeAsync(callerCodeHash, callerCode);
            await store.SaveAccountAsync(CallsIdentityPrecompileAddress, new Account { Balance = 0, Nonce = 1, CodeHash = callerCodeHash });
        }

        private static async Task<Pipeline> BuildPipelineAsync(bool historical = false, bool plain = false, ILogger<BlockImporter> logger = null)
        {
            var state = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(state, Fork);
            await SeedAsync(state);

            var blocks = new InMemoryBlockStore();
            await ShapedHeaders.SaveGenesisAsync(blocks, Fork, 30_000_000);
            var config = new ChainConfig { ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var trieNodes = new InMemoryContentNodeStore();
            var spy = new ReadRecordingStateStore(state);
            var buffered = new BufferedFlatStateStore(spy);
            IStateStore executionStore = plain ? state : historical ? new HistoricalStateStore(buffered) : buffered;
            var calculator = new IncrementalStateRootCalculator(executionStore, trieNodes);
            var engine = new BlockExecutor(
                executionStore, blocks, new FixedChainActivations(Fork),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: calculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodes);
            return new Pipeline
            {
                State = state,
                Spy = spy,
                Blocks = blocks,
                Buffered = buffered,
                Engine = engine,
                Importer = new BlockImporter(engine, blocks, executionStore, logger: logger)
            };
        }

        private static ISignedTransaction CallToContract(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    BlockPipelineHarness.PrivateKey.HexToByteArray(), BlockPipelineHarness.ChainId, ContractAddress, 0, nonce,
                    BlockPipelineHarness.AboveInitialBaseFeeGasPrice, 100_000, ""));

        private static ISignedTransaction CallTo(string address, BigInteger nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    BlockPipelineHarness.PrivateKey.HexToByteArray(), BlockPipelineHarness.ChainId, address, 0, nonce,
                    BlockPipelineHarness.AboveInitialBaseFeeGasPrice, 100_000, ""));

        private static async Task<(BlockHeader Header, List<ISignedTransaction> Transactions)> ProduceBlockAsync(List<ISignedTransaction> transactions = null)
        {
            var producerHarness = await BlockPipelineHarness.CreateAsync(Fork);
            await SeedAsync(producerHarness.StateStore);
            transactions ??= new List<ISignedTransaction> { CallToContract(0), BlockPipelineHarness.Transfer(1) };
            var produced = await producerHarness.ProduceAsync(transactions);
            return (produced.Header, transactions);
        }

        private static async Task<(Pipeline Pipeline, BlockImporterResult Result, BlockHeader Header)> ImportAsync(
            bool historical = false, bool plain = false, ILogger<BlockImporter> logger = null, List<ISignedTransaction> block = null)
        {
            var (header, transactions) = await ProduceBlockAsync(block);
            var pipeline = await BuildPipelineAsync(historical, plain, logger);
            pipeline.Spy.Accounts.Clear();
            pipeline.Spy.Slots.Clear();
            var result = await pipeline.Importer.ImportAsync(header, transactions, uncles: null, withdrawals: null, CancellationToken.None);
            Assert.True(result.RootMatches, result.DescribeRejection());
            return (pipeline, result, header);
        }

        [Fact]
        public async Task Given_ABlockWithStateReads_When_Executed_Then_TheStatsCountTheAccountsAndSlotsLoaded()
        {
            var (pipeline, result, _) = await ImportAsync();

            var stats = result.Stats;
            Assert.NotNull(stats);
            Assert.Equal(pipeline.Spy.Accounts.Count, stats.AccountLoaded);
            Assert.Equal(pipeline.Spy.Slots.Count, stats.StorageLoaded);
            Assert.Contains(BlockPipelineHarness.SenderAddress.ToLowerInvariant(), pipeline.Spy.Accounts);
            Assert.Contains(ContractAddress, pipeline.Spy.Accounts);
            Assert.Contains((ContractAddress, BigInteger.Zero), pipeline.Spy.Slots);
            Assert.Contains((ContractAddress, BigInteger.One), pipeline.Spy.Slots);
            Assert.Equal(2, pipeline.Spy.Slots.Count(s => s.Item1 == ContractAddress));
            Assert.Equal(2, stats.TransactionCount);
            Assert.True(stats.GasUsed > 0);
        }

        [Fact]
        public async Task Given_ABlock_When_Executed_Then_ExecutionExcludesStateReadTimeAsGethDefinesIt()
        {
            var (_, result, _) = await ImportAsync();

            var stats = result.Stats;
            Assert.NotNull(stats);
            var reads = stats.AccountReads + stats.StorageReads + stats.CodeReads;
            Assert.True(stats.Execution + reads <= stats.TotalTime);
            Assert.True(stats.Execution + stats.Validation + stats.DatabaseCommit + stats.BlockWrite <= stats.TotalTime);
            foreach (var duration in new[]
            {
                stats.AccountReads, stats.StorageReads, stats.CodeReads, stats.AccountUpdates, stats.AccountHashes,
                stats.AccountCommits, stats.StorageUpdates, stats.StorageCommits, stats.Execution, stats.Validation,
                stats.DatabaseCommit, stats.BlockWrite, stats.TotalTime
            })
            {
                Assert.True(duration >= TimeSpan.Zero);
            }
            Assert.True(stats.TotalTime > TimeSpan.Zero);
            Assert.True(stats.MgasPerSecond > 0);
            Assert.True(stats.AccountUpdated > 0);
            Assert.True(stats.AccountUpdates > TimeSpan.Zero);
            Assert.True(stats.AccountHashes > TimeSpan.Zero);
        }

        [Fact]
        public async Task Given_ABlockExecutedWithStats_When_ComparedToBefore_Then_ReceiptsAndStateRootAreIdentical()
        {
            var (header, _) = await ProduceBlockAsync();
            var (pipeline, result, _) = await ImportAsync();

            Assert.True(result.RootMatches, result.DescribeRejection());
            Assert.Equal(header.StateRoot, result.ComputedStateRoot);
            Assert.False(result.ReceiptsRootMismatch);
            Assert.False(result.LogsBloomMismatch);
            Assert.False(result.GasUsedMismatch);

            var recomputed = await new IncrementalStateRootCalculator(pipeline.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.Equal(header.StateRoot, recomputed);
        }

        [Fact]
        public async Task Given_TheMainnetStoreChain_When_ABlockExecutes_Then_EveryCapabilityOfTheStateStoreIsVisibleToTheExecutor()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var pipeline = await BuildPipelineAsync(historical: true);
            pipeline.Spy.Accounts.Clear();
            pipeline.Spy.Slots.Clear();

            var result = await pipeline.Engine.ExecuteAsync(
                header, transactions.Select(t => new TxEntry(t, null)).ToList(), null, null,
                new BlockExecutionOptions { Role = BlockExecutionRole.Validating }, CancellationToken.None);

            Assert.False(result.ExecutionValidityMismatch, string.Join(",", result.FailedChecks));
            Assert.NotNull(result.Stats);
            Assert.True(pipeline.Buffered.AccountReads > 0);
            Assert.True(pipeline.Buffered.StorageReads > 0);
            Assert.Equal(pipeline.Spy.Accounts.Count, result.Stats.AccountLoaded);
            Assert.Equal(pipeline.Spy.Slots.Count, result.Stats.StorageLoaded);
        }

        private static string SingleStatsLine(CapturingLogger log)
        {
            var lines = log.Messages.Where(m => m.StartsWith("block.exec.stats")).ToList();
            Assert.Single(lines);
            return lines[0];
        }

        private static readonly string[] GethFields =
        {
            "total_ms=", "execution_ms=", "account_reads_ms=", "storage_reads_ms=",
            "db_commit_ms=", "block_write_ms=", "accounts_loaded=", "storage_loaded="
        };

        private static long Field(string line, string name) =>
            long.Parse(line.Split(' ').Single(t => t.StartsWith(name + "=")).Substring(name.Length + 1));

        [Fact]
        public async Task Given_ABlockImportedThroughTheImporter_When_ItCommits_Then_OneBlockExecStatsLineIsLoggedWithGethFieldNames()
        {
            var log = new CapturingLogger();
            var (_, _, header) = await ImportAsync(logger: log);

            var line = SingleStatsLine(log);
            Assert.Contains("block=" + header.BlockNumber + " ", line);
            foreach (var field in GethFields) Assert.Contains(field, line);
            Assert.True(Field(line, "accounts_loaded") > 0);
        }

        [Fact]
        public async Task Given_AStateChainWithoutTheReadMeter_When_ABlockIsImported_Then_NoStatsLineIsLogged()
        {
            var log = new CapturingLogger();
            var (_, result, _) = await ImportAsync(plain: true, logger: log);

            Assert.Null(result.Stats);
            Assert.DoesNotContain(log.Messages, m => m.StartsWith("block.exec.stats"));
        }


        private static List<ISignedTransaction> BlockCallingPrecompiles() => new List<ISignedTransaction>
        {
            CallTo("0x0000000000000000000000000000000000000001", 0),
            CallTo("0x0000000000000000000000000000000000000007", 1),
            CallTo("0x0000000000000000000000000000000000000001", 2),
            CallTo(CallsIdentityPrecompileAddress, 3),
            CallTo("0x0000000000000000000000000000000000000007", 4),
            CallTo("0x0000000000000000000000000000000000000001", 5),
            CallTo(ContractAddress, 6)
        };

        [Fact]
        public async Task Given_ABlockWithKnownPrecompileCalls_When_Executed_Then_TheStatsReportTheExactCountPerPrecompile()
        {
            var (_, result, _) = await ImportAsync(block: BlockCallingPrecompiles());

            Assert.Equal(new Dictionary<int, int> { [1] = 3, [4] = 1, [7] = 2 }, result.Stats.PrecompileCalls.ToDictionary(c => c.Key, c => c.Value));
            Assert.Equal("01:3,04:1,07:2", result.Stats.PrecompileCallsKey);
        }

        [Fact]
        public async Task Given_ABlockWithKnownPrecompileCalls_When_ItCommits_Then_TheStatsLineListsOnlyTheNonZeroPrecompilesAndKeepsEveryExistingKey()
        {
            var log = new CapturingLogger();
            await ImportAsync(logger: log, block: BlockCallingPrecompiles());

            var line = SingleStatsLine(log);
            Assert.EndsWith(" precompiles=01:3,04:1,07:2", line);
            foreach (var field in GethFields) Assert.Contains(field, line);
        }

        [Fact]
        public async Task Given_ABlockWithoutPrecompileCalls_When_ItCommits_Then_TheStatsLineReportsNoPrecompiles()
        {
            var log = new CapturingLogger();
            var (_, result, _) = await ImportAsync(logger: log);

            Assert.Empty(result.Stats.PrecompileCalls);
            Assert.EndsWith(" precompiles=-", SingleStatsLine(log));
        }
    }
}
