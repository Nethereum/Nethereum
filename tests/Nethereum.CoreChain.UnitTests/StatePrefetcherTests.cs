using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public partial class StatePrefetcherTests
    {
        private const HardforkName Fork = HardforkName.Prague;
        private const string ContractAddress = "0x00000000000000000000000000000000000000c1";
        private const string StoresAndLogsCode = "600160005560006000a000";
        private static readonly ChainConfig Config = new ChainConfig
        {
            ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0
        };

        private sealed class PrefetchProbeStore : IStateStore
        {
            private readonly IStateStore _inner;
            private int _writes;

            public PrefetchProbeStore(IStateStore inner) { _inner = inner; }

            public Action OnPrefetchRead { get; set; }
            public Action OnRealRead { get; set; }
            public System.Collections.Concurrent.ConcurrentBag<string> PrefetchReads { get; } = new();
            public int Writes => Volatile.Read(ref _writes);

            private static bool OnPrefetchThread =>
                Thread.CurrentThread.Name?.StartsWith(StatePrefetcher.WorkerThreadNamePrefix) == true;

            private void Read(string what)
            {
                if (OnPrefetchThread)
                {
                    PrefetchReads.Add(what);
                    OnPrefetchRead?.Invoke();
                }
                else OnRealRead?.Invoke();
            }

            private void Write() => Interlocked.Increment(ref _writes);

            public Task<Account> GetAccountAsync(string address) { Read("account"); return _inner.GetAccountAsync(address); }
            public Task<bool> AccountExistsAsync(string address) { Read("exists"); return _inner.AccountExistsAsync(address); }
            public Task<byte[]> GetStorageAsync(string address, BigInteger slot) { Read("storage:" + address.ToLowerInvariant() + ":" + slot); return _inner.GetStorageAsync(address, slot); }
            public Task<byte[]> GetCodeAsync(byte[] codeHash) { Read("code"); return _inner.GetCodeAsync(codeHash); }
            public Task SaveAccountAsync(string address, Account account) { Write(); return _inner.SaveAccountAsync(address, account); }
            public Task DeleteAccountAsync(string address) { Write(); return _inner.DeleteAccountAsync(address); }
            public Task SaveStorageAsync(string address, BigInteger slot, byte[] value) { Write(); return _inner.SaveStorageAsync(address, slot, value); }
            public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value) { Write(); return _inner.SaveStorageByKeccakAsync(address, slotKeccak, value); }
            public Task ClearStorageAsync(string address) { Write(); return _inner.ClearStorageAsync(address); }
            public Task SaveCodeAsync(byte[] codeHash, byte[] code) { Write(); return _inner.SaveCodeAsync(codeHash, code); }
            public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
            public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
            public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
            public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
            public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
            public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
            public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
            public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
            public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
            public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();
        }

        private sealed class CapturingLoggerFactory : ILoggerFactory
        {
            public List<string> Messages { get; } = new();

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

            public void AddProvider(ILoggerProvider provider) { }

            public void Dispose() { }

            private sealed class CapturingLogger : ILogger
            {
                private readonly List<string> _messages;

                public CapturingLogger(List<string> messages) { _messages = messages; }

                public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel, EventId eventId, TState state, Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                    lock (_messages) _messages.Add(formatter(state, exception));
                }
            }
        }

        private static async Task SeedAsync(IStateStore store)
        {
            await store.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });
            var code = StoresAndLogsCode.HexToByteArray();
            var codeHash = Sha3Keccack.Current.CalculateHash(code);
            await store.SaveCodeAsync(codeHash, code);
            await store.SaveAccountAsync(ContractAddress, new Account { Balance = 0, Nonce = 1, CodeHash = codeHash });
        }

        private static ISignedTransaction CallToContract(BigInteger nonce) =>
            TransactionFactory.CreateTransaction(
                new LegacyTransactionSigner().SignTransaction(
                    BlockPipelineHarness.PrivateKey.HexToByteArray(), BlockPipelineHarness.ChainId, ContractAddress, 0, nonce,
                    BlockPipelineHarness.AboveInitialBaseFeeGasPrice, 100_000, ""));

        private static async Task<(BlockHeader Header, List<ISignedTransaction> Transactions)> ProduceBlockAsync()
        {
            var producer = await BlockPipelineHarness.CreateAsync(Fork);
            await SeedAsync(producer.StateStore);
            var transactions = new List<ISignedTransaction>
            {
                BlockPipelineHarness.Transfer(0),
                CallToContract(1),
                CallToContract(2),
                BlockPipelineHarness.Transfer(3),
                CallToContract(4),
                BlockPipelineHarness.Transfer(5)
            };
            var produced = await producer.ProduceAsync(transactions);
            return (produced.Header, transactions);
        }

        private static ExecutionStack BuildStack(
            InMemoryChainStoreBundle bundle, int prefetchWorkers, ILoggerFactory loggerFactory, IStateStore stateOverride = null) =>
            ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    PrefetchWorkers = prefetchWorkers,
                    LoggerFactory = loggerFactory,
                    StateOverride = stateOverride
                });

        private sealed record ImportedBlock(BlockImporterResult Result, string Receipts, string PrefetchLine);

        private static async Task<ImportedBlock> ImportAsync(
            BlockHeader header, List<ISignedTransaction> transactions, int prefetchWorkers, IStateStore stateOverride = null,
            Action afterSeeding = null)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var state = stateOverride ?? bundle.State;
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(state, Fork);
            await SeedAsync(state);
            await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, Fork, 30_000_000);
            afterSeeding?.Invoke();
            var loggers = new CapturingLoggerFactory();

            using var stack = BuildStack(bundle, prefetchWorkers, loggers, stateOverride);
            var result = await stack.Importer.ImportAsync(header, transactions, null, null, CancellationToken.None);

            var receipts = result.BlockHash == null
                ? ""
                : Describe(await bundle.Receipts.GetByBlockHashAsync(result.BlockHash));
            return new ImportedBlock(result, receipts, loggers.Messages.FirstOrDefault(m => m.StartsWith("block.prefetch.stats")));
        }

        private static string Describe(IEnumerable<Receipt> receipts) =>
            string.Join("|", receipts.Select(r =>
                $"{r.PostStateOrStatus.ToHex()}:{r.CumulativeGasUsed}:{r.Bloom.ToHex()}:" +
                string.Join(",", r.Logs.Select(l => $"{l.Address}/{l.Data.ToHex()}/{string.Join(".", l.Topics.Select(t => t.ToHex()))}"))));

        [Fact]
        public async Task Given_PrefetchWorkersConfigured_When_ABlockExecutes_Then_RootsAndReceiptsAreIdenticalToPrefetchOff()
        {
            var (header, transactions) = await ProduceBlockAsync();

            var off = await ImportAsync(header, transactions, prefetchWorkers: 0);
            Assert.True(off.Result.RootMatches, off.Result.DescribeRejection());
            Assert.Null(off.PrefetchLine);

            for (var round = 0; round < 5; round++)
            {
                var on = await ImportAsync(header, transactions, prefetchWorkers: 4);

                Assert.True(on.Result.RootMatches, on.Result.DescribeRejection());
                Assert.Equal(off.Result.ComputedStateRoot, on.Result.ComputedStateRoot);
                Assert.Equal(off.Result.BlockHash, on.Result.BlockHash);
                Assert.Equal(off.Receipts, on.Receipts);
                Assert.NotEmpty(on.Receipts);
                Assert.Contains("txs=6", on.PrefetchLine);
                Assert.Contains("planned=True", on.PrefetchLine);
            }
        }

        [Fact]
        public async Task Given_APrefetcherThatCannotPlanTheBlock_When_TheBlockIsImported_Then_ItImportsAndReportsNotPlannedWithoutAFailedTransaction()
        {
            var (header, transactions) = await ProduceBlockAsync();
            using var bundle = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, Fork);
            await SeedAsync(bundle.State);
            await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, Fork, 30_000_000);
            var prefetcher = new StatePrefetcher(
                bundle.State, bundle.Blocks, new FixedChainActivations(Fork),
                _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers: 2);
            prefetcher.Dispose();
            var loggers = new CapturingLoggerFactory();

            using var stack = ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    Prefetcher = prefetcher,
                    LoggerFactory = loggers
                });
            var result = await stack.Importer.ImportAsync(header, transactions, null, null, CancellationToken.None);

            Assert.True(result.RootMatches, result.DescribeRejection());
            var line = loggers.Messages.Single(m => m.StartsWith("block.prefetch.stats"));
            Assert.Contains("planned=False", line);
            Assert.Contains("failed=0", line);
            Assert.Contains("prefetched=0", line);
            Assert.Contains("skipped=6", line);
        }

        private static PrefetchProbeStore ProbedState(out InMemoryStateStore inner)
        {
            inner = new InMemoryStateStore();
            return new PrefetchProbeStore(inner);
        }

        private static async Task<StatePrefetcher> PrefetcherOverAsync(PrefetchProbeStore state, InMemoryStateStore inner, int workers)
        {
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(inner, Fork);
            await SeedAsync(inner);
            await inner.ClearDirtyTrackingAsync();
            return new StatePrefetcher(
                state, new InMemoryBlockStore(), new FixedChainActivations(Fork),
                _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers);
        }

        [Fact]
        public async Task Given_APrefetchInFlight_When_TheBlockCompletes_Then_ThePrefetchStopsAndNothingWasWrittenToTheStore()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var state = ProbedState(out var inner);
            using var prefetcher = await PrefetcherOverAsync(state, inner, workers: 2);
            using var gate = new ManualResetEventSlim(false);
            using var firstReadsReached = new CountdownEvent(2);
            state.OnPrefetchRead = () =>
            {
                if (!firstReadsReached.IsSet) firstReadsReached.Signal();
                gate.Wait();
            };

            var session = prefetcher.Begin(header, transactions);
            Assert.True(firstReadsReached.Wait(TimeSpan.FromSeconds(30)));
            var atStop = session.Stop();
            gate.Set();
            await session.Completed.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(transactions.Count - 2, atStop.Skipped);
            Assert.Equal(0, session.Outcome.Prefetched);
            Assert.Equal(transactions.Count, session.Outcome.Skipped);
            Assert.Equal(0, session.Outcome.Failed);
            Assert.Equal(0, state.Writes);
            Assert.Empty(await state.GetDirtyAccountAddressesAsync());
        }

        [Fact]
        public async Task Given_ABlockOfTransactions_When_APrefetchRunsToTheEnd_Then_EveryTransactionIsPrefetchedAndNothingWasWrittenToTheStore()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var state = ProbedState(out var inner);
            using var prefetcher = await PrefetcherOverAsync(state, inner, workers: 3);
            var storedBefore = await inner.GetStorageAsync(ContractAddress, BigInteger.Zero);

            var session = prefetcher.Begin(header, transactions);
            await session.Completed.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(new PrefetchOutcome(transactions.Count, 0, 0), session.Outcome);
            Assert.Contains("code", state.PrefetchReads);
            Assert.Contains("storage:" + ContractAddress.ToLowerInvariant() + ":0", state.PrefetchReads);
            Assert.Equal(0, state.Writes);
            Assert.Empty(await state.GetDirtyAccountAddressesAsync());
            Assert.Equal(storedBefore, await inner.GetStorageAsync(ContractAddress, BigInteger.Zero));
            Assert.Equal((EvmUInt256)0, (await inner.GetAccountAsync(BlockPipelineHarness.SenderAddress)).Nonce);
        }

        [Fact]
        public async Task Given_APrefetchWorkerThrows_When_TheBlockExecutes_Then_TheBlockStillImportsAndTheFailureIsCounted()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var state = ProbedState(out var inner);
            using var prefetcher = await PrefetcherOverAsync(state, inner, workers: 2);
            state.OnPrefetchRead = () => throw new InvalidOperationException("the disk failed");

            var session = prefetcher.Begin(header, transactions);
            await session.Completed.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(new PrefetchOutcome(0, 0, transactions.Count), session.Outcome);

            using var aWorkerThrew = new ManualResetEventSlim(false);
            var probed = ProbedState(out _);
            probed.OnPrefetchRead = () =>
            {
                aWorkerThrew.Set();
                throw new InvalidOperationException("the disk failed");
            };
            var imported = await ImportAsync(
                header, transactions, prefetchWorkers: 3, stateOverride: probed,
                afterSeeding: () => probed.OnRealRead = () => aWorkerThrew.Wait(TimeSpan.FromSeconds(30)));

            Assert.True(aWorkerThrew.IsSet);
            Assert.True(imported.Result.RootMatches, imported.Result.DescribeRejection());
            Assert.Null(imported.Result.Exception);
            Assert.NotNull(imported.PrefetchLine);
        }

        [Fact]
        public void Given_AnExecutionStackRebuiltRepeatedly_When_PrefetchIsOn_Then_NoPrefetchThreadsAccumulate()
        {
            var prefetchers = new List<StatePrefetcher>();

            for (var rebuild = 0; rebuild < 10; rebuild++)
            {
                using var bundle = InMemoryChainStoreBundle.Open();
                var stack = BuildStack(bundle, 3, new CapturingLoggerFactory());
                prefetchers.Add(stack.Prefetcher);
                Assert.Equal(3, stack.Prefetcher.LiveWorkerThreads);
                stack.Dispose();
                Assert.Equal(0, stack.Prefetcher.LiveWorkerThreads);
            }

            Assert.Equal(0, prefetchers.Sum(p => p.LiveWorkerThreads));
        }

        [Fact]
        public void Given_AFollowerExecutorRebuiltRepeatedly_When_ASharedPrefetcherIsSupplied_Then_NoThreadsAreAdded()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var prefetcher = new StatePrefetcher(
                bundle.State, bundle.Blocks, new FixedChainActivations(Fork),
                _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers: 3);

            for (var rebuild = 0; rebuild < 10; rebuild++)
            {
                FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                    bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                    NoRewardPolicy.Instance, null, null, null, prefetcher);
                Assert.Equal(3, prefetcher.LiveWorkerThreads);
            }

            prefetcher.Dispose();
            Assert.Equal(0, prefetcher.LiveWorkerThreads);
        }

        [Fact]
        public async Task Given_ANodeStopping_When_APrefetchIsInFlight_Then_StopCompletesWithinTheBound()
        {
            var (header, transactions) = await ProduceBlockAsync();
            var state = ProbedState(out var inner);
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(inner, Fork);
            await SeedAsync(inner);
            var bound = TimeSpan.FromMilliseconds(500);
            var prefetcher = new StatePrefetcher(
                state, new InMemoryBlockStore(), new FixedChainActivations(Fork),
                _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers: 2, shutdownBound: bound);
            using var gate = new ManualResetEventSlim(false);
            using var readsReached = new CountdownEvent(2);
            state.OnPrefetchRead = () =>
            {
                if (!readsReached.IsSet) readsReached.Signal();
                gate.Wait();
            };
            var session = prefetcher.Begin(header, transactions);
            Assert.True(readsReached.Wait(TimeSpan.FromSeconds(30)));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            prefetcher.Dispose();
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < bound + TimeSpan.FromSeconds(3), stopwatch.Elapsed.ToString());
            Assert.Equal(2, prefetcher.LiveWorkerThreads);
            Assert.Null(prefetcher.Begin(header, transactions));

            gate.Set();
            await session.Completed.WaitAsync(TimeSpan.FromSeconds(30));
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (prefetcher.LiveWorkerThreads != 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
            Assert.Equal(0, prefetcher.LiveWorkerThreads);
            Assert.Equal(0, state.Writes);
        }

        [Fact]
        public void Given_NoWorkers_When_APrefetcherIsConstructed_Then_ItIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatePrefetcher(
                new InMemoryStateStore(), new InMemoryBlockStore(), new FixedChainActivations(Fork),
                _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers: 0));
        }

        [Fact]
        public void Given_ANegativeWorkerCount_When_TheStackIsBuilt_Then_ItIsRefused()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            Assert.Throws<ArgumentOutOfRangeException>(() => BuildStack(bundle, -1, new CapturingLoggerFactory()));
        }
    }
}
