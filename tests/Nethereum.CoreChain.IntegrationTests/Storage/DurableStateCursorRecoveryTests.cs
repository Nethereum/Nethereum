using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Storage
{
    public class DurableStateCursorRecoveryTests : IDisposable
    {
        private readonly string _dir;
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public DurableStateCursorRecoveryTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dscr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { }
        }

        private RocksDbChainStoreBundle OpenBundle(string dbDir)
        {
            Directory.CreateDirectory(dbDir);
            return RocksDbChainStoreBundle.Open(
                dbDir,
                journalOptions: HistoricalStateOptions.FullArchive,
                storageOptions: new RocksDbStorageOptions
                {
                    DatabasePath = dbDir,
                    PathKeyedState = true,
                    TrieNodeHistoryBlocks = 128,
                    TrieNodeHistoryIndex = true,
                });
        }

        [Fact]
        public async Task CommittedBlocks_DurableCursorTracksExecutedHead()
        {
            var dbDir = Path.Combine(_dir, "db1");
            using var bundle = OpenBundle(dbDir);

            byte[] parentRoot = null;
            for (ulong b = 1; b <= 5; b++)
            {
                var (root, hash) = await CommitCleanBlockAsync(bundle, b, parentRoot);
                parentRoot = root;

                Assert.Equal(bundle.Metadata.GetLastBlock(), bundle.Metadata.GetDurableStateBlock());
                Assert.Equal(b, bundle.Metadata.GetDurableStateBlock());
                Assert.Equal(hash, bundle.Metadata.GetLastBlockHash());
            }
        }

        [Fact]
        public async Task ExecutedHeadAheadOfDurableCursor_ServesFromDurableCursor_NoReconcile()
        {
            var dbDir = Path.Combine(_dir, "db2");
            using var bundle = OpenBundle(dbDir);

            byte[] parentRoot = null;
            for (ulong b = 1; b <= 3; b++)
            {
                var (root, _) = await CommitCleanBlockAsync(bundle, b, parentRoot);
                parentRoot = root;
            }

            var tornHash = FillHash(0xEE);
            var tornRoot = FillHash(0xFA);
            await SaveHeaderAsync(bundle.Blocks, 4, FillHash((byte)3), tornHash, tornRoot);
            bundle.Metadata.Commit(4, tornHash);
            Assert.False(bundle.StateTrieNodes.ContainsKey(tornRoot));

            var messages = new List<string>();
            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(messages.Add);

            Assert.Equal(3UL, head);
            Assert.True(recovered);
            Assert.Contains(messages, m => m.Contains("integrity.durable-cursor"));
            Assert.DoesNotContain(messages, m => m.Contains("integrity.recovering torn head"));
            Assert.Equal(3UL, bundle.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task DurableCursorAlsoTorn_FallsThroughToExistingReconcile()
        {
            var dbDir = Path.Combine(_dir, "db3");
            using var bundle = OpenBundle(dbDir);

            byte[] parentRoot = null;
            for (ulong b = 1; b <= 2; b++)
            {
                var (root, _) = await CommitCleanBlockAsync(bundle, b, parentRoot);
                parentRoot = root;
            }

            var tornHash3 = FillHash(0xCC);
            var tornRoot3 = FillHash(0xFB);
            await SaveHeaderAsync(bundle.Blocks, 3, FillHash((byte)2), tornHash3, tornRoot3);
            bundle.Metadata.Commit(3, tornHash3);
            bundle.Metadata.CommitDurableState(3, tornHash3);
            Assert.False(bundle.StateTrieNodes.ContainsKey(tornRoot3));

            var tornHash4 = FillHash(0xDD);
            var tornRoot4 = FillHash(0xFC);
            await SaveHeaderAsync(bundle.Blocks, 4, tornHash3, tornHash4, tornRoot4);
            bundle.Metadata.Commit(4, tornHash4);
            Assert.False(bundle.StateTrieNodes.ContainsKey(tornRoot4));

            var messages = new List<string>();
            var (head, recovered) = await bundle.EnsureConsistentHeadAsync(messages.Add);

            Assert.True(recovered);
            Assert.Equal(2UL, head);
            Assert.Contains(messages, m => m.Contains("integrity.recovering torn head"));
            Assert.DoesNotContain(messages, m => m.Contains("integrity.durable-cursor"));
            Assert.Equal(2UL, bundle.Metadata.GetLastBlock());
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
            bundle.Metadata.CommitDurableState(blockNumber, hash);

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
    }
}
