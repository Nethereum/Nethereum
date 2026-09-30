using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class WindowDeleteThenClearJournalTests
    {
        private const string Addr1 = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task GetAllStorageAsync_SlotDeletedButAddressNotCleared_MasksStaleDiskRow()
        {
            var inner = new InMemoryStateStore();
            await inner.SaveStorageAsync(Addr1, 5, new byte[] { 0x0A });

            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();

            await buffered.SaveStorageAsync(Addr1, 5, null);

            var all = await buffered.GetAllStorageAsync(Addr1);

            Assert.False(all.ContainsKey(StateKeys.StorageSlotKey(5)),
                "deleted-but-not-yet-flushed slot must not surface a stale disk value to a later in-window reader");
        }

        [Fact]
        public async Task ClearStorageAsync_AfterInWindowSlotDelete_JournalsNoStalePreValue_ForAlreadyDeletedSlot()
        {
            var inner = new InMemoryStateStore();
            await inner.SaveStorageAsync(Addr1, 7, new byte[] { 0x0A });

            var buffered = new BufferedFlatStateStore(inner);
            var diffStore = new InMemoryStateDiffStore();
            var historical = new HistoricalStateStore(buffered, diffStore, HistoricalStateOptions.FullArchive);

            buffered.BeginBuffering();

            historical.SetCurrentBlockNumber(1);
            await historical.SaveStorageAsync(Addr1, 7, new byte[] { 0x00 });
            await historical.RecordBlockDiffAsync();

            historical.SetCurrentBlockNumber(2);
            await historical.ClearStorageAsync(Addr1);
            await historical.RecordBlockDiffAsync();

            var diffAtBlock2 = await diffStore.GetBlockDiffAsync(2);
            var slotKeyBytes = StateKeys.StorageSlotKey(7);
            var staleEntry = diffAtBlock2?.StorageDiffs.FirstOrDefault(d => d.SlotKey.SequenceEqual(slotKeyBytes));
            Assert.Null(staleEntry);
        }


        [Fact]
        public async Task GetAllStorageAsync_WithinSingleBlock_SlotDeletedThenReadInSameBlock_MasksStaleDiskRow_AtK1()
        {
            var inner = new InMemoryStateStore();
            await inner.SaveStorageAsync(Addr1, 5, new byte[] { 0x0A });

            var buffered = new BufferedFlatStateStore(inner);
            buffered.BeginBuffering();

            await buffered.SaveStorageAsync(Addr1, 5, null);

            var all = await buffered.GetAllStorageAsync(Addr1);

            Assert.False(all.ContainsKey(StateKeys.StorageSlotKey(5)),
                "a slot deleted earlier in the SAME K=1 block must not surface the stale disk value to a later read in that block");
        }

        [Fact]
        public async Task ClearStorageAsync_WithinSameBlock_AfterSlotDelete_JournalsTruePreBlockValue_AtK1()
        {
            var inner = new InMemoryStateStore();
            await inner.SaveStorageAsync(Addr1, 9, new byte[] { 0x0A });

            var buffered = new BufferedFlatStateStore(inner);
            var diffStore = new InMemoryStateDiffStore();
            var historical = new HistoricalStateStore(buffered, diffStore, HistoricalStateOptions.FullArchive);

            buffered.BeginBuffering();
            historical.SetCurrentBlockNumber(1);

            await historical.SaveStorageAsync(Addr1, 9, new byte[] { 0x00 });
            await historical.ClearStorageAsync(Addr1);
            await historical.RecordBlockDiffAsync();

            var diffAtBlock1 = await diffStore.GetBlockDiffAsync(1);
            var slotKeyBytes = StateKeys.StorageSlotKey(9);
            var entry = diffAtBlock1?.StorageDiffs.FirstOrDefault(d => d.SlotKey.SequenceEqual(slotKeyBytes));

            Assert.NotNull(entry);
            Assert.Equal(new byte[] { 0x0A }, entry.PreValue);
        }
    }
}
