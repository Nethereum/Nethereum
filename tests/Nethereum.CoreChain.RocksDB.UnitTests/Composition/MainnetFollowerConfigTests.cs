using System;
using System.IO;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Composition
{
    public class MainnetFollowerConfigTests : IDisposable
    {
        private readonly string _dir;
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public MainnetFollowerConfigTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-mainnetfollower-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public async System.Threading.Tasks.Task MainnetFollower_WithJournal_builds_Historical_over_Buffered_over_Rocks()
        {
            using (var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir }))
            {
                var rawState = new RocksDbStateStore(mgr);
                var diffStore = new RocksDbStateDiffStore(mgr);

                var wired = new StateLayer().Stores.MainnetFollower(rawState, diffStore, HistoricalStateOptions.FullArchive);

                var hist = Assert.IsType<HistoricalStateStore>(wired);

                hist.SetCurrentBlockNumber(1);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1234, Nonce = 1 });

                Assert.Null(await rawState.GetAccountAsync(AddrA));

                await hist.ClearCurrentBlockNumberAsync();
            }

            using (var mgr2 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir }))
            {
                var raw2 = new RocksDbStateStore(mgr2);
                var a = await raw2.GetAccountAsync(AddrA);
                Assert.NotNull(a);
                Assert.Equal((EvmUInt256)1234, a.Balance);
            }
        }

        [Fact]
        public void MainnetFollower_NullJournal_returns_bare_rawFlatState()
        {
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var rawState = new RocksDbStateStore(mgr);
            var diffStore = new RocksDbStateDiffStore(mgr);

            var wired = new StateLayer().Stores.MainnetFollower(rawState, diffStore, null);

            Assert.Same(rawState, wired);
        }
    }
}
