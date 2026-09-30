using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedStorageRootPersistTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public PathKeyedStorageRootPersistTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-storageroot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir, PathKeyedState = true, TrieNodeHistoryBlocks = 128, TrieNodeHistoryIndex = true,
            });
        }

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');
        private const string C = "0x00000000000000000000000000000000000000ca";

        private static IIncrementalStateRootCalculator NewCalc(RocksDbChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));

        private static async Task<byte[]> CommitBlockAsync(
            RocksDbChainStoreBundle bundle, HistoricalStateStore hist, IIncrementalStateRootCalculator calc,
            ulong blockNumber, byte[] prevRoot, Func<Task> mutate)
        {
            bundle.NodeCommitBlockSource.Arm(blockNumber);
            hist.SetCurrentBlockNumber(blockNumber);
            await mutate();
            var root = await calc.ComputeStateRootWithoutPersistAsync(prevRoot);
            await calc.PersistPendingStateAsync();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, blockNumber, new byte[32]);
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            return root;
        }

        [Fact]
        public async Task StorageRootNode_PersistsAtomicallyWithFlatStorageRoot_ColdReadResolves()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var calc = NewCalc(bundle);

            var root1 = await CommitBlockAsync(bundle, hist, calc, 1, null, async () =>
            {
                for (int i = 1; i <= 12; i++)
                    await hist.SaveStorageAsync(C, i, new byte[] { (byte)(0x10 + i), (byte)(i * 3) });
                await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });
                for (int i = 0; i < 20; i++) await hist.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
            });

            var root2 = await CommitBlockAsync(bundle, hist, calc, 2, root1, async () =>
            {
                await hist.SaveStorageAsync(C, 5, new byte[] { 0xaa, 0xbb, 0xcc });
                await hist.SaveStorageAsync(C, 99, new byte[] { 0xde, 0xad });
            });
            Assert.False(root2.AsSpan().SequenceEqual(root1));

            var root3 = await CommitBlockAsync(bundle, hist, calc, 3, root2, async () =>
            {
                for (int i = 0; i < 20; i++) await hist.SaveAccountAsync(Addr(i), new Account { Balance = 5000 + i, Nonce = 2 });
            });
            var root4 = await CommitBlockAsync(bundle, hist, calc, 4, root3, async () =>
            {
                for (int i = 20; i < 40; i++) await hist.SaveAccountAsync(Addr(i), new Account { Balance = 7000 + i, Nonce = 1 });
            });

            var coldCalc = NewCalc(bundle);
            var root5 = await CommitBlockAsync(bundle, hist, coldCalc, 5, root4, async () =>
            {
                await hist.SaveStorageAsync(C, 7, new byte[] { 0x77, 0x77 });
            });

            var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.Equal(BitConverter.ToString(oracle), BitConverter.ToString(root5));
        }

        [Fact]
        public async Task StorageRootNode_ColdDescentOfContractStorage_ResolvesAfterCommit()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var calc = NewCalc(bundle);

            var root1 = await CommitBlockAsync(bundle, hist, calc, 1, null, async () =>
            {
                for (int i = 1; i <= 40; i++)
                    await hist.SaveStorageAsync(C, i, new byte[] { (byte)(i & 0xff), (byte)((i * 7) & 0xff), 0x01 });
                await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });
            });

            var root2 = await CommitBlockAsync(bundle, hist, calc, 2, root1, async () =>
            {
                await hist.SaveStorageAsync(C, 13, new byte[] { 0xab, 0xcd });
                await hist.SaveStorageAsync(C, 500, new byte[] { 0xef });
            });

            var acct = await hist.GetAccountAsync(C);
            Assert.NotNull(acct?.StateRoot);
            var storage = PatriciaTrie.LoadFromStorage(acct.StateRoot, bundle.StateTrieNodes, StateKeys.AccountKey(C));
            Assert.NotNull(storage.Get(StateKeys.StorageSlotKey(13)));
            Assert.NotNull(storage.Get(StateKeys.StorageSlotKey(500)));
            Assert.NotNull(storage.Get(StateKeys.StorageSlotKey(1)));
        }

        [Fact]
        public async Task StorageRoot_WipedThenRepopulated_ColdReadResolves()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var calc = NewCalc(bundle);

            var root1 = await CommitBlockAsync(bundle, hist, calc, 1, null, async () =>
            {
                for (int i = 1; i <= 10; i++) await hist.SaveStorageAsync(C, i, new byte[] { (byte)(0x20 + i) });
                await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });
            });

            var root2 = await CommitBlockAsync(bundle, hist, calc, 2, root1, async () =>
            {
                for (int i = 1; i <= 10; i++) await hist.SaveStorageAsync(C, i, new byte[0]);
            });

            var root3 = await CommitBlockAsync(bundle, hist, calc, 3, root2, async () =>
            {
                for (int i = 20; i <= 30; i++) await hist.SaveStorageAsync(C, i, new byte[] { (byte)(0x40 + i) });
            });

            var coldCalc = NewCalc(bundle);
            var root4 = await CommitBlockAsync(bundle, hist, coldCalc, 4, root3, async () =>
            {
                await hist.SaveStorageAsync(C, 25, new byte[] { 0x55 });
            });

            var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.Equal(BitConverter.ToString(oracle), BitConverter.ToString(root4));
        }

        [Fact]
        public async Task StorageRoot_ModifiedInDiscardedBlock_ThenRecommitted_ColdReadResolves()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var calc = NewCalc(bundle);

            var root1 = await CommitBlockAsync(bundle, hist, calc, 1, null, async () =>
            {
                for (int i = 1; i <= 12; i++) await hist.SaveStorageAsync(C, i, new byte[] { (byte)(0x30 + i) });
                await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });
            });

            bundle.NodeCommitBlockSource.Arm(2);
            hist.SetCurrentBlockNumber(2);
            await hist.SaveStorageAsync(C, 5, new byte[] { 0xff, 0xee });
            await calc.ComputeStateRootWithoutPersistAsync(root1);
            calc.DiscardPendingState();
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();

            var calcB = NewCalc(bundle);
            var root2 = await CommitBlockAsync(bundle, hist, calcB, 2, root1, async () =>
            {
                await hist.SaveStorageAsync(C, 5, new byte[] { 0xff, 0xee });
            });

            var calcC = NewCalc(bundle);
            var root3 = await CommitBlockAsync(bundle, hist, calcC, 3, root2, async () =>
            {
                await hist.SaveStorageAsync(C, 9, new byte[] { 0x01, 0x02 });
            });

            var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.Equal(BitConverter.ToString(oracle), BitConverter.ToString(root3));
        }

        [Fact]
        public async Task ContractModifiedEveryConsecutiveBlock_WarmCalculator_RootNodePersistsEachBlock()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var calc = NewCalc(bundle);
            var owner = StateKeys.AccountKey(C);

            byte[] prev = null;
            for (ulong b = 1; b <= 6; b++)
            {
                var root = await CommitBlockAsync(bundle, hist, calc, b, prev, async () =>
                {
                    if (b == 1)
                    {
                        for (int i = 1; i <= 20; i++) await hist.SaveStorageAsync(C, i, new byte[] { (byte)(0x10 + i), (byte)(i * 5) });
                        await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 0, CodeHash = new byte[] { 9 } });
                    }
                    else
                    {
                        await hist.SaveStorageAsync(C, (int)(b * 3), new byte[] { (byte)b, 0xaa });
                        await hist.SaveStorageAsync(C, (int)(b * 7), new byte[] { (byte)b, 0xbb, 0xcc });
                        await hist.SaveStorageAsync(C, (int)(b + 100), new byte[] { (byte)b });
                    }
                    await hist.SaveAccountAsync(Addr((int)b), new Account { Balance = 1000 + b, Nonce = b });
                });
                prev = root;

                var acct = await hist.GetAccountAsync(C);
                Assert.NotNull(acct?.StateRoot);
                var cold = PatriciaTrie.LoadFromStorage(acct.StateRoot, bundle.StateTrieNodes, owner);
                var resolved = cold.Root.GetHash();
                Assert.Equal(
                    "block " + b + " " + BitConverter.ToString(acct.StateRoot),
                    "block " + b + " " + BitConverter.ToString(resolved));
                Assert.NotNull(cold.Get(StateKeys.StorageSlotKey(1)));
            }
        }

        [Fact]
        public async Task RevertCurrentBlock_RestoresFlatToPreBlockState_NoFlatTrieDesync()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;

            var r0 = new byte[32]; r0[0] = 0xAA;
            var rWrong = new byte[32]; rWrong[0] = 0xBB;

            hist.SetCurrentBlockNumber(1);
            await hist.SaveAccountAsync(C, new Account { Nonce = 1, Balance = 100, CodeHash = new byte[] { 9 }, StateRoot = r0 });
            await hist.SaveStorageAsync(C, 5, new byte[] { 0x11 });
            await hist.SaveStorageAsync(C, 6, new byte[] { 0x22 });
            await hist.ClearCurrentBlockNumberAsync();

            var acctBefore = await hist.GetAccountAsync(C);
            var slot5Before = await hist.GetStorageAsync(C, 5);

            hist.SetCurrentBlockNumber(2);
            await hist.SaveAccountAsync(C, new Account { Nonce = 2, Balance = 200, CodeHash = new byte[] { 9 }, StateRoot = rWrong });
            await hist.SaveStorageAsync(C, 5, new byte[] { 0x99 });
            await hist.SaveStorageAsync(C, 7, new byte[] { 0x77 });
            await hist.RevertCurrentBlockAsync();

            var acctAfter = await hist.GetAccountAsync(C);
            Assert.Equal(BitConverter.ToString(acctBefore.StateRoot), BitConverter.ToString(acctAfter.StateRoot));
            Assert.Equal((int)acctBefore.Balance, (int)acctAfter.Balance);
            Assert.Equal((int)acctBefore.Nonce, (int)acctAfter.Nonce);
            Assert.Equal(BitConverter.ToString(slot5Before), BitConverter.ToString(await hist.GetStorageAsync(C, 5)));
            var slot7 = await hist.GetStorageAsync(C, 7);
            Assert.True(slot7 == null || slot7.Length == 0, "the block-2-only slot must be reverted (deleted)");

            var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.NotNull(oracle);
        }

        public void Dispose()
        {
            try { _mgr.Dispose(); } catch { }
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
        }
    }
}
