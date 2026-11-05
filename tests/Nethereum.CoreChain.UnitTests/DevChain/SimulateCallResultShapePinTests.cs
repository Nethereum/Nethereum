using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class SimulateCallResultShapePinTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Reverting = "0x00000000000000000000000000000000009011";
        private const string Looping = "0x00000000000000000000000000000000009012";
        private const string Invalid = "0x00000000000000000000000000000000009013";
        private const string Returning = "0x00000000000000000000000000000000009014";

        private const string RevertsWithFourBytes = "0x63deadbeef6000526004601cfd";
        private const string LoopsForever = "0x5b600056";
        private const string InvalidOpcode = "0xfe";
        private const string ReturnsOneWord = "0x602a60005260206000f3";

        private static async Task<EthSimulateCallResult> SimulateOneCallAsync(string contract, string code, string gas = null)
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));
            var call = gas == null
                ? (object)new { from = Caller, to = contract }
                : new { from = Caller, to = contract, gas };
            var payload = new
            {
                blockStateCalls = new object[]
                {
                    new
                    {
                        stateOverrides = new Dictionary<string, object> { [contract] = new { code } },
                        calls = new object[] { call }
                    }
                }
            };

            var response = await new EthSimulateV1Handler().HandleAsync(
                new RpcRequestMessage(1, "eth_simulateV1", payload),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            var blocks = Assert.IsType<List<EthSimulateBlockResult>>(response.ResultNewtonsoft);
            return Assert.Single(Assert.Single(blocks).Calls);
        }

        [Fact]
        public async Task Given_RevertingCall_When_Simulated_Then_ReturnDataIs0xAndErrorIsRevertError()
        {
            var call = await SimulateOneCallAsync(Reverting, RevertsWithFourBytes);

            Assert.Equal("0x0", call.Status);
            Assert.Equal("0x", call.ReturnData);
            var error = Assert.IsType<EthSimulateCallError>(call.Error);
            Assert.Equal(3, error.Code);
            Assert.StartsWith("execution reverted", error.Message);
            Assert.Equal("0xdeadbeef", error.Data);
            Assert.NotNull(call.GasUsed);
            Assert.NotNull(call.MaxUsedGas);
            Assert.NotNull(call.Logs);
        }

        [Fact]
        public async Task Given_OutOfGasCall_When_Simulated_Then_Code32015()
        {
            var call = await SimulateOneCallAsync(Looping, LoopsForever, gas: "0x7530");

            Assert.Equal("0x0", call.Status);
            Assert.Equal("0x", call.ReturnData);
            var error = Assert.IsType<EthSimulateCallError>(call.Error);
            Assert.Equal(-32015, error.Code);
            Assert.Equal("out of gas", error.Message);
            Assert.Null(error.Data);
            Assert.Equal(new BigInteger(0x7530), call.GasUsed.Value);
        }

        [Fact]
        public async Task Given_FailedNonRevertCall_When_Simulated_Then_ReturnDataIsRawAndErrorIsMessage()
        {
            var call = await SimulateOneCallAsync(Invalid, InvalidOpcode);

            Assert.Equal("0x0", call.Status);
            Assert.NotNull(call.ReturnData);
            Assert.NotNull(call.Error);
            Assert.NotNull(call.GasUsed);
            Assert.NotNull(call.Logs);
        }

        [Fact]
        public async Task Given_SucceedingCall_When_Simulated_Then_StatusIs0x1WithReturnDataAndNoError()
        {
            var call = await SimulateOneCallAsync(Returning, ReturnsOneWord);

            Assert.Equal("0x1", call.Status);
            Assert.Equal("0x" + new string('0', 62) + "2a", call.ReturnData);
            Assert.Null(call.Error);
            Assert.NotNull(call.MaxUsedGas);
        }
    }
}
