using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class CursorBehindTrieRecoveryTests : IDisposable
    {
        private readonly string _dir;
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public CursorBehindTrieRecoveryTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "cbtr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }

        private RocksDbChainStoreBundle OpenBundle(string dbDir, int trieNodeHistoryBlocks)
        {
            Directory.CreateDirectory(dbDir);
            return RocksDbChainStoreBundle.Open(
                dbDir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    DatabasePath = dbDir,
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = trieNodeHistoryBlocks,
                    TrieNodeHistoryIndex = true,
                });
        }

        private static async Task<(byte[] Root, byte[] Hash)> CommitCleanBlockAsync(
            RocksDbChainStoreBundle bundle, ulong blockNumber, byte[] parentRoot)
        {
            var hist = (HistoricalStateStore)bundle.State;
            bundle.NodeCommitBlockSource?.Arm(blockNumber);
            hist.SetCurrentBlockNumber((int)blockNumber);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000 * (long)blockNumber, Nonce = (long)blockNumber });
            var root = await new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes)
                .ComputeStateRootAsync(parentRoot);
            await hist.ClearCurrentBlockNumberAsync();
            bundle.NodeCommitBlockSource?.Clear();

            var hash = FillHash((byte)blockNumber);

            if (bundle.NodeCommitBlockSource != null)
                await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, blockNumber, hash);
                await ((IAtomicBlockFlush)bundle).DrainAsync();

            var parentHash = blockNumber == 1 ? new byte[32] : FillHash((byte)(blockNumber - 1));
            await SaveHeaderAsync(bundle.Blocks, blockNumber, parentHash, hash, root);

            bundle.Metadata.Commit(blockNumber, hash);

            Assert.True(bundle.StateTrieNodes.ContainsKey(root));
            return (root, hash);
        }

        private static async Task SaveHeaderAsync(
            IBlockStore blocks, ulong number, byte[] parentHash, byte[] hash, byte[] stateRoot)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parentHash,
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

        private static byte[] FillHash(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        [Fact]
        public async Task StaleCursorBehindTrie_AdvancesToResolvableTrieHead_NoRollbackNoReconcile()
        {
            const ulong A = 3, B = 10;
            var dbDir = Path.Combine(_dir, "db1");
            using var bundle = OpenBundle(dbDir, trieNodeHistoryBlocks: 4);

            byte[] parentRoot = null;
            byte[] rootB = null;
            byte[] hashA = null, hashB = null;
            for (ulong b = 1; b <= B; b++)
            {
                var (root, hash) = await CommitCleanBlockAsync(bundle, b, parentRoot);
                parentRoot = root;
                if (b == A) hashA = hash;
                if (b == B) { rootB = root; hashB = hash; }
            }
            bundle.Metadata.SetLastFetchedHeaderAndBody(B, B);
            Assert.True(bundle.StateTrieNodes.ContainsKey(rootB));

            bundle.Metadata.Commit(A, hashA);
            Assert.Equal(A, bundle.Metadata.GetLastBlock());
            Assert.True(bundle.StateTrieNodes.ContainsKey(rootB), "trie must still hold B's state — untouched by the regression");

            var messages = new List<string>();
            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(messages.Add);

            Assert.True(recovered);
            Assert.Equal(B, head);
            Assert.Equal(B, bundle.Metadata.GetLastBlock());
            Assert.Equal(hashB, bundle.Metadata.GetLastBlockHash());
            Assert.Contains(messages, m => m.Contains("integrity.cursor-behind-trie"));
            Assert.DoesNotContain(messages, m => m.Contains("integrity.recovering torn head"));
            Assert.DoesNotContain(messages, m => m.Contains("recover.rewind"));
            Assert.DoesNotContain(messages, m => m.Contains("recover.reconcile"));

            Assert.True(bundle.StateTrieNodes.ContainsKey(rootB));
            var acct = await bundle.State.GetAccountAsync(AddrA);
            Assert.Equal(1000L * (long)B, acct.Balance);
        }

        [Fact]
        public async Task StaleCursorBehindTrie_SelfHeals_WhenFetchCursorClampedBelowTrieHead()
        {
            const ulong A = 3, B = 10, clampedFetch = 6;
            var dbDir = Path.Combine(_dir, "db2");
            using var bundle = OpenBundle(dbDir, trieNodeHistoryBlocks: 4);

            byte[] parentRoot = null;
            byte[] rootB = null;
            byte[] hashA = null, hashB = null;
            for (ulong b = 1; b <= B; b++)
            {
                var (root, hash) = await CommitCleanBlockAsync(bundle, b, parentRoot);
                parentRoot = root;
                if (b == A) hashA = hash;
                if (b == B) { rootB = root; hashB = hash; }
            }
            bundle.Metadata.SetLastFetchedHeaderAndBody(clampedFetch, clampedFetch);
            bundle.Metadata.Commit(A, hashA);
            Assert.Equal(A, bundle.Metadata.GetLastBlock());
            Assert.True(bundle.StateTrieNodes.ContainsKey(rootB));

            var messages = new List<string>();
            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(messages.Add);

            Assert.True(recovered);
            Assert.Equal(B, head);
            Assert.Equal(B, bundle.Metadata.GetLastBlock());
            Assert.Equal(hashB, bundle.Metadata.GetLastBlockHash());
            Assert.Contains(messages, m => m.Contains("integrity.cursor-behind-trie"));
            Assert.DoesNotContain(messages, m => m.Contains("integrity.recovering torn head"));
            Assert.True(bundle.StateTrieNodes.ContainsKey(rootB));
            var acct = await bundle.State.GetAccountAsync(AddrA);
            Assert.Equal(1000L * (long)B, acct.Balance);
        }

        [Fact]
        public async Task StaleCursorBehindTrie_AdvancesToTrieHead_NotToHigherUnresolvableStoredHeaders()
        {
            const ulong A = 3, trieHead = 8, storedTip = 12, clampedFetch = 5;
            var dbDir = Path.Combine(_dir, "db3");
            using var bundle = OpenBundle(dbDir, trieNodeHistoryBlocks: 4);

            byte[] parentRoot = null;
            byte[] rootHead = null;
            byte[] hashA = null, hashTrieHead = null;
            for (ulong b = 1; b <= trieHead; b++)
            {
                var (root, hash) = await CommitCleanBlockAsync(bundle, b, parentRoot);
                parentRoot = root;
                if (b == A) hashA = hash;
                if (b == trieHead) { rootHead = root; hashTrieHead = hash; }
            }
            for (ulong b = trieHead + 1; b <= storedTip; b++)
                await SaveHeaderAsync(bundle.Blocks, b, FillHash((byte)(b - 1)), FillHash((byte)b), FillHash((byte)(0xE0 + b)));

            bundle.Metadata.SetLastFetchedHeaderAndBody(clampedFetch, clampedFetch);
            bundle.Metadata.Commit(A, hashA);
            Assert.Equal(A, bundle.Metadata.GetLastBlock());
            Assert.Equal((System.Numerics.BigInteger)storedTip, await bundle.Blocks.GetHeightAsync());

            var messages = new List<string>();
            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(messages.Add);

            Assert.True(recovered);
            Assert.Equal(trieHead, head);
            Assert.Equal(trieHead, bundle.Metadata.GetLastBlock());
            Assert.Equal(hashTrieHead, bundle.Metadata.GetLastBlockHash());
            Assert.Contains(messages, m => m.Contains("integrity.cursor-behind-trie"));
            Assert.True(bundle.StateTrieNodes.ContainsKey(rootHead));
        }
    }
}
