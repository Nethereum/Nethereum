using System;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class ExecutionStackFactoryTests
    {
        private static readonly ChainConfig Config = new ChainConfig { ChainId = 1337, BlockGasLimit = 30_000_000, BaseFee = 0 };

        private static ExecutionStack Build(InMemoryChainStoreBundle bundle, ExecutionStackOptions options) =>
            ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(HardforkName.Prague),
                _ => Config, _ => Config.GetHardforkConfig(), options);

        [Fact]
        public void Given_NoSideChainOptions_When_Built_Then_TheImporterHasNoSideChain()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var stack = Build(bundle, new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });

            Assert.Null(stack.SideChain);
            Assert.NotNull(stack.Importer);
            Assert.NotNull(stack.Executor);
            Assert.NotNull(stack.Calculator);
        }

        [Fact]
        public void Given_SideChainAdoptByDifficulty_When_Built_Then_TheSideChainCarriesAForkChoiceAndTheBundleMetadata()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var stack = Build(bundle, new ExecutionStackOptions
            {
                RewardPolicy = NoRewardPolicy.Instance,
                SideChain = new SideChainOptions { AdoptByDifficulty = true }
            });

            Assert.NotNull(stack.SideChain!.ForkChoice);
            Assert.Same(bundle.Metadata, stack.SideChain.Metadata);
            Assert.True(stack.SideChain.AcknowledgeKnownCanonicalBlocks);
        }

        [Fact]
        public void Given_SideChainWithoutMetadataAndWithoutAcknowledge_When_Built_Then_TheSideChainReflectsIt()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var stack = Build(bundle, new ExecutionStackOptions
            {
                RewardPolicy = NoRewardPolicy.Instance,
                SideChain = new SideChainOptions { PersistMetadata = false, AcknowledgeKnownCanonicalBlocks = false }
            });

            Assert.Null(stack.SideChain!.ForkChoice);
            Assert.Null(stack.SideChain.Metadata);
            Assert.False(stack.SideChain.AcknowledgeKnownCanonicalBlocks);
        }

        [Fact]
        public void Given_AWindowedCadenceOverANonHistoricalStore_When_Built_Then_TheImporterRefusesIt()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var ex = Assert.Throws<ArgumentException>(() => Build(bundle, new ExecutionStackOptions
            {
                RewardPolicy = NoRewardPolicy.Instance,
                FlushCadence = new FixedIntervalFlushCadence(4)
            }));

            Assert.Contains("IHistoricalStateProvider", ex.Message);
        }

        [Fact]
        public void Given_NoRewardPolicy_When_Built_Then_ItIsRefused()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            Assert.Throws<ArgumentException>(() => Build(bundle, new ExecutionStackOptions()));
        }

        [Fact]
        public void Given_AnExistingExecutor_When_AnImporterIsCreated_Then_ItUsesTheRoleOptions()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var built = Build(bundle, new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });

            var ex = Assert.Throws<ArgumentException>(() => ExecutionStackFactory.CreateImporter(
                bundle, built.Executor, new ExecutionStackOptions { FlushCadence = new FixedIntervalFlushCadence(4) }));

            Assert.Contains("IHistoricalStateProvider", ex.Message);
        }

        [Fact]
        public void Given_ABuiltStack_When_ANewImporterIsRequested_Then_ItIsADistinctImporterOverTheSameExecutor()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var stack = Build(bundle, new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });

            Assert.NotSame(stack.Importer, stack.NewImporter());
        }

        [Theory]
        [InlineData(1)]
        public void Given_OneBlock_When_ACadenceIsRequested_Then_NoCadenceMeansEveryBlock(int blocks)
        {
            Assert.Null(FlushCadences.ForBlocks(blocks));
        }

        [Fact]
        public void Given_MoreThanOneBlock_When_ACadenceIsRequested_Then_ItIsAFixedInterval()
        {
            var cadence = Assert.IsType<FixedIntervalFlushCadence>(FlushCadences.ForBlocks(8));

            Assert.Equal(8UL, cadence.K);
        }

        [Fact]
        public void Given_ZeroBlocks_When_ACadenceIsRequested_Then_ItIsRefused()
        {
            Assert.Throws<ArgumentException>(() => FlushCadences.ForBlocks(0));
        }

        [Fact]
        public void Given_TheFollowerFactory_When_Built_Then_TheImporterIsTheSharedBuildersWithNoSideChain()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var stack = FollowerExecutorStackFactory.BuildFollowerExecutorStackWithCalculator(
                bundle, new FixedChainActivations(HardforkName.Prague),
                _ => Config, _ => Config.GetHardforkConfig(), NoRewardPolicy.Instance);

            var importer = Assert.IsType<BlockImporter>(stack.Executor);
            Assert.Null(importer.SideChain);
            Assert.Same(bundle.Blocks, importer.Blocks);
        }

        [Fact]
        public void Given_AStateRootCalculatorFactory_When_AnExecutorCoreIsBuilt_Then_TheFactorysCalculatorIsTheOneUsed()
        {
            var state = new InMemoryStateStore();
            var supplied = new IncrementalStateRootCalculator(state);

            var core = ExecutionStackFactory.BuildExecutor(
                state, new InMemoryBlockStore(), new InMemoryContentNodeStore(), new InMemoryContentNodeStore(), null,
                new FixedChainActivations(HardforkName.Prague), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions
                {
                    RewardPolicy = NoRewardPolicy.Instance,
                    StateRootCalculatorFactory = (_, _) => supplied
                });

            Assert.Same(supplied, core.Calculator);
        }

        [Fact]
        public void Given_AnExecutorCoreIsBuilt_When_Built_Then_TheSecp256k1ContextsAreInitialisedBeforeTheFirstBlock()
        {
            var state = new InMemoryStateStore();

            ExecutionStackFactory.BuildExecutor(
                state, new InMemoryBlockStore(), new InMemoryContentNodeStore(), new InMemoryContentNodeStore(), null,
                new FixedChainActivations(HardforkName.Prague), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });

            Assert.True(Nethereum.Signer.Secp256k1Contexts.IsInitialised);
        }

        [Fact]
        public void Given_NoStateRootCalculatorFactory_When_AnExecutorCoreIsBuilt_Then_ThePatriciaIncrementalCalculatorIsUsed()
        {
            var state = new InMemoryStateStore();

            var core = ExecutionStackFactory.BuildExecutor(
                state, new InMemoryBlockStore(), new InMemoryContentNodeStore(), new InMemoryContentNodeStore(), null,
                new FixedChainActivations(HardforkName.Prague), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance });

            Assert.IsType<IncrementalStateRootCalculator>(core.Calculator);
        }

        [Fact]
        public void Given_ChainConfigsOfEachStateTree_When_TheirCalculatorIsCreated_Then_BinaryGetsTheBinaryCalculatorAndPatriciaTheIncrementalOne()
        {
            var state = new InMemoryStateStore();

            Assert.IsType<IncrementalStateRootCalculator>(
                new ChainConfig { StateTree = StateTreeType.Patricia }.CreateIncrementalStateRootCalculatorForStateTree(state));
            Assert.IsType<BinaryIncrementalStateRootCalculator>(
                new ChainConfig { StateTree = StateTreeType.Binary }.CreateIncrementalStateRootCalculatorForStateTree(state));
        }
    }
}
