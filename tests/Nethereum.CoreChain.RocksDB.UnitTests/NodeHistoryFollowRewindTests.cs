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
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class NodeHistoryFollowRewindTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public NodeHistoryFollowRewindTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-nh-follow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 128,
            });
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "reorg-rewind", "Follow-mode journals each block once and rewind patches without double-journaling")]
        public async Task Follow_WithNodeHistory_Journals_PerBlock_And_Rewind_Patches_Without_DoubleJournal()
        {
            const int M = 4;
            const ulong N = 2;

            using var bundle = RocksDbChainStoreBundle.FromManager(
                _mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);

            Assert.IsType<CapturingJournalingPathNodeStore>(bundle.StateTrieNodes);
            Assert.NotNull(bundle.NodeCommitBlockSource);

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

            for (ulong b = 1; b <= M; b++)
                Assert.True(CountNodeHistoryAt(b) > 0, $"expected node-history entry for followed block {b}");

            var historyBeforeRewind = CountNodeHistoryTotal();

            var coordinator = new RewindCoordinator(bundle);
            var result = await coordinator.RewindToAsync(N, RewindPolicy.JournalOnly);

            Assert.Equal(RewindOutcome.NodeHistoryUsed, result.Outcome);
            Assert.Equal(N, bundle.Metadata.GetLastBlock());

            var targetHeader = await bundle.Blocks.GetByNumberAsync(N);
            var trie = PatriciaTrie.LoadFromStorage(roots[(int)N], bundle.StateTrieNodes);
            Assert.NotNull(trie.Get(StateKeys.AccountKey(AddrA)));
            Assert.NotNull(trie.Get(StateKeys.AccountKey(ContractAddr(1))));
            Assert.NotNull(trie.Get(StateKeys.AccountKey(ContractAddr(2))));
            Assert.Null(trie.Get(StateKeys.AccountKey(ContractAddr(3))));
            Assert.Null(trie.Get(StateKeys.AccountKey(ContractAddr(4))));
            Assert.Equal(targetHeader.StateRoot, trie.Root.GetHash());

            Assert.Equal(historyBeforeRewind, CountNodeHistoryTotal());
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

        private int CountNodeHistoryAt(ulong block)
        {
            var be = RocksDbManager.Write64BE(block);
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                var k = it.Key();
                bool match = k.Length >= 8;
                for (int i = 0; i < 8 && match; i++) if (k[i] != be[i]) match = false;
                if (match) n++;
                it.Next();
            }
            return n;
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
