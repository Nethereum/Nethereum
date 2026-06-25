using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Forks
{
    public class Eip2935HelpersTests
    {
        [Fact]
        public async Task ApplyAsync_ParentHashWithLeadingZero_StoresFull32Bytes()
        {
            var store = new InMemoryStateStore();
            var parentBlockNumber = (BigInteger)1_000_000;
            var parentHash = new byte[32];
            parentHash[1] = 0xaa;
            parentHash[2] = 0xbb;
            parentHash[30] = 0x00;
            parentHash[31] = 0x11;

            await Eip2935Helpers.ApplyAsync(store, parentBlockNumber, parentHash);

            var slot = Eip2935Helpers.ComputeSlot(parentBlockNumber, Eip2935Constants.HistoryServeWindow);
            var stored = await store.GetStorageAsync(Eip2935Constants.HistoryStorageAddress, slot);

            Assert.NotNull(stored);
            Assert.Equal(32, stored.Length);
            Assert.True(parentHash.SequenceEqual(stored));
            Assert.Equal((byte)0x00, stored[0]);
        }

        [Fact]
        public void ComputeSlot_RingBufferLayout_ParentNumberModuloWindow()
        {
            var parentNumber = (BigInteger)16_383;
            var slot = Eip2935Helpers.ComputeSlot(parentNumber, Eip2935Constants.HistoryServeWindow);
            Assert.Equal((BigInteger)(16_383 % 8191), slot);
        }
    }
}
