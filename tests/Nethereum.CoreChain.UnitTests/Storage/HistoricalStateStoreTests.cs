using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage
{
    public class HistoricalStateStoreTests
    {
        private readonly InMemoryStateStore _inner;
        private readonly InMemoryStateDiffStore _diffStore;
        private readonly HistoricalStateStore _store;

        private const string Addr1 = "0x1111111111111111111111111111111111111111";
        private const string Addr2 = "0x2222222222222222222222222222222222222222";

        public HistoricalStateStoreTests()
        {
            _inner = new InMemoryStateStore();
            _diffStore = new InMemoryStateDiffStore();
            _store = new HistoricalStateStore(_inner, _diffStore, HistoricalStateOptions.FullArchive);
        }

        private async Task ProduceBlock(BigInteger blockNumber, string address, BigInteger newBalance)
        {
            _store.SetCurrentBlockNumber(blockNumber);
            await _store.SaveAccountAsync(address, new Account { Balance = newBalance, Nonce = blockNumber });
            await _store.ClearCurrentBlockNumberAsync();
        }

        [Fact]
        public async Task SingleBlock_QueryPreviousState_ReturnsPreValue()
        {
            await _store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            await ProduceBlock(1, Addr1, 200);

            var atBlock0 = await _store.GetAccountAtBlockAsync(Addr1, 0);
            Assert.Equal(100, atBlock0.Balance);

            var current = await _store.GetAccountAsync(Addr1);
            Assert.Equal(200, current.Balance);
        }

        [Fact]
        public async Task MultipleBlocks_QueryIntermediateState()
        {
            await _store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            await ProduceBlock(1, Addr1, 200);
            await ProduceBlock(2, Addr1, 300);
            await ProduceBlock(3, Addr1, 400);

            var atBlock0 = await _store.GetAccountAtBlockAsync(Addr1, 0);
            Assert.Equal(100, atBlock0.Balance);

            var atBlock1 = await _store.GetAccountAtBlockAsync(Addr1, 1);
            Assert.Equal(200, atBlock1.Balance);

            var atBlock2 = await _store.GetAccountAtBlockAsync(Addr1, 2);
            Assert.Equal(300, atBlock2.Balance);

            var current = await _store.GetAccountAsync(Addr1);
            Assert.Equal(400, current.Balance);
        }

        [Fact]
        public async Task UnmodifiedAccount_ReturnsCurrentState()
        {
            await _store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            await ProduceBlock(1, Addr2, 500);

            var atBlock0 = await _store.GetAccountAtBlockAsync(Addr1, 0);
            Assert.Equal(100, atBlock0.Balance);
        }

        [Fact]
        public async Task StorageHistory_TracksSlotChanges()
        {
            await _store.SaveStorageAsync(Addr1, 42, new byte[] { 0x01 });

            _store.SetCurrentBlockNumber(1);
            await _store.SaveStorageAsync(Addr1, 42, new byte[] { 0x02 });
            await _store.ClearCurrentBlockNumberAsync();

            var atBlock0 = await _store.GetStorageAtBlockAsync(Addr1, 42, 0);
            Assert.Equal(new byte[] { 0x01 }, atBlock0);

            var current = await _store.GetStorageAsync(Addr1, 42);
            Assert.Equal(new byte[] { 0x02 }, current);
        }

        [Fact]
        public async Task Pruning_RemovesOldDiffs()
        {
            var options = new HistoricalStateOptions
            {
                MaxHistoryBlocks = 3,
                EnablePruning = true,
                PruningIntervalBlocks = 1
            };
            var store = new HistoricalStateStore(_inner, _diffStore, options);

            await store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            store.SetCurrentBlockNumber(1);
            await store.SaveAccountAsync(Addr1, new Account { Balance = 200 });
            await store.ClearCurrentBlockNumberAsync();

            store.SetCurrentBlockNumber(2);
            await store.SaveAccountAsync(Addr1, new Account { Balance = 300 });
            await store.ClearCurrentBlockNumberAsync();

            store.SetCurrentBlockNumber(3);
            await store.SaveAccountAsync(Addr1, new Account { Balance = 400 });
            await store.ClearCurrentBlockNumberAsync();

            store.SetCurrentBlockNumber(4);
            await store.SaveAccountAsync(Addr1, new Account { Balance = 500 });
            await store.ClearCurrentBlockNumberAsync();

            store.SetCurrentBlockNumber(5);
            await store.SaveAccountAsync(Addr1, new Account { Balance = 600 });
            await store.ClearCurrentBlockNumberAsync();

            var oldest = await _diffStore.GetOldestDiffBlockAsync();
            Assert.NotNull(oldest);
            Assert.True(oldest >= 2, $"Oldest diff should have been pruned, was at block {oldest}");
        }

        [Fact]
        public async Task PurgeDiffsAbove_CleansReorgBlocks()
        {
            await _store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            await ProduceBlock(1, Addr1, 200);
            await ProduceBlock(2, Addr1, 300);
            await ProduceBlock(3, Addr1, 400);

            await _store.PurgeDiffsAboveBlockAsync(1);

            var newest = await _diffStore.GetNewestDiffBlockAsync();
            Assert.NotNull(newest);
            Assert.True(newest <= 1);
        }

        [Fact]
        public async Task DeleteAccount_CapturesPreValue()
        {
            await _store.SaveAccountAsync(Addr1, new Account { Balance = 500 });

            _store.SetCurrentBlockNumber(1);
            await _store.DeleteAccountAsync(Addr1);
            await _store.ClearCurrentBlockNumberAsync();

            var atBlock0 = await _store.GetAccountAtBlockAsync(Addr1, 0);
            Assert.NotNull(atBlock0);
            Assert.Equal(500, atBlock0.Balance);
        }

        [Fact]
        public async Task ClearStorage_CapturesAllSlots()
        {
            await _store.SaveStorageAsync(Addr1, 1, new byte[] { 0x0A });
            await _store.SaveStorageAsync(Addr1, 2, new byte[] { 0x0B });

            _store.SetCurrentBlockNumber(1);
            await _store.ClearStorageAsync(Addr1);
            await _store.ClearCurrentBlockNumberAsync();

            var slot1 = await _store.GetStorageAtBlockAsync(Addr1, 1, 0);
            var slot2 = await _store.GetStorageAtBlockAsync(Addr1, 2, 0);

            Assert.Equal(new byte[] { 0x0A }, slot1);
            Assert.Equal(new byte[] { 0x0B }, slot2);
        }

        [Fact]
        public async Task QueryCurrentBlock_ReturnsCurrentState()
        {
            await _store.SaveAccountAsync(Addr1, new Account { Balance = 100 });
            await ProduceBlock(1, Addr1, 200);

            var atBlock1 = await _store.GetAccountAtBlockAsync(Addr1, 1);
            Assert.Equal(200, atBlock1.Balance);
        }

        [Fact]
        public async Task BackwardCompat_DefaultConstructor_Works()
        {
            var store = new HistoricalStateStore(new InMemoryStateStore());
            await store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            store.SetCurrentBlockNumber(1);
            await store.SaveAccountAsync(Addr1, new Account { Balance = 200 });
            await store.ClearCurrentBlockNumberAsync();

            var atBlock0 = await store.GetAccountAtBlockAsync(Addr1, 0);
            Assert.Equal(100, atBlock0.Balance);
        }

        [Fact]
        public async Task PrunedBlock_Throws_HistoricalStateNotAvailable()
        {
            var diffStore = new InMemoryStateDiffStore();
            var inner = new InMemoryStateStore();
            var options = new HistoricalStateOptions
            {
                MaxHistoryBlocks = 3,
                EnablePruning = true,
                PruningIntervalBlocks = 1
            };
            var store = new HistoricalStateStore(inner, diffStore, options);

            await store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            for (int i = 1; i <= 10; i++)
            {
                store.SetCurrentBlockNumber(i);
                await store.SaveAccountAsync(Addr1, new Account { Balance = 100 + i * 10 });
                await store.ClearCurrentBlockNumberAsync();
            }

            var oldest = await diffStore.GetOldestDiffBlockAsync();
            Assert.NotNull(oldest);
            Assert.True(oldest > 1, $"Expected pruning to have occurred, oldest={oldest}");

            await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                () => store.GetAccountAtBlockAsync(Addr1, 0));

            await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                () => store.GetAccountAtBlockAsync(Addr1, oldest.Value - 1));
        }

        [Fact]
        public async Task WithinRetentionWindow_StillWorks()
        {
            var diffStore = new InMemoryStateDiffStore();
            var inner = new InMemoryStateStore();
            var options = new HistoricalStateOptions
            {
                MaxHistoryBlocks = 5,
                EnablePruning = true,
                PruningIntervalBlocks = 1
            };
            var store = new HistoricalStateStore(inner, diffStore, options);

            await store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            for (int i = 1; i <= 10; i++)
            {
                store.SetCurrentBlockNumber(i);
                await store.SaveAccountAsync(Addr1, new Account { Balance = 100 + i * 10 });
                await store.ClearCurrentBlockNumberAsync();
            }

            var oldest = await diffStore.GetOldestDiffBlockAsync();
            Assert.NotNull(oldest);

            var atOldest = await store.GetAccountAtBlockAsync(Addr1, oldest.Value);
            Assert.NotNull(atOldest);

            var newest = await diffStore.GetNewestDiffBlockAsync();
            var atNewest = await store.GetAccountAtBlockAsync(Addr1, newest.Value);
            Assert.NotNull(atNewest);
            Assert.Equal(100 + newest.Value * 10, (BigInteger)atNewest.Balance);
        }

        [Fact]
        public async Task FullArchiveMode_NeverThrows()
        {
            var diffStore = new InMemoryStateDiffStore();
            var inner = new InMemoryStateStore();
            var store = new HistoricalStateStore(inner, diffStore, HistoricalStateOptions.FullArchive);

            await store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            for (int i = 1; i <= 20; i++)
            {
                store.SetCurrentBlockNumber(i);
                await store.SaveAccountAsync(Addr1, new Account { Balance = 100 + i * 10 });
                await store.ClearCurrentBlockNumberAsync();
            }

            var atBlock0 = await store.GetAccountAtBlockAsync(Addr1, 0);
            Assert.Equal(100, atBlock0.Balance);

            var atBlock1 = await store.GetAccountAtBlockAsync(Addr1, 1);
            Assert.Equal(110, atBlock1.Balance);

            var atBlock10 = await store.GetAccountAtBlockAsync(Addr1, 10);
            Assert.Equal(200, atBlock10.Balance);

            var atBlock19 = await store.GetAccountAtBlockAsync(Addr1, 19);
            Assert.Equal(290, atBlock19.Balance);
        }

        [Fact]
        public async Task PrunedBlock_StorageQuery_Throws()
        {
            var diffStore = new InMemoryStateDiffStore();
            var inner = new InMemoryStateStore();
            var options = new HistoricalStateOptions
            {
                MaxHistoryBlocks = 3,
                EnablePruning = true,
                PruningIntervalBlocks = 1
            };
            var store = new HistoricalStateStore(inner, diffStore, options);

            await store.SaveStorageAsync(Addr1, 42, new byte[] { 0x01 });

            for (int i = 1; i <= 10; i++)
            {
                store.SetCurrentBlockNumber(i);
                await store.SaveStorageAsync(Addr1, 42, new byte[] { (byte)(i + 1) });
                await store.ClearCurrentBlockNumberAsync();
            }

            await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                () => store.GetStorageAtBlockAsync(Addr1, 42, 0));
        }

        [Fact]
        public async Task PruningBoundary_ExactOldestBlock_Works()
        {
            var diffStore = new InMemoryStateDiffStore();
            var inner = new InMemoryStateStore();
            var options = new HistoricalStateOptions
            {
                MaxHistoryBlocks = 3,
                EnablePruning = true,
                PruningIntervalBlocks = 1
            };
            var store = new HistoricalStateStore(inner, diffStore, options);

            await store.SaveAccountAsync(Addr1, new Account { Balance = 100 });

            for (int i = 1; i <= 8; i++)
            {
                store.SetCurrentBlockNumber(i);
                await store.SaveAccountAsync(Addr1, new Account { Balance = 100 + i * 10 });
                await store.ClearCurrentBlockNumberAsync();
            }

            var oldest = await diffStore.GetOldestDiffBlockAsync();
            Assert.NotNull(oldest);

            var atOldest = await store.GetAccountAtBlockAsync(Addr1, oldest.Value);
            Assert.NotNull(atOldest);

            if (oldest.Value > 0)
            {
                await Assert.ThrowsAsync<HistoricalStateNotAvailableException>(
                    () => store.GetAccountAtBlockAsync(Addr1, oldest.Value - 1));
            }
        }
    }
}
