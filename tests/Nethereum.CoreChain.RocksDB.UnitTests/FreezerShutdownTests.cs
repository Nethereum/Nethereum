using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;
using static Nethereum.CoreChain.RocksDB.UnitTests.FreezerBlockFactory;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Collection(ConsoleErrorCaptureCollection.Name)]
    [Trait("Slice", "Freezer")]
    public class FreezerShutdownTests : IDisposable
    {
        private const int PendingEntries = 200_000;
        private const ulong FlushedBlock = 5;

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"freezershutdown_{Guid.NewGuid():N}");

        public void Dispose() => DeleteQuietly(_root);

        private static FlatStateBatch LargeBatch()
        {
            var accounts = new List<(string Address, Account Account)>(PendingEntries);
            for (var i = 0; i < PendingEntries; i++)
            {
                accounts.Add(("0x" + i.ToString("x40"), new Account
                {
                    Nonce = new EvmUInt256((ulong)(i + 1)),
                    Balance = new EvmUInt256((ulong)(i + 1)),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
            }
            return new FlatStateBatch(null, null, null, null, accounts, null);
        }

        [Fact]
        public async Task Given_ABundleThatDoesNotOwnItsManagerAndAPendingWrite_When_Disposed_Then_ThePendingWriteHasLanded()
        {
            Directory.CreateDirectory(DataDir(_root));
            var options = new RocksDbStorageOptions { DatabasePath = DataDir(_root), PathKeyedState = true };
            using var manager = new RocksDbManager(options);
            var bundle = RocksDbChainStoreBundle.FromManager(
                manager, DataDir(_root), journalOptions: null, ownsManager: false, bulkSync: false,
                flatStateCache: null, historyRocks: null, historyDataDir: null, signer: FreezerBlockFactory.Signer);
            var batch = LargeBatch();
            Task drain = Task.CompletedTask;
            try
            {
                await ((IAtomicBlockFlush)bundle).FlushBlockAsync(batch, FlushedBlock, Fill32((long)FlushedBlock))
                    .WaitAsync(TimeSpan.FromSeconds(60));
                drain = ((IAtomicBlockFlush)bundle).DrainAsync();

                await bundle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(60));

                Assert.Equal(FlushedBlock, bundle.Metadata.GetDurableStateBlock());
            }
            finally
            {
                try { await drain.WaitAsync(TimeSpan.FromSeconds(60)); } catch { }
            }
        }

        [Fact]
        public async Task Given_ARunningFreezerJobAndTrailer_When_TheBundleIsDisposed_Then_AllStoppedLinesPrecedeDisposeReturning()
        {
            using var capture = new ErrorCapture();
            var bundle = OpenFreezerBundle(_root, FreezerOptions(_root, backgroundIndexing: true));
            bundle.StartBackgroundFreezeIndexing();

            await bundle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(60));

            var afterDispose = capture.Text;
            Assert.Contains("freezer-promotion stopped", afterDispose);
            Assert.Contains("index-trailer byhash stopped", afterDispose);
            Assert.Contains("index-trailer render stopped", afterDispose);

            bundle.StartBackgroundFreezeIndexing();
            await Task.Delay(TimeSpan.FromSeconds(2));

            Assert.Equal(afterDispose, capture.Text);
        }

        [Fact]
        public async Task Given_ABulkSyncBundle_When_FinishBulkSyncRunsAfterDispose_Then_ItThrowsObjectDisposed()
        {
            var options = new RocksDbStorageOptions { PathKeyedState = true };
            var bundle = RocksDbChainStoreBundle.Open(DataDir(_root), null, true, options, FreezerBlockFactory.Signer);
            await bundle.PersistBlocksAsync(MakeChainedBlocks(0, 3)).WaitAsync(TimeSpan.FromSeconds(60));

            await bundle.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Throws<ObjectDisposedException>(() => bundle.FinishBulkSync());
        }
    }
}
