using System;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetChainProfileAndDefinitionTests
    {
        [Fact]
        public async Task Given_TheMainnetProfile_When_ItIsAsked_Then_ItReportsMainnetsGenesisHashNetworkIdAndForkThresholds()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var definition = new MainnetChainDefinition();
            await definition.EnsureGenesisAsync(bundle, CancellationToken.None);

            var profile = await definition.CreateProfileAsync(bundle);

            Assert.Equal((ulong)MainnetGenesisConstants.ChainId, profile.NetworkId);
            Assert.True(ByteUtil.AreEqual(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(), profile.GenesisHash));
            Assert.Same(MainnetChainSchedule.ForkIdentity.BlockHeights, profile.ForkThresholds.BlockHeights);
            Assert.Same(MainnetChainSchedule.ForkIdentity.Timestamps, profile.ForkThresholds.Timestamps);
            Assert.Same(SyncPeerSession.MainnetBootnodes, profile.Bootnodes);
        }

        [Fact]
        public async Task Given_TheMainnetProfile_When_ItBuildsAHandshakeWorker_Then_ThatWorkerAdvertisesMainnetsLiveHeadForkId()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var syntheticHeadHash = new byte[32];
            syntheticHeadHash[0] = 1;
            await bundle.Blocks.SaveAsync(
                new BlockHeader { BlockNumber = 1, Timestamp = 999_999 },
                syntheticHeadHash);
            var profile = new MainnetChainProfile(localKey: null, bundle);

            var worker = profile.CreateHandshakeWorker(loggerFactory: null, advertiseSnap2: false);

            Assert.IsType<MainnetPeerHandshakeWorker>(worker);

            var ourHeadProvider = (Func<Task<(ulong HeadBlock, ulong HeadTime)>>)typeof(MainnetPeerHandshakeWorker)
                .GetField("_ourHeadProvider", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(worker)!;

            var expected = await ChainHeadResolver.ResolveOurHeadAsync(bundle);
            var actual = await ourHeadProvider();

            Assert.Equal(1UL, expected.HeadBlock);
            Assert.Equal(999_999UL, expected.HeadTime);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task Given_TheMainnetDefinition_When_ItEnsuresGenesis_Then_TheBundleHoldsMainnetsGenesisBlock()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var definition = new MainnetChainDefinition();

            await definition.EnsureGenesisAsync(bundle, CancellationToken.None);

            var genesisBlock = await bundle.Blocks.GetByNumberAsync(BigInteger.Zero);
            Assert.NotNull(genesisBlock);
            var genesisHash = await bundle.Blocks.GetHashByNumberAsync(BigInteger.Zero);
            Assert.True(ByteUtil.AreEqual(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(), genesisHash));
            Assert.True(bundle.Metadata.IsGenesisLoaded());
        }

        [Fact]
        public void Given_TheMainnetProfilesForkThresholds_When_Asked_Then_TheyAreTheChainSchedulesOwnDerivation()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var profile = new MainnetChainProfile(localKey: null, bundle);

            var (blockHeights, timestamps) = profile.ForkThresholds;

            Assert.Same(MainnetChainSchedule.ForkIdentity.BlockHeights, blockHeights);
            Assert.Same(MainnetChainSchedule.ForkIdentity.Timestamps, timestamps);
        }
    }
}
