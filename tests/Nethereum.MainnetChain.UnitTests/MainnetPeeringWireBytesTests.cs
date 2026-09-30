using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ChainNode.Hosting;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.EVM.ForkId;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.MainnetChain.Hosting;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class MainnetPeeringWireBytesTests
    {
        private static async Task<Eth68StatusMessage> OldHandRolledStatusTemplateAsync(
            Nethereum.CoreChain.Storage.IChainStoreBundle bundle)
        {
            var lastHash = bundle.Metadata.GetLastBlockHash();
            var bestHash = lastHash ?? MainnetGenesisConstants.BlockHashHex.HexToByteArray();
            var genesisHash = MainnetGenesisConstants.BlockHashHex.HexToByteArray();

            var head = await bundle.Blocks.GetLatestAsync();
            var (headBlock, headTime) = head == null
                ? (0UL, MainnetGenesisConstants.Timestamp)
                : ((ulong)head.BlockNumber, (ulong)head.Timestamp);

            var forkId = Eip2124ForkIdCalculator.NewId(
                genesisHash, MainnetChainSchedule.ForkIdentity.BlockHeights, MainnetChainSchedule.ForkIdentity.Timestamps,
                headBlock, headTime);

            return new Eth68StatusMessage
            {
                ProtocolVersion = 68,
                NetworkId = MainnetGenesisConstants.ChainId,
                TotalDifficulty = BigInteger.Zero,
                BestHash = bestHash,
                GenesisHash = genesisHash,
                ForkHash = forkId.Hash,
                ForkNext = forkId.Next,
            };
        }

        [Fact]
        public async Task Given_AFreshMainnetBundleWithNoSyncedHead_When_TheOldAndNewStatusTemplatesAreBothBuilt_Then_TheirEncodedWireBytesAreIdentical()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var oldMessage = await OldHandRolledStatusTemplateAsync(bundle);

            var profile = new MainnetChainProfile(localKey: null, bundle);
            var newMessage = await ChainNodeServeListener.BuildStatusTemplateAsync(profile, bundle);

            var oldBytes = Eth68StatusMessageEncoder.Encode(oldMessage);
            var newBytes = Eth68StatusMessageEncoder.Encode(newMessage);

            Assert.Equal(oldBytes, newBytes);
        }

        [Fact]
        public async Task Given_AMainnetBundleWithASyncedHead_When_TheOldAndNewStatusTemplatesAreBothBuilt_Then_TheirEncodedWireBytesAreIdentical()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var definition = new MainnetChainDefinition();
            await definition.EnsureGenesisAsync(bundle, CancellationToken.None);

            var headHash = new byte[32];
            headHash[0] = 0xAB;
            await bundle.Blocks.SaveAsync(
                new BlockHeader { BlockNumber = 19_000_000, Timestamp = 1_700_000_000 },
                headHash);
            bundle.Metadata.Commit(19_000_000, headHash);

            var oldMessage = await OldHandRolledStatusTemplateAsync(bundle);

            var profile = new MainnetChainProfile(localKey: null, bundle);
            var newMessage = await ChainNodeServeListener.BuildStatusTemplateAsync(profile, bundle);

            var oldBytes = Eth68StatusMessageEncoder.Encode(oldMessage);
            var newBytes = Eth68StatusMessageEncoder.Encode(newMessage);

            Assert.Equal(oldBytes, newBytes);
        }

        [Fact]
        public async Task Given_TheNewSharedStatusTemplate_When_ItIsBuiltForMainnet_Then_ItReportsMainnetsNetworkIdAndGenesisHashUnconditionally()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var profile = new MainnetChainProfile(localKey: null, bundle);

            var message = await ChainNodeServeListener.BuildStatusTemplateAsync(profile, bundle);

            Assert.Equal((ulong)MainnetGenesisConstants.ChainId, message.NetworkId);
            Assert.Equal(
                MainnetGenesisConstants.BlockHashHex.HexToByteArray(),
                message.GenesisHash);
            Assert.Equal(68, message.ProtocolVersion);
            Assert.Equal(BigInteger.Zero, message.TotalDifficulty);
        }
    }
}
