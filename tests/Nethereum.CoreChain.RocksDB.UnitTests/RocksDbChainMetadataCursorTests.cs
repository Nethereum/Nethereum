using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbChainMetadataCursorTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"metacursor_{Guid.NewGuid():N}");
        private readonly List<IDisposable> _owned = new();

        public RocksDbChainMetadataCursorTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            foreach (var owned in _owned) owned.Dispose();
            try { Directory.Delete(_root, recursive: true); } catch { }
        }

        private RocksDbManager NewManager(string name)
        {
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = Path.Combine(_root, name) });
            _owned.Add(manager);
            return manager;
        }

        private static byte[] Hash(byte fill) => Enumerable.Repeat(fill, 32).ToArray();

        public static IEnumerable<object[]> Cursors()
        {
            yield return new object[] { "last_block", (Func<RocksDbChainMetadataStore, ulong>)(s => s.GetLastBlock()) };
            yield return new object[] { "last_fetched_header", (Func<RocksDbChainMetadataStore, ulong>)(s => s.GetLastFetchedHeader()) };
            yield return new object[] { "last_fetched_body", (Func<RocksDbChainMetadataStore, ulong>)(s => s.GetLastFetchedBody()) };
            yield return new object[] { "receipt_backfill_cursor", (Func<RocksDbChainMetadataStore, ulong>)(s => s.GetReceiptBackfillCursor()) };
            yield return new object[] { "durable_state_block", (Func<RocksDbChainMetadataStore, ulong>)(s => s.GetDurableStateBlock()) };
            yield return new object[] { "promotion_cursor", (Func<RocksDbChainMetadataStore, ulong>)(s => s.GetPromotionCursor()) };
        }

        [Theory]
        [MemberData(nameof(Cursors))]
        public void Given_ACursorValueOfTheWrongLength_When_ItIsRead_Then_ZeroIsReturned(
            string key, Func<RocksDbChainMetadataStore, ulong> read)
        {
            var manager = NewManager("wronglength");
            var store = new RocksDbChainMetadataStore(manager);
            var keyBytes = System.Text.Encoding.ASCII.GetBytes(key);

            foreach (var length in new[] { 0, 1, 7, 9, 32 })
            {
                manager.Put(RocksDbManager.CF_METADATA, keyBytes, new byte[length].Select(_ => (byte)0xFF).ToArray());
                Assert.Equal(0UL, read(store));
            }

            manager.Put(RocksDbManager.CF_METADATA, keyBytes, RocksDbManager.Write64BE(42));
            Assert.Equal(42UL, read(store));
        }

        [Fact]
        public void Given_AHashThatIsNot32Bytes_When_CommitIsCalled_Then_OnlyTheNumberIsWritten()
        {
            var store = new RocksDbChainMetadataStore(NewManager("hashlength"));

            store.Commit(7, new byte[31]);
            Assert.Equal(7UL, store.GetLastBlock());
            Assert.Null(store.GetLastBlockHash());

            store.Commit(8, null);
            Assert.Equal(8UL, store.GetLastBlock());
            Assert.Null(store.GetLastBlockHash());

            store.Commit(9, Hash(0x5A));
            Assert.Equal(9UL, store.GetLastBlock());
            Assert.Equal(Hash(0x5A), store.GetLastBlockHash());
        }

        [Fact]
        public async Task Given_ABundleBatchCommit_When_Flushed_Then_LastBlockAndLastBlockHashEqualMetadataStoreCommit()
        {
            var batchBundle = RocksDbChainStoreBundle.Open(Path.Combine(_root, "batch"));
            _owned.Add(batchBundle);
            var direct = new RocksDbChainMetadataStore(NewManager("direct"));

            using (var batch = batchBundle.BeginBatch())
            {
                batch.Commit(11, Hash(0x77));
                await batch.CommitAsync();
            }
            direct.Commit(11, Hash(0x77));

            Assert.Equal(direct.GetLastBlock(), batchBundle.Metadata.GetLastBlock());
            Assert.Equal(direct.GetLastBlockHash(), batchBundle.Metadata.GetLastBlockHash());
            Assert.Equal(11UL, batchBundle.Metadata.GetLastBlock());
            Assert.Equal(Hash(0x77), batchBundle.Metadata.GetLastBlockHash());
        }
    }
}
