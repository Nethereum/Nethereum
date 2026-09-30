using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;

namespace Nethereum.CoreChain.RocksDB.Composition
{
    public static class RocksDbStoresExtensions
    {
        public static IStateStore MainnetFollower(
            this StoresFactory stores,
            RocksDbStateStore rawFlatState,
            IStateDiffStore diffStore,
            HistoricalStateOptions journalOptions,
            FlatStateCache flatCache = null,
            IPendingFlushFlatOverlay pendingFlush = null)
            => journalOptions != null
                ? new HistoricalStateStore(new BufferedFlatStateStore(rawFlatState, flatCache, pendingFlush), diffStore, journalOptions)
                : rawFlatState;
    }
}
