using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class FlatCacheInvalidationTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public FlatCacheInvalidationTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-flatcacheinval-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
            });
        }

        private static IIncrementalStateRootCalculator NewCalc(IChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));

        private static byte[] Hash(byte fill) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = fill; return h; }

        private static async Task SaveHeader(IBlockStore blocks, ulong number, byte[] hash, byte[] stateRoot)
            => await blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = number,
                ParentHash = number == 0 ? new byte[32] : Hash((byte)(number - 1)),
                StateRoot = stateRoot, TransactionsHash = new byte[32], ReceiptHash = new byte[32],
                UnclesHash = new byte[32], ExtraData = Array.Empty<byte>(), LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000", Difficulty = 0, GasLimit = 0,
                GasUsed = 0, Timestamp = 0, MixHash = new byte[32], Nonce = new byte[8],
            }, hash);

        private static async Task<byte[]> RunBlock(
            RocksDbChainStoreBundle bundle, HistoricalStateStore hist, int b, byte[] parentRoot, Nethereum.Util.EvmUInt256 balance)
        {
            bundle.NodeCommitBlockSource.Arm((ulong)b);
            hist.SetCurrentBlockNumber(b);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = balance, Nonce = b });
            var root = await NewCalc(bundle).ComputeStateRootAsync(parentRoot);
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, Hash((byte)b));
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            await SaveHeader(bundle.Blocks, (ulong)b, Hash((byte)b), root);
            bundle.Metadata.Commit((ulong)b, Hash((byte)b));
            return root;
        }

        [Fact]
        public async Task ResetSnapBootstrapState_ClearsFlatCache_SubsequentReadIsNotStale()
        {
            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false, flatStateCache: cache);
            var hist = (HistoricalStateStore)bundle.State;

            var root0 = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), root0);
            await RunBlock(bundle, hist, 1, root0, 1000);

            Assert.True(cache.TryGetAccount(AddrA, out var warm));
            Assert.Equal((Nethereum.Util.EvmUInt256)1000, warm.Balance);
            var preReset = await bundle.State.GetAccountAsync(AddrA);
            Assert.NotNull(preReset);
            Assert.Equal((Nethereum.Util.EvmUInt256)1000, preReset.Balance);

            await bundle.ResetSnapBootstrapStateAsync();

            var postReset = await bundle.State.GetAccountAsync(AddrA);
            Assert.Null(postReset);

            var rawProbe = new RocksDbStateStore(_mgr);
            Assert.Null(await rawProbe.GetAccountAsync(AddrA));
        }

        [Fact]
        public async Task ResetStateOnly_ClearsFlatCache_SubsequentReadIsNotStale()
        {
            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false, flatStateCache: cache);
            var hist = (HistoricalStateStore)bundle.State;

            var root0 = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), root0);
            await RunBlock(bundle, hist, 1, root0, 2000);

            Assert.True(cache.TryGetAccount(AddrA, out _));

            await bundle.ResetStateOnlyAsync();

            var postReset = await bundle.State.GetAccountAsync(AddrA);
            Assert.Null(postReset);

            var rawProbe = new RocksDbStateStore(_mgr);
            Assert.Null(await rawProbe.GetAccountAsync(AddrA));
        }

        [Fact]
        public async Task RecoverToAsync_ClearsFlatCache_ReadReflectsRewoundValue_NotStaleForwardValue()
        {
            const int M = 5;
            const ulong N = 2;

            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false, flatStateCache: cache);
            var hist = (HistoricalStateStore)bundle.State;

            var roots = new byte[M + 1][];
            roots[0] = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), roots[0]);
            for (int b = 1; b <= M; b++)
                roots[b] = await RunBlock(bundle, hist, b, roots[b - 1], (Nethereum.Util.EvmUInt256)(1000 * b));

            Assert.True(cache.TryGetAccount(AddrA, out var forwardCached));
            Assert.Equal((Nethereum.Util.EvmUInt256)(1000 * M), forwardCached.Balance);

            _mgr.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>(), new byte[40]);

            var committed = await bundle.RecoverToAsync(N, FlatRecoverySource.ReconcileFromTrie, _ => { }, default);
            Assert.Equal(N, committed);

            var postRecover = await bundle.State.GetAccountAsync(AddrA);
            Assert.NotNull(postRecover);
            Assert.Equal((Nethereum.Util.EvmUInt256)(1000 * (int)N), postRecover.Balance);
            Assert.NotEqual((Nethereum.Util.EvmUInt256)(1000 * M), postRecover.Balance);

            var rawProbe = new RocksDbStateStore(_mgr);
            var rawAccount = await rawProbe.GetAccountAsync(AddrA);
            Assert.NotNull(rawAccount);
            Assert.Equal((Nethereum.Util.EvmUInt256)(1000 * (int)N), rawAccount.Balance);
        }

        [Fact]
        public async Task RewindCoordinator_JournalRewind_ClearsFlatCache_ReadReflectsRewoundValue_NotStaleForwardValue()
        {
            var dir = Path.Combine(Path.GetTempPath(), "necc-flatcacheinval-hashrw-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir });
            try
            {
                var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
                using var bundle = RocksDbChainStoreBundle.FromManager(
                    mgr, dir, HistoricalStateOptions.FullArchive, ownsManager: false, flatStateCache: cache);
                var hist = (HistoricalStateStore)bundle.State;

                await SaveHeader(bundle.Blocks, 0, Hash(0), Array.Empty<byte>());
                await SaveHeader(bundle.Blocks, 1, Hash(1), Array.Empty<byte>());
                await SaveHeader(bundle.Blocks, 2, Hash(2), Array.Empty<byte>());

                hist.SetCurrentBlockNumber(1);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
                await hist.ClearCurrentBlockNumberAsync();
                bundle.Metadata.Commit(1, Hash(1));

                hist.SetCurrentBlockNumber(2);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 2000, Nonce = 2 });
                await hist.ClearCurrentBlockNumberAsync();
                bundle.Metadata.Commit(2, Hash(2));

                Assert.True(cache.TryGetAccount(AddrA, out var forwardCached));
                Assert.Equal((Nethereum.Util.EvmUInt256)2000, forwardCached.Balance);

                var coordinator = new RewindCoordinator(bundle);
                var result = await coordinator.RewindToAsync(1, RewindPolicy.JournalOnly);
                Assert.Equal(RewindOutcome.JournalUsed, result.Outcome);
                Assert.Equal(1UL, bundle.Metadata.GetLastBlock());

                var postRewind = await bundle.State.GetAccountAsync(AddrA);
                Assert.NotNull(postRewind);
                Assert.Equal((Nethereum.Util.EvmUInt256)1000, postRewind.Balance);

                var rawProbe = new RocksDbStateStore(mgr);
                var rawAccount = await rawProbe.GetAccountAsync(AddrA);
                Assert.NotNull(rawAccount);
                Assert.Equal((Nethereum.Util.EvmUInt256)1000, rawAccount.Balance);
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task ReconcileFlatStateAsync_ClearsFlatCache_ReadReflectsPatchedValue_NotStaleLeakedValue()
        {
            var cache = new FlatStateCache(budgetBytes: 4 * 1024 * 1024);
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false, flatStateCache: cache);
            var hist = (HistoricalStateStore)bundle.State;

            var root0 = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), root0);
            var root1 = await RunBlock(bundle, hist, 1, root0, 1000);

            hist.SetCurrentBlockNumber(2);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 9999, Nonce = 2 });
            await hist.ClearCurrentBlockNumberAsync();

            Assert.True(cache.TryGetAccount(AddrA, out var leaked));
            Assert.Equal((Nethereum.Util.EvmUInt256)9999, leaked.Balance);
            var rawProbeBeforeReconcile = new RocksDbStateStore(_mgr);
            Assert.Equal((Nethereum.Util.EvmUInt256)9999, (await rawProbeBeforeReconcile.GetAccountAsync(AddrA)).Balance);

            var result = await bundle.ReconcileFlatStateAsync(root1, _ => { }, default);
            Assert.True(result.AccountsPatched >= 1, "expected the leaked AddrA row to be patched back to the trie value");

            var postReconcile = await bundle.State.GetAccountAsync(AddrA);
            Assert.NotNull(postReconcile);
            Assert.Equal((Nethereum.Util.EvmUInt256)1000, postReconcile.Balance);

            var rawProbeAfterReconcile = new RocksDbStateStore(_mgr);
            var rawAccount = await rawProbeAfterReconcile.GetAccountAsync(AddrA);
            Assert.NotNull(rawAccount);
            Assert.Equal((Nethereum.Util.EvmUInt256)1000, rawAccount.Balance);
        }

        [Fact]
        public async Task BufferedFlatStateStore_And_HistoricalStateStore_ClearCache_DropTheAttachedCache()
        {
            var raw = new RocksDbStateStore(_mgr);
            var cache = new FlatStateCache(budgetBytes: 1024 * 1024);
            var buffered = new BufferedFlatStateStore(raw, cache);
            var hist = new HistoricalStateStore(buffered, new RocksDbStateDiffStore(_mgr), HistoricalStateOptions.FullArchive);

            hist.SetCurrentBlockNumber(1);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 42, Nonce = 1 });
            await hist.ClearCurrentBlockNumberAsync();

            Assert.True(cache.TryGetAccount(AddrA, out _));
            Assert.Equal(1, cache.Count);

            buffered.ClearCache();
            Assert.Equal(0, cache.Count);
            Assert.False(cache.TryGetAccount(AddrA, out _));

            hist.SetCurrentBlockNumber(2);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 43, Nonce = 2 });
            await hist.ClearCurrentBlockNumberAsync();
            Assert.Equal(1, cache.Count);

            hist.ClearCache();
            Assert.Equal(0, cache.Count);
            Assert.False(cache.TryGetAccount(AddrA, out _));
        }

        [Fact]
        public void ClearCache_NoCacheAttached_IsANoOp()
        {
            var raw = new RocksDbStateStore(_mgr);
            var buffered = new BufferedFlatStateStore(raw);
            var hist = new HistoricalStateStore(buffered, new RocksDbStateDiffStore(_mgr), HistoricalStateOptions.FullArchive);

            var ex1 = Record.Exception(() => buffered.ClearCache());
            var ex2 = Record.Exception(() => hist.ClearCache());
            Assert.Null(ex1);
            Assert.Null(ex2);
        }

        public void Dispose()
        {
            _mgr?.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
