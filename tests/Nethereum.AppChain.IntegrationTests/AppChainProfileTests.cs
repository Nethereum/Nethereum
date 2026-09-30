using Nethereum.ChainNode.Hosting;
using System;
using System.Linq;
using Nethereum.AppChain.Server.Hosting;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainProfileTests
    {
        private static readonly byte[] Genesis = Enumerable.Repeat((byte)0xAB, 32).ToArray();

        private static DefaultChainProfile Build(
            ulong networkId = 420420, string[] peers = null, ChainForkSchedule schedule = null) =>
            new DefaultChainProfile(
                networkId,
                Genesis,
                schedule ?? ChainForkSchedule.Running(420420, HardforkName.Amsterdam),
                peers ?? new[] { "enode://aa@127.0.0.1:30303" });

        [Fact]
        public void Given_AnAppChainProfile_When_ItsIdentityIsRead_Then_ItIsTheConfiguredChainIdAndGenesis()
        {
            var profile = Build(networkId: 420420);

            Assert.Equal(420420UL, profile.NetworkId);
            Assert.True(ByteUtil.AreEqual(Genesis, profile.GenesisHash));
        }

        [Fact]
        public void Given_AnAppChainRunningOneFork_When_ItsForkThresholdsAreRead_Then_TheyAreEmptyBecauseItCrossesNoBoundary()
        {
            var (heights, timestamps) = Build().ForkThresholds;

            Assert.Empty(heights);
            Assert.Empty(timestamps);
        }

        [Fact]
        public void Given_AnAppChainThatSchedulesAFork_When_ItsForkThresholdsAreRead_Then_TheyAreItsOwnScheduleNotEmpty()
        {
            var schedule = new ChainForkSchedule
            {
                ChainId = 420420,
                GenesisFork = HardforkName.Osaka.ToString(),
                Schedule =
                {
                    new ForkActivationEntry
                    {
                        Fork = HardforkName.Amsterdam.ToString(), Timestamp = 1_800_000_000UL
                    }
                }
            };

            var (heights, timestamps) = Build(schedule: schedule).ForkThresholds;

            Assert.Empty(heights);
            Assert.Equal(new ulong[] { 1_800_000_000UL }, timestamps);
        }

        [Fact]
        public void Given_AProfileWithNoForkSchedule_When_ItIsConstructed_Then_ItRefusesRatherThanAssumingNoForks()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DefaultChainProfile(1, Genesis, null, Array.Empty<string>()));
        }

        [Fact]
        public void Given_AnAppChain_When_ItsPeersAreRead_Then_TheyAreTheConfiguredEnodesNotDiscoveredOnes()
        {
            var profile = Build(peers: new[] { "enode://aa@127.0.0.1:1", "enode://bb@127.0.0.1:2" });

            Assert.Equal(2, profile.Bootnodes.Count);
        }

        [Fact]
        public void Given_AProfileWithAMalformedGenesisHash_When_ItIsConstructed_Then_ItRefuses()
        {
            Assert.Throws<ArgumentException>(() =>
                new DefaultChainProfile(1, new byte[16], ChainForkSchedule.Running(1, HardforkName.Amsterdam), Array.Empty<string>()));
        }

        [Fact]
        public void Given_AProfileWithNoGenesisHash_When_ItIsConstructed_Then_ItRefusesRatherThanDefaulting()
        {
            Assert.Throws<ArgumentException>(() =>
                new DefaultChainProfile(1, null, ChainForkSchedule.Running(1, HardforkName.Amsterdam), Array.Empty<string>()));
        }

        [Fact]
        public void Given_AnAppChain_When_AHandshakeWorkerIsCreated_Then_ItValidatesChainIdentityNotForkId()
        {
            Assert.IsType<ChainPeerHandshakeWorker>(Build().CreateHandshakeWorker(null, advertiseSnap2: false));
        }
    }
}
