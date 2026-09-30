using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockImporterConstructorGuardTests
    {
        private static BlockExecutor BuildEngine(IStateStore stateStore, IBlockStore blockStore, ITrieNodeStore trieNodeStore)
        {
            var config = new ChainConfig { ChainId = 1337, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var calculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);
            return new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(Nethereum.EVM.HardforkName.Prague),
                chainConfigFactory: _ => config,
                hardforkConfigFactory: _ => config.GetHardforkConfig(),
                stateRootCalculator: calculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore);
        }

        [Fact]
        public void KGreaterThanOne_NonHistoricalStateStore_Throws()
        {
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = BuildEngine(stateStore, blockStore, trieNodeStore);

            var ex = Assert.Throws<System.ArgumentException>(() => new BlockImporter(
                engine, blockStore, stateStore,
                flushCadence: new FixedIntervalFlushCadence(2)));

            Assert.Contains("IHistoricalStateProvider", ex.Message);
        }

        [Fact]
        public void KGreaterThanOne_HistoricalStateStore_DoesNotThrow()
        {
            var innerStore = new InMemoryStateStore();
            var stateStore = new HistoricalStateStore(innerStore);
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = BuildEngine(stateStore, blockStore, trieNodeStore);

            var importer = new BlockImporter(
                engine, blockStore, stateStore,
                flushCadence: new FixedIntervalFlushCadence(2));

            Assert.NotNull(importer);
        }

        [Fact]
        public void KEqualsOne_NonHistoricalStateStore_DoesNotThrow_UnchangedFromToday()
        {
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = BuildEngine(stateStore, blockStore, trieNodeStore);

            var importerDefault = new BlockImporter(engine, blockStore, stateStore);
            Assert.NotNull(importerDefault);

            var importerExplicitK1 = new BlockImporter(
                engine, blockStore, stateStore,
                flushCadence: new FixedIntervalFlushCadence(1));
            Assert.NotNull(importerExplicitK1);
        }

        private sealed class CustomWindowCadence : IFlushCadence
        {
            public bool IsBoundary(ulong blockNumber) => blockNumber % 3 == 0;
        }

        [Fact]
        public void CustomNonEveryBlockCadence_NonHistoricalStateStore_Throws()
        {
            var stateStore = new InMemoryStateStore();
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = BuildEngine(stateStore, blockStore, trieNodeStore);

            var ex = Assert.Throws<System.ArgumentException>(() => new BlockImporter(
                engine, blockStore, stateStore,
                flushCadence: new CustomWindowCadence()));

            Assert.Contains("IHistoricalStateProvider", ex.Message);
        }

        [Fact]
        public void CustomNonEveryBlockCadence_HistoricalStateStore_DoesNotThrow()
        {
            var innerStore = new InMemoryStateStore();
            var stateStore = new HistoricalStateStore(innerStore);
            var blockStore = new InMemoryBlockStore();
            var trieNodeStore = new InMemoryContentNodeStore();
            var engine = BuildEngine(stateStore, blockStore, trieNodeStore);

            var importer = new BlockImporter(
                engine, blockStore, stateStore,
                flushCadence: new CustomWindowCadence());

            Assert.NotNull(importer);
        }
    }
}
