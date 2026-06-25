using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Forks
{
    public class Eip4788HelpersTests
    {
        [Fact]
        public async Task ApplyAsync_BeaconRootWithLeadingZero_StoresFull32Bytes()
        {
            var store = new InMemoryStateStore();
            var timestamp = (BigInteger)1_700_000_000;
            var beaconRoot = new byte[32];
            beaconRoot[1] = 0xaa;
            beaconRoot[2] = 0xbb;
            beaconRoot[30] = 0x00;
            beaconRoot[31] = 0x11;

            await Eip4788Helpers.ApplyAsync(store, timestamp, beaconRoot);

            var rootSlot = Eip4788Helpers.ComputeRootSlot(timestamp, Eip4788Constants.HistoryBufferLength);
            var stored = await store.GetStorageAsync(Eip4788Constants.BeaconRootsAddress, rootSlot);

            Assert.NotNull(stored);
            Assert.Equal(32, stored.Length);
            Assert.True(beaconRoot.SequenceEqual(stored));
            Assert.Equal((byte)0x00, stored[0]);
        }

        [Fact]
        public async Task ApplyAsync_TimestampSlot_StoresTimestampValue()
        {
            var store = new InMemoryStateStore();
            var timestamp = (BigInteger)1_700_000_000;
            var beaconRoot = new byte[32];

            await Eip4788Helpers.ApplyAsync(store, timestamp, beaconRoot);

            var timestampSlot = Eip4788Helpers.ComputeTimestampSlot(timestamp, Eip4788Constants.HistoryBufferLength);
            var stored = await store.GetStorageAsync(Eip4788Constants.BeaconRootsAddress, timestampSlot);

            Assert.NotNull(stored);
            var storedValue = new BigInteger(stored, isUnsigned: true, isBigEndian: true);
            Assert.Equal(timestamp, storedValue);
        }

        [Fact]
        public void ComputeSlots_RingBufferLayout_RootSlotIsTimestampPlusBufferLength()
        {
            var timestamp = (BigInteger)42;
            var timestampSlot = Eip4788Helpers.ComputeTimestampSlot(timestamp, Eip4788Constants.HistoryBufferLength);
            var rootSlot = Eip4788Helpers.ComputeRootSlot(timestamp, Eip4788Constants.HistoryBufferLength);

            Assert.Equal((BigInteger)42, timestampSlot);
            Assert.Equal((BigInteger)(42 + Eip4788Constants.HistoryBufferLength), rootSlot);
        }
    }
}
