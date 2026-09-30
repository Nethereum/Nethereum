using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class MaterializingRewindTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public MaterializingRewindTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-nh-matrw-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
            });
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "reorg-rewind", "Node history materializing rewind restores a torn head to the target block state, peer-free")]
        public async Task MaterializingRewind_RestoresTargetTrie_FromTornHead_AndLeavesNodeHistoryIntact()
        {
            const int M = 5;
            const ulong N = 2;

            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);

            var hist = (HistoricalStateStore)bundle.State;
            var roots = new byte[M + 1][];

            roots[0] = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), roots[0]);

            for (int b = 1; b <= M; b++)
            {
                bundle.NodeCommitBlockSource.Arm((ulong)b);
                hist.SetCurrentBlockNumber(b);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000 * b, Nonce = b });
                var contract = ContractAddr(b);
                await hist.SaveAccountAsync(contract, new Account { Balance = 0, Nonce = 1 });
                await hist.SaveStorageAsync(contract, BigInteger.One, new byte[] { (byte)(0x10 + b) });
                roots[b] = await NewCalc(bundle).ComputeStateRootAsync(roots[b - 1]);
                await hist.ClearCurrentBlockNumberAsync();
                bundle.NodeCommitBlockSource.Clear();
                await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, Hash((byte)b));
                await ((IAtomicBlockFlush)bundle).DrainAsync();

                await SaveHeader(bundle.Blocks, (ulong)b, Hash((byte)b), roots[b]);
                bundle.Metadata.Commit((ulong)b, Hash((byte)b));
            }

            _mgr.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>(), new byte[40]);
            Assert.Throws<InvalidOperationException>(() =>
                PatriciaTrie.LoadFromStorage(roots[M], bundle.StateTrieNodes).Get(StateKeys.AccountKey(AddrA)));

            var historyBefore = CountNodeHistoryTotal();

            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: false);
            var stats = journal.MaterializingRewindTo(N);
            Assert.True(stats.EntriesApplied > 0, "expected reverse-diffs to apply");
            Assert.Equal(N, stats.TargetBlock);
            Assert.Equal((ulong)M, stats.MaxHistoryBlock);

            var restoredRoot = _mgr.Get(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>());
            Assert.NotNull(restoredRoot);
            Assert.Equal(roots[(int)N], new Sha3Keccack().CalculateHash(restoredRoot));

            var trie = PatriciaTrie.LoadFromStorage(roots[(int)N], bundle.StateTrieNodes);
            Assert.NotNull(trie.Get(StateKeys.AccountKey(AddrA)));
            Assert.NotNull(trie.Get(StateKeys.AccountKey(ContractAddr(1))));
            Assert.NotNull(trie.Get(StateKeys.AccountKey(ContractAddr(2))));
            Assert.Null(trie.Get(StateKeys.AccountKey(ContractAddr(3))));
            Assert.Null(trie.Get(StateKeys.AccountKey(ContractAddr(4))));
            Assert.Null(trie.Get(StateKeys.AccountKey(ContractAddr(5))));
            Assert.Equal(roots[(int)N], trie.Root.GetHash());

            Assert.Equal(historyBefore, CountNodeHistoryTotal());
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "reorg-rewind", "Node-history recovery rolls a torn head back, rebuilds flat, verifies clean, and commits the target")]
        public async Task RecoverFromNodeHistory_RestoresTornHead_RebuildsFlat_VerifiesClean_CommitsTarget()
        {
            const int M = 5;
            const ulong N = 2;

            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);

            var hist = (HistoricalStateStore)bundle.State;
            var roots = new byte[M + 1][];
            roots[0] = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), roots[0]);
            for (int b = 1; b <= M; b++)
            {
                bundle.NodeCommitBlockSource.Arm((ulong)b);
                hist.SetCurrentBlockNumber(b);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000 * b, Nonce = b });
                var contract = ContractAddr(b);
                await hist.SaveAccountAsync(contract, new Account { Balance = 0, Nonce = 1 });
                await hist.SaveStorageAsync(contract, BigInteger.One, new byte[] { (byte)(0x10 + b) });
                roots[b] = await NewCalc(bundle).ComputeStateRootAsync(roots[b - 1]);
                await hist.ClearCurrentBlockNumberAsync();
                bundle.NodeCommitBlockSource.Clear();
                await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, Hash((byte)b));
                await ((IAtomicBlockFlush)bundle).DrainAsync();
                await SaveHeader(bundle.Blocks, (ulong)b, Hash((byte)b), roots[b]);
                bundle.Metadata.Commit((ulong)b, Hash((byte)b));
            }
            Assert.Equal((ulong)M, bundle.Metadata.GetLastBlock());

            _mgr.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>(), new byte[40]);

            var committed = await bundle.RecoverToAsync(N, FlatRecoverySource.ReconcileFromTrie, _ => { }, default);

            Assert.Equal(N, committed);
            Assert.Equal(N, bundle.Metadata.GetLastBlock());
            Assert.Equal(Hash((byte)N), bundle.Metadata.GetLastBlockHash());

            var trie = PatriciaTrie.LoadFromStorage(roots[(int)N], bundle.StateTrieNodes);
            Assert.NotNull(trie.Get(StateKeys.AccountKey(AddrA)));
            Assert.NotNull(trie.Get(StateKeys.AccountKey(ContractAddr(2))));
            Assert.Null(trie.Get(StateKeys.AccountKey(ContractAddr(3))));

            var verify = await bundle.VerifyFlatStateAsync(roots[(int)N], _ => { }, default);
            Assert.Equal(0, verify.TotalRepairs);
        }

        [Fact]
        public async Task EnsureConsistentHead_CleanHead_ReturnsHeadWithoutRecovery()
        {
            const int M = 4;
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            await BuildBlocks(bundle, M);

            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(_ => { });

            Assert.False(recovered);
            Assert.Equal((ulong)M, head);
            Assert.Equal((ulong)M, bundle.Metadata.GetLastBlock());
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "reorg-rewind", "Boot integrity gate rolls a cursor-outran-flush torn head back to the highest resolvable block")]
        public async Task EnsureConsistentHead_CursorOutranFlush_RecoversToHighestResolvable()
        {
            const int M = 4;
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var roots = await BuildBlocks(bundle, M);

            var unflushedRoot = Hash(0xEE);
            await SaveHeader(bundle.Blocks, M + 1, Hash((byte)(M + 1)), unflushedRoot);
            bundle.Metadata.Commit((ulong)(M + 1), Hash((byte)(M + 1)));

            Assert.Equal((ulong)(M + 1), bundle.Metadata.GetLastBlock());
            Assert.False(bundle.StateTrieNodes.ContainsKey(unflushedRoot), "the head root must not resolve (torn)");
            Assert.True(bundle.StateTrieNodes.ContainsKey(roots[M]), "block M's root must still resolve");

            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(_ => { });

            Assert.True(recovered);
            Assert.Equal((ulong)M, head);
            Assert.Equal((ulong)M, bundle.Metadata.GetLastBlock());
            Assert.Equal(Hash((byte)M), bundle.Metadata.GetLastBlockHash());
            Assert.True(bundle.StateTrieNodes.ContainsKey(roots[M]), "the recovered head resolves");

            var verify = await bundle.VerifyFlatStateAsync(roots[M], _ => { }, default);
            Assert.Equal(0, verify.TotalRepairs);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "reorg-rewind", "Rewind ladder falls back from a failed trie patch to in-process node history recovery")]
        public async Task RewindCoordinator_TornTrie_PatchFails_FallsBackToNodeHistory()
        {
            const int M = 5;
            const ulong N = 2;
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var roots = await BuildBlocks(bundle, M);
            Assert.Equal((ulong)M, bundle.Metadata.GetLastBlock());

            _mgr.Put(RocksDbManager.CF_STATE_TRIE_ACCOUNT, Array.Empty<byte>(), new byte[40]);

            var result = await new RewindCoordinator(bundle)
                .RewindToAsync(N, RewindPolicy.JournalFirstThenSnapshot);

            Assert.Equal(RewindOutcome.NodeHistoryUsed, result.Outcome);
            Assert.Equal(N, result.NewHead);
            Assert.Equal(N, bundle.Metadata.GetLastBlock());
            Assert.Equal(Hash((byte)N), bundle.Metadata.GetLastBlockHash());
            Assert.True(bundle.StateTrieNodes.ContainsKey(roots[(int)N]), "the recovered head resolves at block N");

            var verify = await bundle.VerifyFlatStateAsync(roots[(int)N], _ => { }, default);
            Assert.Equal(0, verify.TotalRepairs);
        }

        private async Task<byte[][]> BuildBlocks(RocksDbChainStoreBundle bundle, int m)
        {
            var hist = (HistoricalStateStore)bundle.State;
            var roots = new byte[m + 1][];
            roots[0] = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), roots[0]);
            for (int b = 1; b <= m; b++)
            {
                bundle.NodeCommitBlockSource.Arm((ulong)b);
                hist.SetCurrentBlockNumber(b);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000 * b, Nonce = b });
                var contract = ContractAddr(b);
                await hist.SaveAccountAsync(contract, new Account { Balance = 0, Nonce = 1 });
                await hist.SaveStorageAsync(contract, BigInteger.One, new byte[] { (byte)(0x10 + b) });
                roots[b] = await NewCalc(bundle).ComputeStateRootAsync(roots[b - 1]);
                await hist.ClearCurrentBlockNumberAsync();
                bundle.NodeCommitBlockSource.Clear();
                await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, Hash((byte)b));
                await ((IAtomicBlockFlush)bundle).DrainAsync();
                await SaveHeader(bundle.Blocks, (ulong)b, Hash((byte)b), roots[b]);
                bundle.Metadata.Commit((ulong)b, Hash((byte)b));
            }
            return roots;
        }

        private static IIncrementalStateRootCalculator NewCalc(IChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes);

        private static string ContractAddr(int i) => "0x" + i.ToString("x2") + new string('c', 38);

        private static async Task SaveHeader(IBlockStore blocks, ulong number, byte[] hash, byte[] stateRoot)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = number == 0 ? new byte[32] : Hash((byte)(number - 1)),
                StateRoot = stateRoot,
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            await blocks.SaveAsync(header, hash);
        }

        private static byte[] Hash(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        private int CountNodeHistoryTotal()
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
