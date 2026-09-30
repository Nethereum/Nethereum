using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class NodeForkResolutionProductionPathTests
    {
        private sealed class BlockBoundaryActivations : IChainActivations
        {
            private readonly long _boundary;
            private readonly HardforkName _before;
            private readonly HardforkName _after;

            public BlockBoundaryActivations(long boundary, HardforkName before, HardforkName after)
            {
                _boundary = boundary;
                _before = before;
                _after = after;
            }

            public HardforkName ResolveAt(long blockNumber, ulong timestamp)
                => blockNumber >= _boundary ? _after : _before;
        }

        private const long ForkBoundaryBlock = 3;
        private const int DeployedByteCount = 32;
        private const long PreAmsterdamPerByte = 200;
        private const long AmsterdamPerStateByte = 1530;

        private static byte[] InitCodeReturning32Bytes()
            => new byte[] { 0x60, 0x20, 0x60, 0x00, 0xf3 };

        private static async Task MineUpToAsync(DevChainNode node, long height)
        {
            while (await node.GetBlockNumberAsync() < height)
                await node.MineBlockAsync();
        }

        private static async Task SeedPredeploysForScheduledForkAsync(DevChainNode node, ChainConfig config)
        {
            foreach (var predeploy in SystemContractPredeploys.For(config.NewestForkThisChainRuns))
                await node.SetCodeAsync(predeploy.Address, predeploy.RuntimeCode);
        }

        private const string SlotReporter = "0x5107510751075107510751075107510751075107";

        private static byte[] ReturnSlotNumber()
            => new byte[] { 0x4b, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 };

        [Fact]
        [Trait("Category", "ForkResolution")]
        public async Task Given_ScheduledChain_When_HeadCrossesForkBoundary_Then_EthCallFollowsTheNewForkToo()
        {
            var config = DevChainConfig.Default;
            config.Hardfork = "prague";
            config.Activations = new BlockBoundaryActivations(
                ForkBoundaryBlock, HardforkName.Prague, HardforkName.Amsterdam);

            using var node = new DevChainNode(config);
            await node.StartAsync();
            await SeedPredeploysForScheduledForkAsync(node, config);
            await node.SetCodeAsync(SlotReporter, ReturnSlotNumber());

            await MineUpToAsync(node, ForkBoundaryBlock - 2);
            var beforeBoundary = await node.CallAsync(SlotReporter, Array.Empty<byte>());

            await MineUpToAsync(node, ForkBoundaryBlock + 1);
            var afterBoundary = await node.CallAsync(SlotReporter, Array.Empty<byte>());

            Assert.False(beforeBoundary.Success,
                "SLOTNUM is not an opcode at Prague, so a call before the boundary must fail");
            Assert.True(afterBoundary.Success,
                "after the boundary the chain is Amsterdam, so the same call must succeed; " +
                "a failure here means eth_call priced the block at ChainConfig.Hardfork " +
                "instead of following the schedule");
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public async Task Given_ScheduledChain_When_HeadCrossesForkBoundary_Then_EstimateFollowsTheNewFork()
        {
            var config = DevChainConfig.Default;
            config.Hardfork = "prague";
            config.Activations = new BlockBoundaryActivations(
                ForkBoundaryBlock, HardforkName.Prague, HardforkName.Amsterdam);

            using var node = new DevChainNode(config);
            await node.StartAsync();
            await SeedPredeploysForScheduledForkAsync(node, config);

            await MineUpToAsync(node, ForkBoundaryBlock - 2);
            var beforeBoundary = await node.EstimateContractCreationGasAsync(InitCodeReturning32Bytes());
            Assert.True(beforeBoundary.Success, beforeBoundary.RevertReason);

            await MineUpToAsync(node, ForkBoundaryBlock + 1);
            var afterBoundary = await node.EstimateContractCreationGasAsync(InitCodeReturning32Bytes());
            Assert.True(afterBoundary.Success, afterBoundary.RevertReason);

            var minimumIncrease = DeployedByteCount * (AmsterdamPerStateByte - PreAmsterdamPerByte);
            Assert.True(
                afterBoundary.GasUsed - beforeBoundary.GasUsed >= minimumIncrease,
                $"crossing into Amsterdam must reprice the deposit by at least {minimumIncrease} " +
                $"({DeployedByteCount} bytes x ({AmsterdamPerStateByte} - {PreAmsterdamPerByte})); " +
                $"got {beforeBoundary.GasUsed} before the boundary and {afterBoundary.GasUsed} after. " +
                "Equal values mean the node is still pricing at ChainConfig.Hardfork.");
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public async Task Given_UnscheduledChain_When_HeadAdvances_Then_EveryBlockKeepsThePinnedFork()
        {
            var config = DevChainConfig.Default;
            config.Hardfork = "prague";
            Assert.Null(config.Activations);

            using var node = new DevChainNode(config);
            await node.StartAsync();

            await MineUpToAsync(node, ForkBoundaryBlock - 2);
            var early = await node.EstimateContractCreationGasAsync(InitCodeReturning32Bytes());

            await MineUpToAsync(node, ForkBoundaryBlock + 1);
            var late = await node.EstimateContractCreationGasAsync(InitCodeReturning32Bytes());

            Assert.Equal(early.GasUsed, late.GasUsed);
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public void Given_NoSchedule_When_ResolvingAtAnyBlock_Then_MatchesThePinnedConfig()
        {
            var config = new ChainConfig { Hardfork = "prague" };
            var pinned = config.GetHardforkConfig();

            Assert.Equal(HardforkName.Prague, config.ResolveHardforkAt(0, 0));
            Assert.Equal(HardforkName.Prague, config.ResolveHardforkAt(30_000_000, 2_000_000_000));
            Assert.Same(pinned, config.GetHardforkConfigAt(30_000_000, 2_000_000_000));
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public void Given_MainnetSchedule_When_ResolvingRealActivations_Then_PicksTheForkThatBlockRanUnder()
        {
            var config = new ChainConfig
            {
                Hardfork = "cancun",
                Activations = MainnetChainActivations.Instance
            };

            Assert.Equal(HardforkName.Cancun,
                config.ResolveHardforkAt(19_426_587, MainnetChainActivations.CancunTimestamp));
            Assert.Equal(HardforkName.Prague,
                config.ResolveHardforkAt(22_431_084, MainnetChainActivations.PragueTimestamp));
            Assert.Equal(HardforkName.Osaka,
                config.ResolveHardforkAt(23_935_694, MainnetChainActivations.OsakaTimestamp.Value));
            Assert.Equal(HardforkName.OsakaBpo1,
                config.ResolveHardforkAt(23_976_500, MainnetChainActivations.OsakaBpo1Timestamp.Value));
            Assert.Equal(HardforkName.OsakaBpo2,
                config.ResolveHardforkAt(24_179_383, MainnetChainActivations.OsakaBpo2Timestamp.Value));

            Assert.Equal(HardforkName.Prague,
                config.ResolveHardforkAt(23_935_693, MainnetChainActivations.OsakaTimestamp.Value - 1));
        }

        [Fact]
        [Trait("Category", "ForkResolution")]
        public async Task Given_ScheduledChain_When_QueryingBlobBaseFee_Then_UsesTheHeadBlocksFraction()
        {
            var config = DevChainConfig.Default;
            config.Hardfork = "cancun";
            config.Activations = new BlockBoundaryActivations(
                ForkBoundaryBlock, HardforkName.Cancun, HardforkName.OsakaBpo2);

            using var node = new DevChainNode(config);
            await node.StartAsync();
            await SeedPredeploysForScheduledForkAsync(node, config);
            await MineUpToAsync(node, ForkBoundaryBlock + 1);

            var head = await node.GetLatestBlockAsync();
            head.ExcessBlobGas = 50_000_000;
            await node.Blocks.SaveAsync(head, await node.GetBlockHashByNumberAsync(head.BlockNumber.ToLong()));

            var handler = new EthBlobBaseFeeHandler();
            var response = await handler.HandleAsync(
                new RpcRequestMessage(1, handler.MethodName),
                new RpcContext(node, config.ChainId, null));

            Assert.Null(response.Error);
            var reported = ParseHexBigInteger((string)response.Result);

            var atHeadFork = config
                .GetHardforkConfigAt((long)head.BlockNumber, (ulong)head.Timestamp)
                .IntrinsicGasRules.Blob
                .CalculateBlobBaseFee(new EvmUInt256(50_000_000UL));
            var atPinnedFork = config.GetHardforkConfig()
                .IntrinsicGasRules.Blob
                .CalculateBlobBaseFee(new EvmUInt256(50_000_000UL));

            Assert.True(atHeadFork.ToBigInteger() < atPinnedFork.ToBigInteger(),
                "the two forks must price the same excess differently for this test to mean anything");

            Assert.Equal(atHeadFork.ToBigInteger(), reported);
            Assert.NotEqual(atPinnedFork.ToBigInteger(), reported);
        }

        private static BigInteger ParseHexBigInteger(string hex)
            => BigInteger.Parse("0" + hex.Substring(2), System.Globalization.NumberStyles.HexNumber);
    }
}
