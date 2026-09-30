using System;
using Nethereum.CoreChain;
using Nethereum.MainnetChain.Configuration;
using Nethereum.MainnetChain.Hosting;
using Xunit;

namespace Nethereum.MainnetChain.Server.IntegrationTests
{
    public class FlushCadenceCompositionTests
    {
        [Fact]
        public void Default_Config_Maps_To_Null_Cadence_KEquals1()
        {
            var config = new MainnetChainServerConfig { DataDir = "./x" };

            var cadence = MainnetNodeComposition.BuildFlushCadence(config);

            Assert.Null(cadence);
        }

        [Fact]
        public void FlushCadenceBlocks_24_Maps_To_FixedIntervalFlushCadence_KEquals24()
        {
            var config = new MainnetChainServerConfig { DataDir = "./x", FlushCadenceBlocks = 24 };

            var cadence = MainnetNodeComposition.BuildFlushCadence(config);

            var fixedCadence = Assert.IsType<FixedIntervalFlushCadence>(cadence);
            Assert.Equal(24UL, fixedCadence.K);
        }

        [Fact]
        public void FlushCadenceBlocks_0_Throws()
        {
            var config = new MainnetChainServerConfig { DataDir = "./x", FlushCadenceBlocks = 0 };

            Assert.Throws<ArgumentException>(() => MainnetNodeComposition.BuildFlushCadence(config));
        }

        [Fact]
        public void FlushCadenceBlocks_Negative_Throws()
        {
            var config = new MainnetChainServerConfig { DataDir = "./x", FlushCadenceBlocks = -1 };

            Assert.Throws<ArgumentException>(() => MainnetNodeComposition.BuildFlushCadence(config));
        }
    }
}
