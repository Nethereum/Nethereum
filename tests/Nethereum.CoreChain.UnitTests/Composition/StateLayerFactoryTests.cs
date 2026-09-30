using System;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Composition
{
    public class StateLayerFactoryTests
    {
        private static T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (T)field.GetValue(instance);
        }

        [Fact]
        public void StateLayer_InMemory_builds_HistoricalOverInMemory()
        {
            var stateLayer = new StateLayer();

            var store = stateLayer.Stores.HistoricalInMemory();

            var historical = Assert.IsType<HistoricalStateStore>(store);
            var inner = GetPrivateField<IStateStore>(historical, "_inner");
            Assert.IsType<InMemoryStateStore>(inner);
        }

        [Fact]
        public async Task HistoricalInMemory_WithJournal_wraps_in_Historical()
        {
            var stateLayer = new StateLayer();
            var rawState = new InMemoryStateStore();
            var diffStore = new InMemoryStateDiffStore();
            const string address = "0x1111111111111111111111111111111111111111";

            var store = stateLayer.Stores.HistoricalInMemory(rawState, diffStore, HistoricalStateOptions.Default);

            var historical = Assert.IsType<HistoricalStateStore>(store);
            Assert.Same(rawState, GetPrivateField<IStateStore>(historical, "_inner"));

            await historical.SaveAccountAsync(address, new Account { Balance = 100 });
            historical.SetCurrentBlockNumber(1);
            await historical.SaveAccountAsync(address, new Account { Balance = 200 });
            await historical.ClearCurrentBlockNumberAsync();

            var atBlock0 = await historical.GetAccountAtBlockAsync(address, 0);
            Assert.Equal(100, atBlock0.Balance);

            var current = await historical.GetAccountAsync(address);
            Assert.Equal(200, current.Balance);
        }

        [Fact]
        public void HistoricalInMemory_NullJournal_returns_bare_rawState()
        {
            var stateLayer = new StateLayer();
            var rawState = new InMemoryStateStore();
            var diffStore = new InMemoryStateDiffStore();

            var store = stateLayer.Stores.HistoricalInMemory(rawState, diffStore, null);

            Assert.Same(rawState, store);
        }

        [Fact]
        public void StateLayer_SnapSync_builds_TrieFallbackOverGivenStores()
        {
            var stateLayer = new StateLayer();
            var rawFlat = new InMemoryStateStore();
            var nodeBlobStore = new InMemoryContentNodeStore();
            Func<byte[]> rootFn = () => new byte[] { 1, 2, 3 };

            var store = stateLayer.Stores.SnapSync(rawFlat, nodeBlobStore, rootFn);

            var fallback = Assert.IsType<TrieFallbackStateStore>(store);
            Assert.Same(rawFlat, GetPrivateField<IStateStore>(fallback, "_inner"));
            Assert.Same(nodeBlobStore, GetPrivateField<INodeBlobStore>(fallback, "_trieStorage"));
            Assert.Same(rootFn, GetPrivateField<Func<byte[]>>(fallback, "_stateRootProvider"));
        }

        [Fact]
        public void StateLayer_WitnessCapture_withNoHistorical_wrapsGivenStore()
        {
            var stateLayer = new StateLayer();
            var liveStore = new InMemoryStateStore();

            var store = stateLayer.Stores.WitnessCapture(liveStore);

            var readOnly = Assert.IsType<ReadOnlyStateStoreWrapper>(store);
            Assert.Same(liveStore, GetPrivateField<IStateStore>(readOnly, "_inner"));
        }

        [Fact]
        public void StateLayer_WitnessCapture_withHistoricalAdapter_wrapsAdapterNotLiveStore()
        {
            var stateLayer = new StateLayer();
            var liveStore = new InMemoryStateStore();
            var historicalAdapter = new InMemoryStateStore();

            var store = stateLayer.Stores.WitnessCapture(liveStore, historicalAdapter);

            var readOnly = Assert.IsType<ReadOnlyStateStoreWrapper>(store);
            Assert.Same(historicalAdapter, GetPrivateField<IStateStore>(readOnly, "_inner"));
        }
    }
}
