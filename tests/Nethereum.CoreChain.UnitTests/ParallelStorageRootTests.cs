using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class ParallelStorageRootTests
    {
        private const int AccountCount = 150;
        private const int Blocks = 4;

        private static string Address(int number) => "0x" + number.ToString("x40");

        private static bool IsContract(int number) => number % 3 == 0;

        private static int SlotCount(int number) => 8 + number % 25;

        private static byte[] SlotValue(int number, int slot, int block)
            => new byte[] { 0, (byte)(number + 1), (byte)(slot + block + 1) };

        private static Account Eoa(int number, int block)
            => new Account { Nonce = number % 3, Balance = number + block + 1 };

        private static Account Contract(int number, int block)
            => new Account { Nonce = 1, Balance = number + block, CodeHash = new byte[] { 1, 2, (byte)number } };

        private static Account AccountFor(int number, int block)
            => IsContract(number) ? Contract(number, block) : Eoa(number, block);

        private static async Task ApplyBlockAsync(IStateStore state, int block)
        {
            switch (block)
            {
                case 0: await CreateAccountsAsync(state, 0, AccountCount, block); break;
                case 1: await ReviseAccountsAsync(state, block); break;
                case 2: await DeleteClearAndCreateAsync(state, block); break;
                default: await TouchWithoutChangingStorageAsync(state, block); break;
            }
        }

        private static async Task CreateAccountsAsync(IStateStore state, int from, int to, int block)
        {
            for (var i = from; i < to; i++)
            {
                await state.SaveAccountAsync(Address(i), AccountFor(i, block));
                if (!IsContract(i)) continue;
                for (var slot = 0; slot < SlotCount(i); slot++)
                    await state.SaveStorageAsync(Address(i), slot, SlotValue(i, slot, block));
            }
        }

        private static async Task ReviseAccountsAsync(IStateStore state, int block)
        {
            for (var i = 0; i < AccountCount; i += 2)
                await state.SaveAccountAsync(Address(i), AccountFor(i, block));

            for (var i = 0; i < AccountCount; i++)
            {
                if (!IsContract(i)) continue;
                var address = Address(i);
                if (i % 9 == 0)
                {
                    for (var slot = 0; slot < SlotCount(i); slot++)
                        await state.SaveStorageAsync(address, slot, new byte[0]);
                    continue;
                }
                for (var slot = 0; slot < 4; slot++)
                    await state.SaveStorageAsync(address, slot, SlotValue(i, slot, block));
                await state.SaveStorageAsync(address, 4, new byte[] { 0, 0 });
                await state.SaveStorageAsync(address, 5, null);
                await state.SaveStorageAsync(address, 100 + i, SlotValue(i, 100, block));
            }
        }

        private static async Task DeleteClearAndCreateAsync(IStateStore state, int block)
        {
            foreach (var i in new[] { 1, 4, 7, 10, 13, 16, 3, 6, 12 })
                await state.DeleteAccountAsync(Address(i));

            foreach (var i in new[] { 15, 21, 27 })
            {
                await state.ClearStorageAsync(Address(i));
                await state.SaveAccountAsync(Address(i), Contract(i, block));
                await state.SaveStorageAsync(Address(i), 0, SlotValue(i, 0, block));
                await state.SaveStorageAsync(Address(i), 1, SlotValue(i, 1, block));
            }

            await CreateAccountsAsync(state, AccountCount, AccountCount + 20, block);
        }

        private static async Task TouchWithoutChangingStorageAsync(IStateStore state, int block)
        {
            for (var i = 20; i < AccountCount; i += 5)
                await state.SaveAccountAsync(Address(i), AccountFor(i, block));
            for (var i = 30; i < AccountCount; i += 6)
            {
                if (!IsContract(i)) continue;
                await state.SaveStorageAsync(Address(i), 1, SlotValue(i, 1, block));
                await state.SaveStorageAsync(Address(i), 2, new byte[0]);
            }
        }

        private static async Task<List<string>> RootsAsync(int workers)
        {
            var state = new InMemoryStateStore();
            using var calculator = new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore(), stateRootWorkers: workers);
            var roots = new List<string>();
            for (var block = 0; block < Blocks; block++)
            {
                await ApplyBlockAsync(state, block);
                roots.Add(Convert.ToHexString(await calculator.ComputeStateRootAsync()));
            }
            return roots;
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(16)]
        public async Task Given_many_accounts_with_dirty_storage_When_the_root_is_computed_with_N_workers_Then_it_equals_the_sequential_root(int workers)
        {
            var sequential = await RootsAsync(1);
            var parallel = await RootsAsync(workers);

            Assert.Equal(sequential, parallel);
            Assert.Equal(Blocks, parallel.Distinct().Count());
        }

        [Fact]
        public async Task Given_the_parallel_root_When_compared_with_a_full_rebuild_Then_the_scenario_itself_is_sound()
        {
            var state = new InMemoryStateStore();
            using var calculator = new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore(), stateRootWorkers: 8);
            byte[] last = null;
            for (var block = 0; block < Blocks; block++)
            {
                await ApplyBlockAsync(state, block);
                last = await calculator.ComputeStateRootAsync();
            }

            var rebuilt = await new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore()).ComputeFullStateRootAsync();

            Assert.Equal(rebuilt, last);
        }

        [Fact]
        public async Task Given_one_worker_When_the_root_is_computed_Then_no_worker_thread_is_started()
        {
            var state = new InMemoryStateStore();
            using var calculator = new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore(), stateRootWorkers: 1);
            for (var block = 0; block < Blocks; block++)
            {
                await ApplyBlockAsync(state, block);
                await calculator.ComputeStateRootAsync();
            }

            Assert.Equal(1, calculator.StateRootWorkers);
            Assert.Equal(0, calculator.WorkerThreadsStarted);
        }

        [Fact]
        public async Task Given_several_workers_When_many_blocks_are_computed_Then_the_threads_start_on_the_first_incremental_block_and_are_never_recreated()
        {
            var state = new InMemoryStateStore();
            using var calculator = new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore(), stateRootWorkers: 4);
            var started = new List<int>();
            for (var block = 0; block < Blocks; block++)
            {
                await ApplyBlockAsync(state, block);
                await calculator.ComputeStateRootAsync();
                started.Add(calculator.WorkerThreadsStarted);
            }

            Assert.Equal(0, started[0]);
            Assert.All(started.Skip(1), count => Assert.Equal(4, count));
        }

        [Fact]
        public async Task Given_an_account_deleted_and_a_sibling_created_in_one_block_When_the_root_is_computed_Then_updates_apply_before_deletions_and_the_root_matches()
        {
            var (deleted, kept, created) = ThreeAccountsInDistinctRootBranches();
            var state = new InMemoryStateStore();
            var nodes = new ReadRecordingNodeStore(new InMemoryContentNodeStore());
            using var calculator = new IncrementalStateRootCalculator(state, nodes, stateRootWorkers: 4);

            await state.SaveAccountAsync(Address(deleted), Eoa(deleted, 0));
            await state.SaveAccountAsync(Address(kept), Eoa(kept, 0));
            await calculator.ComputeStateRootAsync();
            await state.SaveAccountAsync(Address(kept), Eoa(kept, 1));
            await calculator.ComputeStateRootAsync();

            nodes.StartRecording();
            await state.DeleteAccountAsync(Address(deleted));
            await state.SaveAccountAsync(Address(created), Eoa(created, 2));
            var root = await calculator.ComputeStateRootAsync();

            var rebuilt = await new IncrementalStateRootCalculator(state, new InMemoryContentNodeStore()).ComputeFullStateRootAsync();
            Assert.Equal(rebuilt, root);
            Assert.Contains(BranchOf(deleted), nodes.ReadAccountBranches);
            Assert.DoesNotContain(BranchOf(kept), nodes.ReadAccountBranches);
        }

        [Fact]
        public async Task Given_a_storage_trie_update_that_throws_on_a_worker_When_the_root_is_computed_Then_the_block_fails_rather_than_returning_a_partial_root()
        {
            var state = new InMemoryStateStore();
            var nodes = new ReadRecordingNodeStore(new InMemoryContentNodeStore());
            byte[] previousRoot;
            using (var first = new IncrementalStateRootCalculator(state, nodes, stateRootWorkers: 1))
            {
                await CreateAccountsAsync(state, 0, AccountCount, 0);
                previousRoot = await first.ComputeStateRootAsync();
            }

            using var calculator = new IncrementalStateRootCalculator(state, nodes, stateRootWorkers: 4);
            await calculator.ComputeStateRootAsync(previousRoot);
            nodes.ThrowOnStorageReads();
            await ReviseAccountsAsync(state, 1);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => calculator.ComputeStateRootAsync());

            Assert.Equal(ReadRecordingNodeStore.FailureMessage, failure.Message);
            Assert.NotEmpty(nodes.FailingThreadNames);
            Assert.All(nodes.FailingThreadNames, name => Assert.StartsWith("state-root-", name));
        }

        private static (int Deleted, int Kept, int Created) ThreeAccountsInDistinctRootBranches()
        {
            var byBranch = new Dictionary<int, int>();
            for (var i = 1; byBranch.Count < 3; i++)
                byBranch.TryAdd(BranchOf(i), i);
            var numbers = byBranch.Values.ToArray();
            return (numbers[0], numbers[1], numbers[2]);
        }

        private static int BranchOf(int number) => StateKeys.AccountKey(Address(number))[0] >> 4;

        private sealed class ReadRecordingNodeStore : ITrieNodeStore
        {
            public const string FailureMessage = "storage trie node read failed";

            private readonly ITrieNodeStore _inner;
            private readonly object _gate = new object();
            private readonly List<int> _accountBranches = new List<int>();
            private readonly List<string> _failingThreads = new List<string>();
            private bool _recording;
            private bool _throwOnStorageReads;

            public ReadRecordingNodeStore(ITrieNodeStore inner) { _inner = inner; }

            public IReadOnlyCollection<int> ReadAccountBranches { get { lock (_gate) return _accountBranches.ToArray(); } }

            public IReadOnlyCollection<string> FailingThreadNames { get { lock (_gate) return _failingThreads.ToArray(); } }

            public void StartRecording() { lock (_gate) _recording = true; }

            public void ThrowOnStorageReads() { lock (_gate) _throwOnStorageReads = true; }

            public byte[] Get(Node reference)
            {
                var isStorage = reference.Owner != null && reference.Owner.Length > 0;
                lock (_gate)
                {
                    if (isStorage && _throwOnStorageReads)
                    {
                        _failingThreads.Add(Thread.CurrentThread.Name ?? string.Empty);
                        throw new InvalidOperationException(FailureMessage);
                    }
                    if (!isStorage && _recording && reference.Path != null && reference.Path.Length == 1)
                        _accountBranches.Add(reference.Path[0]);
                }
                return _inner.Get(reference);
            }

            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
        }
    }
}
