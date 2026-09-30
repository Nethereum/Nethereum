using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class AccessListAndTraceCallCompositionTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Target = "0x9999999999999999999999999999999999999a";

        private const string InitCodeSstoreThenEmptyReturn = "0x600160005560006000f3";

        private const string RuntimeCodeSstoreOnce = "0x600160005500";

        private const string RevertingCode = "0x60006000fd";

        private static async Task<DevChainNode> StartAsync(string fork)
        {
            var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = fork });
            await node.StartAsync(new[] { Caller });
            return node;
        }

        [Fact]
        public async Task Given_SenderNonceIsNonZero_When_TraceCallCreatesContract_Then_TracedContractAddressMatchesRealNonce()
        {
            using var node = await StartAsync("prague");
            await node.SetNonceAsync(Caller, 5);

            var expectedAddress = ContractUtils.CalculateContractAddress(Caller, 5);
            var staleZeroNonceAddress = ContractUtils.CalculateContractAddress(Caller, 0);

            var prestate = await node.TraceCallPrestateAsync(
                new CallInput { From = Caller, Data = InitCodeSstoreThenEmptyReturn });

            Assert.Contains(prestate.Post.Keys, key => key.IsTheSameAddress(expectedAddress));
            Assert.DoesNotContain(prestate.Post.Keys, key => key.IsTheSameAddress(staleZeroNonceAddress));
        }

        [Fact]
        public async Task Given_ToIsEmpty_When_CreateAccessListRequested_Then_AccessListIsPopulatedAndGasUsedIsPositive()
        {
            using var node = await StartAsync("prague");

            var result = await node.CreateAccessListAsync(
                to: null, data: InitCodeSstoreThenEmptyReturn.HexToByteArray(), from: Caller);

            Assert.True(result.GasUsed > 0, $"expected positive GasUsed, got {result.GasUsed}");
            Assert.NotEmpty(result.AccessList);
        }

        [Fact]
        public async Task Given_SenderNonceIsNonZero_When_CreateAccessListDiscoversCreation_Then_ComputedContractAddressMatchesRealNonce()
        {
            using var node = await StartAsync("prague");
            await node.SetNonceAsync(Caller, 5);

            var expectedAddress = ContractUtils.CalculateContractAddress(Caller, 5);
            var staleZeroNonceAddress = ContractUtils.CalculateContractAddress(Caller, 0);

            var result = await node.CreateAccessListAsync(
                to: null, data: InitCodeSstoreThenEmptyReturn.HexToByteArray(), from: Caller);

            Assert.Contains(result.AccessList, item => item.Address.IsTheSameAddress(expectedAddress));
            Assert.DoesNotContain(result.AccessList, item => item.Address.IsTheSameAddress(staleZeroNonceAddress));
        }

        [Fact]
        public async Task Given_Amsterdam_StateGasActive_When_TraceCallExecutesFreshSstore_Then_ExecutionGasMatchesCallAsyncLessIntrinsic()
        {
            using var node = await StartAsync("amsterdam");
            await node.SetCodeAsync(Target, RuntimeCodeSstoreOnce.HexToByteArray());

            // A gas limit above EIP8037_TX_MAX_GAS_LIMIT (16,777,216) is required for
            // AllocateEvmGas to actually open a non-zero state-gas reservoir; below that
            // cap the reservoir is always zero and this test would be vacuous w.r.t.
            // RPC-COMP-03 (the reservoir would be zero on both the fixed and the old
            // bespoke path alike).
            const long generousGasLimit = 20_000_000;

            var callResult = await node.CallAsync(Target, Array.Empty<byte>(), Caller, gasLimit: generousGasLimit);
            Assert.True(callResult.Success, $"eth_call failed: {callResult.RevertReason}");

            var trace = await node.TraceCallCallTracerAsync(
                new CallInput { From = Caller, To = Target, Data = "0x", Gas = new Nethereum.Hex.HexTypes.HexBigInteger(generousGasLimit) });

            var expectedTraceGas = (BigInteger)(callResult.ExecutionGasUsed - callResult.IntrinsicGasUsed);

            Assert.Equal(expectedTraceGas, trace.GasUsed.Value);
        }

        [Fact]
        public async Task Given_TraceCallOnRevertingCreate_When_ConvertedToCallTraceResult_Then_MatchesTraceCallOnRevertingCall_ForTheSharedFields()
        {
            using var node = await StartAsync("prague");
            await node.SetCodeAsync(Target, RevertingCode.HexToByteArray());

            var createTrace = await node.TraceCallCallTracerAsync(
                new CallInput { From = Caller, Data = RevertingCode });
            var callTrace = await node.TraceCallCallTracerAsync(
                new CallInput { From = Caller, To = Target, Data = "0x" });

            Assert.Equal("CREATE", createTrace.Type);
            Assert.Equal("CALL", callTrace.Type);
            Assert.Equal("execution reverted", createTrace.Error);
            Assert.Equal("execution reverted", callTrace.Error);
            Assert.True(createTrace.GasUsed.Value > 0);
            Assert.True(callTrace.GasUsed.Value > 0);
        }
    }
}
