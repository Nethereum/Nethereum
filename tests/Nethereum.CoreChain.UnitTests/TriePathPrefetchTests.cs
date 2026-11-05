using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class TriePathPrefetchTests
    {
        private const HardforkName Fork = HardforkName.Prague;
        private const int WorkerCount = 3;
        private static readonly ChainConfig Config = new ChainConfig
        {
            ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0
        };

        private sealed class ObservedNodeStore : ITrieNodeStore
        {
            private readonly ConcurrentDictionary<string, byte[]> _nodes = new();
            private int _reads;
            private int _commits;
            private int _activeReads;

            public Action OnWorkerRead { get; set; }
            public bool WorkerReadsThrow { get; set; }
            public int Reads => Volatile.Read(ref _reads);
            public int Commits => Volatile.Read(ref _commits);
            public int ActiveReads => Volatile.Read(ref _activeReads);

            public void Commit(TrieNodeSet nodes)
            {
                Interlocked.Increment(ref _commits);
                foreach (var node in nodes.Nodes)
                {
                    var rlp = node.GetEncodedData();
                    if (rlp != null && rlp.Length >= 32) _nodes[Key(node.Owner, node.Path)] = rlp;
                }
                foreach (var delete in nodes.Deletes) _nodes.TryRemove(Key(delete.Owner, delete.Path), out _);
            }

            public byte[] Get(Node reference)
            {
                Interlocked.Increment(ref _reads);
                Interlocked.Increment(ref _activeReads);
                try
                {
                    if (IsWorker())
                    {
                        if (WorkerReadsThrow) throw new InvalidOperationException("node read failed");
                        OnWorkerRead?.Invoke();
                    }
                    return _nodes.TryGetValue(Key(reference.Owner, reference.Path), out var rlp) ? rlp : null;
                }
                finally
                {
                    Interlocked.Decrement(ref _activeReads);
                }
            }

            public bool Contains(Node reference) => _nodes.ContainsKey(Key(reference.Owner, reference.Path));
            public bool ContainsKey(byte[] stateRoot) => false;
            public void Flush() { }
            public void Clear() { }

            private static bool IsWorker()
                => (Thread.CurrentThread.Name ?? "").StartsWith(StatePrefetcher.WorkerThreadNamePrefix, StringComparison.Ordinal);

            private static string Key(byte[] owner, byte[] path) =>
                Convert.ToHexString(owner ?? Array.Empty<byte>()) + "/" + Convert.ToHexString(path ?? Array.Empty<byte>());
        }

        private sealed class CountingObserver : IDirtyKeyObserver
        {
            private int _accounts;
            public int Accounts => Volatile.Read(ref _accounts);
            public void OnAccountDirty(Nethereum.Util.EvmAddress address) => Interlocked.Increment(ref _accounts);
            public void OnStorageSlotDirty(Nethereum.Util.EvmAddress address, System.Numerics.BigInteger slot) { }
        }

        private sealed class Chain
        {
            public Chain()
            {
                State = new BufferedFlatStateStore(new InMemoryStateStore());
                State.BeginBuffering();
                Nodes = new ObservedNodeStore();
                Calculator = new IncrementalStateRootCalculator(State, Nodes, emitTombstones: true);
            }

            public BufferedFlatStateStore State { get; }
            public ObservedNodeStore Nodes { get; }
            public IncrementalStateRootCalculator Calculator { get; }

            public async Task<byte[]> GenesisAsync(int accounts, int slots)
            {
                for (var i = 1; i <= accounts; i++)
                {
                    await State.SaveAccountAsync(AddressOf(i), new Account { Balance = i, Nonce = 1 });
                    for (var slot = 1; slot <= slots; slot++)
                        await State.SaveStorageAsync(AddressOf(i), slot, new byte[] { (byte)i, (byte)slot });
                }
                var built = await Calculator.ComputeStateRootAsync();
                for (var i = 1; i <= accounts; i++) await TouchAsync(i, 1, 0x01);
                return await Calculator.ComputeStateRootAsync(built);
            }

            public async Task TouchAsync(int account, int slot, byte value)
            {
                var existing = await State.GetAccountAsync(AddressOf(account));
                existing.Nonce += 1;
                await State.SaveAccountAsync(AddressOf(account), existing);
                await State.SaveStorageAsync(AddressOf(account), slot, new byte[] { value, value });
            }

            public StatePrefetcher NewPrefetcher(int workers, TimeSpan? shutdownBound = null)
            {
                return new StatePrefetcher(
                    State, new InMemoryBlockStore(), new FixedChainActivations(Fork),
                    _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers, shutdownBound);
            }
        }

        private static string AddressOf(int index) => "0x" + index.ToString("x40");

        private static PrefetchSession BeginSession(StatePrefetcher prefetcher, Chain chain, byte[] parentRoot)
        {
            var session = prefetcher.Begin(new BlockHeader { BlockNumber = 2, Timestamp = 1_000 }, new List<ISignedTransaction>());
            Assert.True(session.StartTriePaths(chain.State, parentRoot, chain.Calculator));
            return session;
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(5);
            Assert.True(condition());
        }

        private static Task WalksSettledAsync(PrefetchSession session) => WaitUntilAsync(() =>
        {
            var outcome = session.TriePathOutcome;
            return outcome.Enqueued > 0 && outcome.Warmed + outcome.Failed + outcome.Skipped == outcome.Enqueued;
        });

        private static async Task<(byte[] Root, int ComputeReads, TriePathOutcome Outcome)> SecondBlockAsync(bool prefetch)
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(25, 3);
            using var prefetcher = prefetch ? chain.NewPrefetcher(WorkerCount) : null;
            var session = prefetch ? BeginSession(prefetcher, chain, parentRoot) : null;

            for (var i = 1; i <= 25; i += 2) await chain.TouchAsync(i, 2, 0xAB);
            if (session != null)
            {
                await WalksSettledAsync(session);
                session.EndTriePathsBeforeRoot();
            }
            var readsBefore = chain.Nodes.Reads;
            var root = await chain.Calculator.ComputeStateRootAsync(parentRoot);
            return (root, chain.Nodes.Reads - readsBefore, session?.TriePathOutcome ?? default);
        }

        private static async Task<byte[]> RecreatedAccountRootAsync(bool prefetch)
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 3);
            using var prefetcher = prefetch ? chain.NewPrefetcher(WorkerCount) : null;
            var session = prefetch ? BeginSession(prefetcher, chain, parentRoot) : null;

            await chain.TouchAsync(5, 2, 0x22);
            if (session != null) await WalksSettledAsync(session);
            await chain.State.ClearStorageAsync(AddressOf(5));
            await chain.State.SaveAccountAsync(AddressOf(5), new Account { Balance = 5, Nonce = 7 });
            await chain.State.SaveStorageAsync(AddressOf(5), 9, new byte[] { 0x33 });
            session?.EndTriePathsBeforeRoot();
            return await chain.Calculator.ComputeStateRootAsync(parentRoot);
        }

        [Fact]
        public async Task Given_PrefetchedStoragePaths_When_TheRootIsComputed_Then_ItEqualsTheRootWithoutPrefetchAndTheCalculatorReadsLess()
        {
            var without = await SecondBlockAsync(prefetch: false);
            var with = await SecondBlockAsync(prefetch: true);

            Assert.Equal(without.Root, with.Root);
            Assert.True(with.Outcome.Warmed > 0);
            Assert.Equal(0, with.Outcome.Failed);
            Assert.True(with.ComputeReads < without.ComputeReads, $"{with.ComputeReads} < {without.ComputeReads}");
        }

        [Fact]
        public async Task Given_AWalkInFlight_When_TheRootBoundaryIsReached_Then_CommitWaitsForIt()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 2);
            using var gate = new ManualResetEventSlim(false);
            using var reading = new ManualResetEventSlim(false);
            chain.Nodes.OnWorkerRead = () => { reading.Set(); gate.Wait(); };
            using var prefetcher = chain.NewPrefetcher(2);
            var session = BeginSession(prefetcher, chain, parentRoot);
            await chain.TouchAsync(3, 1, 0x11);
            Assert.True(reading.Wait(TimeSpan.FromSeconds(30)));

            var ending = Task.Run(session.EndTriePathsBeforeRoot);
            await Task.Delay(300);
            Assert.False(ending.IsCompleted);
            Assert.True(chain.Nodes.ActiveReads > 0);

            gate.Set();
            await ending.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, chain.Nodes.ActiveReads);
        }

        [Fact]
        public async Task Given_AnAccountWhosePreBlockRootDiffersFromTheSet_When_Adopting_Then_TheCalculatorOpensItsOwnTrie()
        {
            var without = await RecreatedAccountRootAsync(prefetch: false);
            var with = await RecreatedAccountRootAsync(prefetch: true);

            Assert.Equal(without, with);
        }

        [Fact]
        public async Task Given_TheNextBlock_When_TriesAreRequested_Then_NoTrieFromThePreviousBlockIsReused()
        {
            var chain = new Chain();
            var firstRoot = await chain.GenesisAsync(10, 2);
            var first = chain.Calculator.BeginBlockTries(firstRoot);
            await chain.TouchAsync(4, 1, 0x44);
            var secondRoot = await chain.Calculator.ComputeStateRootAsync(firstRoot);

            var second = chain.Calculator.BeginBlockTries(secondRoot);
            var reads = chain.Nodes.Reads;
            second.TakeAccountTrie(secondRoot).Get(StateKeys.AccountKey(AddressOf(4)));

            Assert.NotSame(first, second);
            Assert.True(chain.Nodes.Reads > reads);
        }

        [Fact]
        public async Task Given_AStorageWriteToAnAccountWithNoStorageTrie_When_TheWalkIsCounted_Then_ItIsSkippedAndNotWarmed()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 2);
            using var prefetcher = chain.NewPrefetcher(2);
            var session = BeginSession(prefetcher, chain, parentRoot);

            await chain.State.SaveAccountAsync(AddressOf(99), new Account { Balance = 1, Nonce = 1 });
            await chain.State.SaveStorageAsync(AddressOf(99), 1, new byte[] { 0x09 });
            await WalksSettledAsync(session);
            session.EndTriePathsBeforeRoot();

            var outcome = session.TriePathOutcome;
            Assert.Equal(1, outcome.Warmed);
            Assert.Equal(1, outcome.Skipped);
            Assert.Equal(0, outcome.Failed);
        }

        [Fact]
        public async Task Given_AWalkThatThrows_When_ItIsCounted_Then_ItIsFailedAndNotWarmed()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 2);
            chain.Nodes.WorkerReadsThrow = true;
            using var prefetcher = chain.NewPrefetcher(2);
            var session = BeginSession(prefetcher, chain, parentRoot);

            await chain.TouchAsync(2, 1, 0x55);
            await WalksSettledAsync(session);
            session.EndTriePathsBeforeRoot();

            var outcome = session.TriePathOutcome;
            Assert.True(outcome.Failed > 0);
            Assert.Equal(0, outcome.Warmed);
        }

        [Fact]
        public async Task Given_DirtyAccounts_When_TheRealExecutionSavesThem_Then_TheirTriePathsAreReadOnPrefetchThreadsAndNothingIsWritten()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(30, 1);
            var commits = chain.Nodes.Commits;
            using var prefetcher = chain.NewPrefetcher(WorkerCount);
            var session = BeginSession(prefetcher, chain, parentRoot);
            var readsBefore = chain.Nodes.Reads;

            for (var i = 1; i <= 30; i++) await chain.TouchAsync(i, 1, 0x66);
            await WalksSettledAsync(session);
            session.EndTriePathsBeforeRoot();

            var outcome = session.TriePathOutcome;
            Assert.Equal(0, outcome.Failed);
            Assert.Equal(outcome.Enqueued, outcome.Warmed);
            Assert.Equal(0, outcome.Dropped);
            Assert.True(chain.Nodes.Reads > readsBefore);
            Assert.Equal(commits, chain.Nodes.Commits);
        }

        [Fact]
        public async Task Given_AFloodOfDirtyKeysAndBlockedReads_When_TheProducerSaves_Then_TheQueueIsBoundedKeysAreDroppedAndCountedAndTheProducerNeverBlocks()
        {
            const int flood = 20_000;
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 1);
            using var gate = new ManualResetEventSlim(false);
            chain.Nodes.OnWorkerRead = () => gate.Wait();
            using var prefetcher = chain.NewPrefetcher(2);
            var session = BeginSession(prefetcher, chain, parentRoot);

            var producer = Task.Run(() => SaveAccountsAsync(chain.State, 1, flood));
            var finished = await Task.WhenAny(producer, Task.Delay(TimeSpan.FromSeconds(60)));
            Assert.Same(producer, finished);

            var outcome = session.TriePathOutcome;
            Assert.Equal(flood, outcome.Enqueued + outcome.Dropped);
            Assert.True(outcome.Dropped > 0);
            Assert.True(outcome.Enqueued <= PrefetchSession.TriePathQueueCapacity + 2);
            Assert.Equal(0, outcome.Warmed);

            gate.Set();
            session.EndTriePathsBeforeRoot();
        }

        [Fact]
        public async Task Given_TheSessionStops_When_MoreKeysAreDirtied_Then_NoMoreTriePathsAreEnqueuedOrRead()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(40, 1);
            using var prefetcher = chain.NewPrefetcher(2);
            var session = BeginSession(prefetcher, chain, parentRoot);
            for (var i = 1; i <= 10; i++) await chain.TouchAsync(i, 1, 0x77);
            await WalksSettledAsync(session);

            session.Stop();
            var enqueued = session.TriePathOutcome.Enqueued;
            var reads = chain.Nodes.Reads;
            for (var i = 11; i <= 40; i++) await chain.TouchAsync(i, 1, 0x77);
            await Task.Delay(100);

            Assert.Equal(enqueued, session.TriePathOutcome.Enqueued);
            Assert.Equal(reads, chain.Nodes.Reads);
        }

        [Fact]
        public async Task Given_ABurstOfDirtyKeys_When_WorkersAreBusy_Then_TheProducerSignalsOnlyOnTheEmptyToNonEmptyTransition()
        {
            const int burst = 5_000;
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 1);
            using var gate = new ManualResetEventSlim(false);
            chain.Nodes.OnWorkerRead = () => gate.Wait();
            using var prefetcher = chain.NewPrefetcher(2);
            var session = BeginSession(prefetcher, chain, parentRoot);
            var before = prefetcher.Wakeups;

            await SaveAccountsAsync(chain.State, 1, burst);

            Assert.InRange(prefetcher.Wakeups - before, 1, 4);
            gate.Set();
            session.EndTriePathsBeforeRoot();
        }

        [Fact]
        public async Task Given_ASessionStartedWithoutATrieHost_When_TriePathsStart_Then_TheyAreRefusedAndNoObserverIsAttached()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(5, 1);
            using var prefetcher = chain.NewPrefetcher(2);
            var session = prefetcher.Begin(new BlockHeader { BlockNumber = 2, Timestamp = 1_000 }, new List<ISignedTransaction>());

            var started = session.StartTriePaths(chain.State, parentRoot, null);
            await chain.TouchAsync(1, 1, 0x99);

            Assert.False(started);
            Assert.Equal(0, session.TriePathOutcome.Enqueued);
            Assert.True(session.StartTriePaths(chain.State, parentRoot, chain.Calculator));
        }

        [Fact]
        public async Task Given_AnObserverAttached_When_ASecondOneAttaches_Then_ItIsRefusedAndTheFirstStillReceivesEvents()
        {
            var state = new BufferedFlatStateStore(new InMemoryStateStore());
            state.BeginBuffering();
            var first = new CountingObserver();
            var second = new CountingObserver();

            Assert.True(state.TryAttachDirtyKeyObserver(first));
            Assert.False(state.TryAttachDirtyKeyObserver(second));
            await SaveAccountsAsync(state, 1, 3);

            Assert.Equal(3, first.Accounts);
            Assert.Equal(0, second.Accounts);
        }

        [Fact]
        public async Task Given_AnObserverDetached_When_AnotherAttaches_Then_ItIsAcceptedAndReceivesEvents()
        {
            var state = new BufferedFlatStateStore(new InMemoryStateStore());
            state.BeginBuffering();
            var first = new CountingObserver();
            var second = new CountingObserver();
            Assert.True(state.TryAttachDirtyKeyObserver(first));

            state.DetachDirtyKeyObserver(second);
            Assert.False(state.TryAttachDirtyKeyObserver(second));
            state.DetachDirtyKeyObserver(first);
            Assert.True(state.TryAttachDirtyKeyObserver(second));
            await SaveAccountsAsync(state, 1, 2);

            Assert.Equal(0, first.Accounts);
            Assert.Equal(2, second.Accounts);
        }

        [Fact]
        public async Task Given_TriePathsRunning_When_TheNodeStops_Then_WorkerThreadsReturnToBaseline()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(40, 1);
            var prefetcher = chain.NewPrefetcher(4);
            var session = BeginSession(prefetcher, chain, parentRoot);
            Assert.Equal(4, prefetcher.LiveWorkerThreads);
            for (var i = 1; i <= 40; i++) await chain.TouchAsync(i, 1, 0x88);
            await WalksSettledAsync(session);

            prefetcher.Dispose();

            await WaitUntilAsync(() => prefetcher.LiveWorkerThreads == 0);
            Assert.False(session.StartTriePaths(chain.State, parentRoot, chain.Calculator));
        }

        [Fact]
        public async Task Given_ABlockedWalk_When_TheNodeStops_Then_DisposeReturnsWithinTheBoundAndThreadsExitOnceUnblocked()
        {
            var chain = new Chain();
            var parentRoot = await chain.GenesisAsync(10, 1);
            using var gate = new ManualResetEventSlim(false);
            using var reading = new ManualResetEventSlim(false);
            chain.Nodes.OnWorkerRead = () => { reading.Set(); gate.Wait(); };
            var bound = TimeSpan.FromMilliseconds(500);
            var prefetcher = chain.NewPrefetcher(2, bound);
            BeginSession(prefetcher, chain, parentRoot);
            await chain.TouchAsync(2, 1, 0x99);
            Assert.True(reading.Wait(TimeSpan.FromSeconds(30)));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            prefetcher.Dispose();

            Assert.True(stopwatch.Elapsed < bound + TimeSpan.FromSeconds(3), stopwatch.Elapsed.ToString());
            gate.Set();
            await WaitUntilAsync(() => prefetcher.LiveWorkerThreads == 0);
        }

        private static async Task SaveAccountsAsync(IStateStore state, int from, int to)
        {
            for (var i = from; i <= to; i++)
                await state.SaveAccountAsync(AddressOf(i), new Account { Balance = i, Nonce = 1 });
        }
    }
}
