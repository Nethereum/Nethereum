using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Forks;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.Rpc
{
    public class CreateAccessListGasPricingTests : IAsyncLifetime, IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _shared;

        public CreateAccessListGasPricingTests(DevChainNodeFixture shared) { _shared = shared; }

        private const string Sender = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string Recipient = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const int NonZeroCalldataBytes = 100;

        // EIP-2028 §Specification: "The gas per non-zero byte is reduced from 68 to 16."
        private const long TxBase = 21000;
        private const long HomesteadPerNonZeroByte = 68;
        private const long PostEip2028PerNonZeroByte = 16;

        private const long TokensPerNonZeroByte = 4;
        private const long TotalCostFloorPerToken = 10;
        private const long StandardTokenCost = 4;

        private const long AccessListAddressCost = 2400;
        private const long AccessListStorageKeyCost = 1900;

        private DevChainNode _node = null!;
        private readonly BigInteger _chainId = 31337;

        public async Task InitializeAsync()
        {
            _node = new DevChainNode(new DevChainConfig
            {
                ChainId = _chainId,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            });
            await _node.StartAsync(new[] { Sender }, BigInteger.Parse("10000000000000000000000"));
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        private async Task<BigInteger> GasReportedAtAsync(HardforkName fork)
        {
            _node.Config.Activations = new FixedChainActivations(fork);

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            var context = new RpcContext(_node, _chainId, new ServiceCollection().BuildServiceProvider());

            var callInput = new
            {
                from = Sender,
                to = Recipient,
                data = "0x" + string.Concat(Enumerable.Repeat("ab", NonZeroCalldataBytes))
            };

            var response = await new RpcDispatcher(registry, context)
                .DispatchAsync(new RpcRequestMessage(1, "eth_createAccessList", callInput, "latest"));

            Assert.Null(response.Error);
            var result = JObject.FromObject(response.Result);
            Assert.Empty((JArray)result["accessList"]);
            return new Nethereum.Hex.HexTypes.HexBigInteger(result["gasUsed"].Value<string>()).Value;
        }

        [Fact]
        public async Task Given_TwoForkSchedulesOverTheSameCall_When_CreateAccessListPricesIt_Then_TheBlocksForkDecidesTheCalldataCost()
        {
            var homestead = await GasReportedAtAsync(HardforkName.Homestead);
            var istanbul = await GasReportedAtAsync(HardforkName.Istanbul);

            Assert.Equal(TxBase + NonZeroCalldataBytes * HomesteadPerNonZeroByte, homestead);
            Assert.Equal(TxBase + NonZeroCalldataBytes * PostEip2028PerNonZeroByte, istanbul);
            Assert.Equal(NonZeroCalldataBytes * (HomesteadPerNonZeroByte - PostEip2028PerNonZeroByte),
                homestead - istanbul);
        }

        [Fact]
        public async Task Given_CalldataHeavyEnoughToTripTheFloor_When_CreateAccessListPricesItAtPrague_Then_TheReportedGasIsTheEip7623Floor()
        {
            var floor = TxBase + TotalCostFloorPerToken * (NonZeroCalldataBytes * TokensPerNonZeroByte);

            Assert.Equal(floor, await GasReportedAtAsync(HardforkName.Prague));
        }

        [Fact]
        public async Task Given_ACallThatBothTripsTheFloorAndExecutesCode_When_CreateAccessListPricesIt_Then_ExecutionGasIsInsideTheMaxAndNotDiscarded()
        {
            var contract = await _shared.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));
            var calldata = "0x70a08231000000000000000000000000" + _shared.Address.Substring(2).ToLowerInvariant();

            const long NonZero = 24, Zero = 12;
            var floor = TxBase + TotalCostFloorPerToken * (NonZero * TokensPerNonZeroByte + Zero);

            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();
            var context = new RpcContext(_shared.Node, _shared.ChainId, new ServiceCollection().BuildServiceProvider());
            var response = await new RpcDispatcher(registry, context).DispatchAsync(
                new RpcRequestMessage(1, "eth_createAccessList",
                    new { from = _shared.Address, to = contract, data = calldata }, "latest"));

            Assert.Null(response.Error);
            var reported = new Nethereum.Hex.HexTypes.HexBigInteger(
                JObject.FromObject(response.Result)["gasUsed"].Value<string>()).Value;

            var list = (JArray)JObject.FromObject(response.Result)["accessList"];
            long addresses = list.Count;
            long storageKeys = list.Sum(a => ((JArray)a["storageKeys"]).Count);
            Assert.True(addresses > 0, "premise: the call must produce a non-empty access list");

            var intrinsicWithList = TxBase + NonZero * PostEip2028PerNonZeroByte + Zero * StandardTokenCost
                + addresses * AccessListAddressCost + storageKeys * AccessListStorageKeyCost;
            var mempoolAdmissionBound = Math.Max(intrinsicWithList, floor);

            Assert.True(reported > mempoolAdmissionBound,
                $"execution gas was discarded: expected more than max(intrinsic {intrinsicWithList}, "
                + $"floor {floor}) = {mempoolAdmissionBound}, got {reported}");
        }

        [Fact]
        public async Task Given_TheSameCalldataAtAForkWithNoFloorRule_When_CreateAccessListPricesIt_Then_TheReportedGasIsBelowThatFloor()
        {
            var floor = TxBase + TotalCostFloorPerToken * (NonZeroCalldataBytes * TokensPerNonZeroByte);
            var istanbul = await GasReportedAtAsync(HardforkName.Istanbul);

            Assert.Equal(TxBase + NonZeroCalldataBytes * PostEip2028PerNonZeroByte, istanbul);
            Assert.True(istanbul < floor,
                $"expected the no-floor fork to report less than the Prague floor {floor}, got {istanbul}");
        }
    }
}
