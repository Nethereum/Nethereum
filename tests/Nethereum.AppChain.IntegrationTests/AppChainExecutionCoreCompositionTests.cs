using System;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.AppChain.Server.Configuration;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainExecutionCoreCompositionTests
    {
        private static AppChainExecutionCore Compose(InMemoryChainStoreBundle bundle, IFlushCadence? cadence = null) =>
            new AppChainExecutionCore(
                AppChainConfig.CreateWithName("core-composition", 1337),
                bundle, null, NullLoggerFactory.Instance, cadence);

        [Fact]
        public void Given_AnAppChainCore_When_Composed_Then_ItsSideChainIsTheAppChainRoleOptions()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var core = Compose(bundle);

            Assert.False(core.SideChain.AcknowledgeKnownCanonicalBlocks);
            Assert.Null(core.SideChain.Metadata);
            Assert.Null(core.SideChain.ForkChoice);
            Assert.NotNull(core.StateRoot);
            Assert.NotNull(core.Executor);
        }

        [Fact]
        public void Given_AWindowedCadence_When_TheCoreIsComposed_Then_TheSharedImporterRulesRefuseItOverANonHistoricalStore()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var ex = Assert.Throws<ArgumentException>(() => Compose(bundle, new FixedIntervalFlushCadence(4)));

            Assert.Contains("IHistoricalStateProvider", ex.Message);
        }

        [Fact]
        public void Given_NoConfiguredCadence_When_TheServerConfigIsRead_Then_TheCadenceIsEveryBlock()
        {
            Assert.Null(AppChainExecutionCore.FlushCadenceFor(new AppChainServerConfig()));
        }

        [Fact]
        public void Given_AConfiguredCadenceOfEight_When_TheServerConfigIsRead_Then_TheCadenceIsAFixedIntervalOfEight()
        {
            var config = new AppChainServerConfig();
            config.Node.Storage.FlushCadenceBlocks = 8;

            var cadence = Assert.IsType<FixedIntervalFlushCadence>(AppChainExecutionCore.FlushCadenceFor(config));

            Assert.Equal(8UL, cadence.K);
        }

        [Fact]
        public void Given_AnAppChainCore_When_ImportersAreCreated_Then_EachIsFreshOverTheSharedExecutor()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var core = Compose(bundle);

            Assert.NotSame(core.CreateImporter(), core.CreateImporter());
        }
    }
}
