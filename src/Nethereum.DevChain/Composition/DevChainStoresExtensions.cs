using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevChain.Storage.Sqlite;

namespace Nethereum.DevChain.Composition
{
    public static class DevChainStoresExtensions
    {
        public static IStateStore DevChainInMemory(this StoresFactory stores)
            => new HistoricalStateStore(new InMemoryStateStore(), new InMemoryStateDiffStore(), HistoricalStateOptions.DevChainDefault);

        public static IStateStore DevChainSqlite(this StoresFactory stores, SqliteStorageManager manager)
            => new HistoricalStateStore(new SqliteStateStore(manager), new SqliteStateDiffStore(manager), HistoricalStateOptions.DevChainDefault);
    }
}
