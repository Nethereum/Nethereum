using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Nethereum.EVM.ForkId;

namespace Nethereum.DevP2P.SpecTests.Eth
{
    public class Eip2124ForkIdCalculatorTests
    {
        private static readonly byte[] MainnetGenesis =
            "d4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3".HexToByteArray();

        private static readonly ulong[] MainnetForkBlocks = new ulong[]
        {
            1_150_000UL,
            1_920_000UL,
            2_463_000UL,
            2_675_000UL,
            4_370_000UL,
            7_280_000UL,
            9_069_000UL,
            9_200_000UL,
            12_244_000UL,
            12_965_000UL,
            13_773_000UL,
            15_050_000UL
        };

        private static readonly ulong[] MainnetForkTimestamps = new ulong[]
        {
            1_681_338_455UL,
            1_710_338_135UL
        };

        [Fact]
        public void Mainnet_GenesisOnly_MatchesFcOnly_ForFrontierEra()
        {
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(MainnetGenesis, new ulong[0], new ulong[0]);
            Assert.Equal(0xfc64ec04u, forkHash);
        }

        [Fact]
        public void Mainnet_AfterHomestead_MatchesGoEthereumReference()
        {
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL },
                new ulong[0]);
            Assert.Equal(0x97c2c34cu, forkHash);
        }

        [Fact]
        public void Mainnet_AfterDao_MatchesGoEthereumReference()
        {
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 1_920_000UL },
                new ulong[0]);
            Assert.Equal(0x91d1f948u, forkHash);
        }

        [Fact]
        public void Mainnet_AfterPetersburg_MatchesGoEthereumReference()
        {
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 1_920_000UL, 2_463_000UL, 2_675_000UL, 4_370_000UL, 7_280_000UL, 7_280_000UL },
                new ulong[0]);
            Assert.Equal(0x668db0afu, forkHash);
        }

        [Fact]
        public void Mainnet_AfterShanghai_MatchesGoEthereumReference()
        {
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                MainnetForkBlocks,
                new ulong[] { 1_681_338_455UL });
            Assert.Equal(0xdce96c2du, forkHash);
        }

        [Fact]
        public void Mainnet_AfterCancun_MatchesGoEthereumReference()
        {
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                MainnetForkBlocks,
                MainnetForkTimestamps);
            Assert.Equal(0x9f3d2254u, forkHash);
        }

        [Fact]
        public void DuplicateForkBlocks_AreDropped()
        {
            var withDups = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 7_280_000UL, 7_280_000UL },
                new ulong[0]);
            var withoutDups = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 7_280_000UL },
                new ulong[0]);
            Assert.Equal(withoutDups, withDups);
        }

        [Fact]
        public void ZeroForkValues_AreSkipped()
        {
            var withZero = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 0UL, 1_150_000UL },
                new ulong[] { 0UL, 1_681_338_455UL });
            var withoutZero = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL },
                new ulong[] { 1_681_338_455UL });
            Assert.Equal(withoutZero, withZero);
        }
    }
}
