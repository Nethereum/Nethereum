using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Composition;
using Nethereum.DevChain.Storage.Sqlite;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class DevChainStoresConfigTests
    {
        [Fact]
        public void DevChainInMemory_ReturnsHistoricalStateStoreOverInMemory_WithDevChainDefaultOptions()
        {
            var store = new StateLayer().Stores.DevChainInMemory();

            var historical = Assert.IsType<HistoricalStateStore>(store);
            Assert.IsType<InMemoryStateStore>(historical.Inner);

            var options = Assert.IsType<HistoricalStateOptions>(historical.Options);
            Assert.Equal(HistoricalStateOptions.DevChainDefault.MaxHistoryBlocks, options.MaxHistoryBlocks);
            Assert.Equal(HistoricalStateOptions.DevChainDefault.PruningIntervalBlocks, options.PruningIntervalBlocks);
            Assert.Equal(HistoricalStateOptions.DevChainDefault.EnablePruning, options.EnablePruning);
            Assert.NotEqual(HistoricalStateOptions.Default.MaxHistoryBlocks, options.MaxHistoryBlocks);
        }

        [Fact]
        public async Task DevChainSqlite_ReturnsHistoricalStateStoreOverSqlite_WithDevChainDefaultOptions()
        {
            using var manager = new SqliteStorageManager(null, deleteOnDispose: true);

            var store = new StateLayer().Stores.DevChainSqlite(manager);

            var historical = Assert.IsType<HistoricalStateStore>(store);
            Assert.IsType<SqliteStateStore>(historical.Inner);

            var options = Assert.IsType<HistoricalStateOptions>(historical.Options);
            Assert.Equal(HistoricalStateOptions.DevChainDefault.MaxHistoryBlocks, options.MaxHistoryBlocks);
            Assert.Equal(HistoricalStateOptions.DevChainDefault.PruningIntervalBlocks, options.PruningIntervalBlocks);

            await store.SaveAccountAsync("0x1234567890123456789012345678901234567890", new Account { Balance = 42, Nonce = 1 });
            var account = await store.GetAccountAsync("0x1234567890123456789012345678901234567890");
            Assert.Equal((EvmUInt256)42, account.Balance);
        }
    }
}
