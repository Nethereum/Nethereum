using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class PathKeyedReorgRewindTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private const string CDeep = "0x00000000000000000000000000000000000000ca";

        public PathKeyedReorgRewindTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-reorg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir, PathKeyedState = true, TrieNodeHistoryBlocks = 128, TrieNodeHistoryIndex = true,
            });
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "reorg-rewind", "Rewind a deep-trie reorg to the fork point and replay a new fork")]
        public async Task DeepTrie_Reorg_RewindsHeadersTrieState_ThenReplaysNewFork()
        {
            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var roots = new byte[16][];

            roots[0] = await NewCalc(bundle).ComputeStateRootAsync();
            await SaveHeader(bundle.Blocks, 0, Hash(0), roots[0]);

            for (int b = 1; b <= 3; b++)
            {
                await ApplyBlock(bundle, hist, roots, b, async () =>
                {
                    for (int i = (b - 1) * 20; i < b * 20; i++)
                        await hist.SaveAccountAsync(Addr(i), new Account { Balance = 1000 + i, Nonce = 1 });
                    if (b == 1) await hist.SaveAccountAsync(CDeep, new Account { Balance = 0, Nonce = 1 });
                    for (int s = (b - 1) * 27; s < b * 27 && s < 80; s++)
                        await hist.SaveStorageAsync(CDeep, s + 1, ValueBytes(s + 1));
                });
            }
            const ulong forkPoint = 3;
            var forkRoot = roots[forkPoint];

            for (int b = 4; b <= 6; b++)
            {
                await ApplyBlock(bundle, hist, roots, b, async () =>
                {
                    for (int i = 60 + (b - 4) * 5; i < 60 + (b - 3) * 5; i++)
                        await hist.SaveAccountAsync(Addr(i), new Account { Balance = 5000 + i, Nonce = 2 });
                    for (int s = (b - 4) * 12; s < (b - 3) * 12; s++)
                        await hist.SaveStorageAsync(CDeep, s + 1, Array.Empty<byte>());
                });
            }
            var headBeforeRewind = bundle.Metadata.GetLastBlock();
            Assert.Equal(6UL, headBeforeRewind);

            var result = await new RewindCoordinator(bundle).RewindToAsync(forkPoint, RewindPolicy.JournalOnly);
            Assert.Equal(RewindOutcome.NodeHistoryUsed, result.Outcome);

            Assert.Equal(forkPoint, bundle.Metadata.GetLastBlock());

            var latest = ((ILatestProofServingBundle)bundle).LatestProofNodeStore;
            Assert.True(latest.ContainsKey(forkRoot), "latest path store must serve the fork-point root after rewind");
            var trie = PatriciaTrie.LoadFromStorage(forkRoot, bundle.StateTrieNodes);
            Assert.Equal(forkRoot, trie.Root.GetHash());

            Assert.NotNull(trie.Get(StateKeys.AccountKey(Addr(0))));
            Assert.NotNull(trie.Get(StateKeys.AccountKey(Addr(59))));
            Assert.Null(trie.Get(StateKeys.AccountKey(Addr(60))));
            Assert.Null(trie.Get(StateKeys.AccountKey(Addr(74))));

            var oracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.Equal(forkRoot, oracle);

            var newRoots = new byte[9][];
            newRoots[(int)forkPoint] = forkRoot;
            for (int b = 4; b <= 8; b++)
            {
                await ApplyBlockV2(bundle, hist, newRoots, b, forkPoint, async () =>
                {
                    for (int i = 100 + b; i < 100 + b + 3; i++)
                        await hist.SaveAccountAsync(Addr(i), new Account { Balance = 9000 + i, Nonce = 3 });
                });
            }
            Assert.Equal(8UL, bundle.Metadata.GetLastBlock());
            var newHead = newRoots[8];
            Assert.True(latest.ContainsKey(newHead), "latest path store must serve the new fork's head root");
            var newTrie = PatriciaTrie.LoadFromStorage(newHead, bundle.StateTrieNodes);
            Assert.Equal(newHead, newTrie.Root.GetHash());
            var newOracle = await new IncrementalStateRootCalculator(bundle.State, new InMemoryContentNodeStore())
                .ComputeFullStateRootAsync();
            Assert.Equal(newHead, newOracle);
        }

        private static IIncrementalStateRootCalculator NewCalc(IChainStoreBundle bundle)
            => new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));

        private static async Task ApplyBlock(
            RocksDbChainStoreBundle bundle, HistoricalStateStore hist, byte[][] roots, int b, Func<Task> mutations)
        {
            bundle.NodeCommitBlockSource.Arm((ulong)b);
            hist.SetCurrentBlockNumber(b);
            await mutations();
            roots[b] = await NewCalc(bundle).ComputeStateRootAsync(roots[b - 1]);
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, Hash((byte)b));
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            await SaveHeader(bundle.Blocks, (ulong)b, Hash((byte)b), roots[b]);
            bundle.Metadata.Commit((ulong)b, Hash((byte)b));
        }

        private static async Task ApplyBlockV2(
            RocksDbChainStoreBundle bundle, HistoricalStateStore hist, byte[][] roots, int b, ulong forkPoint, Func<Task> mutations)
        {
            bundle.NodeCommitBlockSource.Arm((ulong)b);
            hist.SetCurrentBlockNumber(b);
            await mutations();
            roots[b] = await NewCalc(bundle).ComputeStateRootAsync(roots[b - 1]);
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, HashV2((byte)b));
            await ((IAtomicBlockFlush)bundle).DrainAsync();
            await SaveHeader(bundle.Blocks, (ulong)b, HashV2((byte)b), roots[b]);
            bundle.Metadata.Commit((ulong)b, HashV2((byte)b));
        }

        private static async Task SaveHeader(IBlockStore blocks, ulong number, byte[] hash, byte[] stateRoot)
            => await blocks.SaveAsync(new BlockHeader
            {
                BlockNumber = number, ParentHash = number == 0 ? new byte[32] : Hash((byte)(number - 1)),
                StateRoot = stateRoot, TransactionsHash = new byte[32], ReceiptHash = new byte[32],
                UnclesHash = new byte[32], ExtraData = Array.Empty<byte>(), LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000", Difficulty = 0, GasLimit = 0,
                GasUsed = 0, Timestamp = 0, MixHash = new byte[32], Nonce = new byte[8],
            }, hash);

        private static string Addr(int i) => "0x" + i.ToString("x").PadLeft(40, '0');
        private static byte[] ValueBytes(int v)
        {
            var b = new BigInteger(v).ToByteArray(isUnsigned: true, isBigEndian: true);
            return b.Length == 0 ? new byte[] { 0 } : b;
        }
        private static byte[] Hash(byte f) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = f; return h; }
        private static byte[] HashV2(byte f) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(f ^ 0xa5); return h; }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }
    }
}
