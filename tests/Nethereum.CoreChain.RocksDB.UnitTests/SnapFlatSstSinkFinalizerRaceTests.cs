using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class SnapFlatSstSinkFinalizerRaceTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sstsink_race_{Guid.NewGuid():N}");
        private readonly RocksDbManager _mgr;
        private readonly RocksDbStateStore _store;

        public SnapFlatSstSinkFinalizerRaceTests()
        {
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _store = new RocksDbStateStore(_mgr);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private static byte[] Key(int writer, int i)
        {
            var k = new byte[32];
            BitConverter.GetBytes(writer).CopyTo(k, 0);
            BitConverter.GetBytes(i).CopyTo(k, 4);
            k[31] = 0x5A;
            return k;
        }

        [Fact]
        public async Task Given_ConcurrentWritersForcingFrequentIngests_When_TheGcAndFinalizersRunContinuously_Then_EveryRowReadsBackExactly()
        {
            const int writers = 8;
            const int rowsPerWriter = 4_000;
            var owner = new byte[32];
            owner[0] = 0x42;

            using var stop = new CancellationTokenSource();
            var collector = Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                    GC.WaitForPendingFinalizers();
                }
            });

            using (var sink = new SnapFlatSstSink(_mgr, _store, Path.Combine(_dir, "sst-scratch"), flushEntryThreshold: 50))
            {
                await Task.WhenAll(Enumerable.Range(0, writers).Select(w => Task.Run(async () =>
                {
                    for (int i = 0; i < rowsPerWriter; i++)
                        await sink.SaveStorageByHashAsync(owner, Key(w, i), new byte[] { (byte)w, (byte)(i & 0xFF), 0x77 });
                })));
                sink.Flush();
            }

            stop.Cancel();
            await collector;

            for (int w = 0; w < writers; w++)
            for (int i = 0; i < rowsPerWriter; i++)
            {
                var slotKey = new byte[64];
                Buffer.BlockCopy(owner, 0, slotKey, 0, 32);
                Buffer.BlockCopy(Key(w, i), 0, slotKey, 32, 32);
                var stored = _mgr.Get(RocksDbManager.CF_STATE_STORAGE, slotKey);
                Assert.NotNull(stored);
                Assert.Equal(new byte[] { (byte)w, (byte)(i & 0xFF), 0x77 }, stored);
            }
        }
    }
}
