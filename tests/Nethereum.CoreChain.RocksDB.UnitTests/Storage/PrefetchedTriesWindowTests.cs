using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class PrefetchedTriesWindowTests : IDisposable
    {
        private const int Accounts = 20;
        private readonly string _dir;
        private static readonly ChainConfig Config = new ChainConfig
        {
            ChainId = 1337, BlockGasLimit = 30_000_000, BaseFee = 0
        };

        public PrefetchedTriesWindowTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-prefetched-tries-window-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static string AddressOf(int index) => "0x" + index.ToString("x40");

        private static byte[] BlockHash(byte block) { var h = new byte[32]; h[0] = block; return h; }

        private static async Task TouchAsync(IStateStore state, int account, byte value)
        {
            var existing = await state.GetAccountAsync(AddressOf(account));
            existing.Nonce += 1;
            await state.SaveAccountAsync(AddressOf(account), existing);
            for (var slot = 1; slot <= 3; slot++)
                await state.SaveStorageAsync(AddressOf(account), slot, new byte[] { value, (byte)slot });
        }

        private static async Task TouchAllAsync(IStateStore state, byte value)
        {
            for (var i = 1; i <= Accounts; i++) await TouchAsync(state, i, value);
        }

        private static async Task<byte[]> RecomputedRootAsync(IStateStore state)
        {
            using var reference = new IncrementalStateRootCalculator(state);
            return await reference.ComputeFullStateRootAsync();
        }

        private async Task<(byte[] Root, byte[] Expected, TriePathOutcome Outcome)> ThirdBlockOverAWindowAsync()
        {
            var options = new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 100_000,
                TrieNodeHistoryIndex = true,
            };
            using var manager = new RocksDbManager(options);
            var bundle = RocksDbChainStoreBundle.FromManager(manager, _dir, ownsManager: false);
            var flush = (IAtomicBlockFlush)bundle;
            var state = new BufferedFlatStateStore(new InMemoryStateStore());
            state.BeginBuffering();
            using var calculator = new IncrementalStateRootCalculator(state, bundle.StateTrieNodes, emitTombstones: true);

            bundle.NodeCommitBlockSource.Arm(1);
            for (var i = 1; i <= Accounts; i++)
                await state.SaveAccountAsync(AddressOf(i), new Account { Balance = i, Nonce = 1 });
            await TouchAllAsync(state, 0x10);
            var built = await calculator.ComputeStateRootAsync();
            await TouchAllAsync(state, 0x11);
            var root1 = await calculator.ComputeStateRootAsync(built);
            bundle.NodeCommitBlockSource.Clear();
            await flush.FlushBlockAsync(flat: null, 1, BlockHash(1));
            await flush.DrainAsync();

            bundle.NodeCommitBlockSource.Arm(2);
            await TouchAllAsync(state, 0x22);
            var root2 = await calculator.ComputeStateRootAsync(root1);
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            using var prefetcher = new StatePrefetcher(
                state, new InMemoryBlockStore(), new FixedChainActivations(HardforkName.Prague),
                _ => Config, _ => Config.GetHardforkConfig(), authorResolver: null, workers: 3);
            var session = prefetcher.Begin(new BlockHeader { BlockNumber = 3, Timestamp = 1_000 }, new List<ISignedTransaction>());
            Assert.True(session.StartTriePaths(state, root2, calculator));
            await TouchAllAsync(state, 0x33);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline
                && session.TriePathOutcome.Warmed + session.TriePathOutcome.Failed + session.TriePathOutcome.Skipped < session.TriePathOutcome.Enqueued)
                await Task.Delay(5);
            session.EndTriePathsBeforeRoot();

            var root3 = await calculator.ComputeStateRootAsync(root2);
            bundle.NodeCommitBlockSource.Clear();
            return (root3, await RecomputedRootAsync(state), session.TriePathOutcome);
        }

        [Fact]
        public async Task Given_ANodeRewrittenEarlierInTheFlushWindow_When_ThePrefetcherResolvesIt_Then_ItReadsTheWindowVersionNotDisk()
        {
            var (root, expected, outcome) = await ThirdBlockOverAWindowAsync();

            Assert.True(outcome.Warmed > 0);
            Assert.Equal(0, outcome.Failed);
            Assert.Equal(expected, root);
        }
    }
}
