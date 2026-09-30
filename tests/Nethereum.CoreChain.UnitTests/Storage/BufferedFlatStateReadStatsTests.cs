using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class BufferedFlatStateReadStatsTests
    {
        private const string Addr = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Fact]
        public async Task Reads_AreCounted_AcrossDiskAndOverlayTiers()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            var stats = (IStateReadStats)buffered;

            Assert.Equal(0, stats.AccountReads);
            Assert.Equal(0, stats.StorageReads);

            await buffered.GetAccountAsync(Addr);
            await buffered.GetStorageAsync(Addr, 1);
            await buffered.GetStorageAsync(Addr, 2);
            Assert.Equal(1, stats.AccountReads);
            Assert.Equal(2, stats.StorageReads);

            buffered.BeginBuffering();
            await buffered.SaveAccountAsync(Addr, new Account { Nonce = 1, Balance = 5 });
            await buffered.SaveStorageAsync(Addr, 1, new byte[] { 0x7 });
            var a = await buffered.GetAccountAsync(Addr);
            var s = await buffered.GetStorageAsync(Addr, 1);
            Assert.NotNull(a);
            Assert.Equal(new byte[] { 0x7 }, s);
            Assert.Equal(2, stats.AccountReads);
            Assert.Equal(3, stats.StorageReads);
        }

        [Fact]
        public async Task HistoricalStateStore_ForwardsReadCounts_FromInnerBufferedStore()
        {
            var inner = new InMemoryStateStore();
            var buffered = new BufferedFlatStateStore(inner);
            var historical = new HistoricalStateStore(buffered);
            var stats = (IStateReadStats)historical;

            Assert.Equal(0, stats.AccountReads);
            Assert.Equal(0, stats.StorageReads);

            await historical.GetAccountAsync(Addr);
            await historical.GetStorageAsync(Addr, 1);
            await historical.GetStorageAsync(Addr, 2);

            Assert.Equal(1, stats.AccountReads);
            Assert.Equal(2, stats.StorageReads);
        }

        [Fact]
        public void ForwardingIsInert_WhenInnerDoesNotCount()
        {
            var historical = new HistoricalStateStore(new InMemoryStateStore());
            var stats = (IStateReadStats)historical;
            Assert.Equal(0, stats.AccountReads);
            Assert.Equal(0, stats.StorageReads);
        }
    }
}
