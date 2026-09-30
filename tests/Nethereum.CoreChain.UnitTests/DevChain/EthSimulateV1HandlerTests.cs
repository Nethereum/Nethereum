using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.DevChain;
using Nethereum.EVM.Execution.TransferLogs.Rules;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EthSimulateV1HandlerTests
    {
        private const string Caller = "0x1234567890123456789012345678901234567890";
        private const string Recipient = "0x00000000000000000000000000000000000abc";
        private const string CounterContract = "0x00000000000000000000000000000000009001";
        private const string RevertingContract = "0x00000000000000000000000000000000009002";
        private const string UnfundedSender = "0x000000000000000000000000000000000000ad";
        private const string LowBalanceSender = "0x00000000000000000000000000000000000000ba";
        private const string NestedCallContract = "0x00000000000000000000000000000000009101";
        private const string NestedTransferRecipient = "0x00000000000000000000000000000000009102";

        private static readonly string EmptyContractInitCode = "0x60006000f3";

        private static readonly string IncrementAndReturnCode =
            "0x6000546001018060005560005260206000f3";

        private static readonly string WritesSlotZeroThenReverts =
            "0x600160005560006000fd";

        private static readonly string ForwardsOneWeiToNestedRecipientCode =
            "0x600060006000600060017300000000000000000000000000000000000091025af100";

        private static async Task<List<EthSimulateBlockResult>> SimulateAsync(
            DevChainNode node, object payload)
        {
            var response = await new EthSimulateV1Handler().HandleAsync(
                new RpcRequestMessage(1, "eth_simulateV1", payload),
                new RpcContext(node, chainId: node.Config.ChainId, services: null));

            Assert.False(response.HasError, response.Error?.Message);
            return Assert.IsType<List<EthSimulateBlockResult>>(response.ResultNewtonsoft);
        }

        private static async Task<RpcException> SimulateExpectingRpcErrorAsync(DevChainNode node, object payload)
        {
            return await Assert.ThrowsAsync<RpcException>(() => new EthSimulateV1Handler().HandleAsync(
                new RpcRequestMessage(1, "eth_simulateV1", payload),
                new RpcContext(node, chainId: node.Config.ChainId, services: null)));
        }

        [Fact]
        public async Task Given_ASingleValueTransferCall_When_SimulatingV1_Then_TheBlockResultReportsSuccess()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                blockStateCalls = new object[]
                {
                    new
                    {
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Single(blocks);
            Assert.Single(blocks[0].Calls);
            Assert.Equal("0x1", blocks[0].Calls[0].Status);
        }

        [Fact]
        public async Task Given_TwoCallsInOneBlockAgainstAnAccumulatingContract_When_SimulatingV1_Then_TheSecondCallObservesTheFirstsWrite()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var payload = new
            {
                blockStateCalls = new object[]
                {
                    new
                    {
                        stateOverrides = new Dictionary<string, object>
                        {
                            [CounterContract] = new { code = IncrementAndReturnCode }
                        },
                        calls = new object[]
                        {
                            new { to = CounterContract },
                            new { to = CounterContract }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Single(blocks);
            Assert.Equal(2, blocks[0].Calls.Count);

            Assert.Equal("0x1", blocks[0].Calls[0].Status);
            Assert.Equal("0x1", blocks[0].Calls[1].Status);

            var firstValue = blocks[0].Calls[0].ReturnData.HexToBigInteger(false);
            var secondValue = blocks[0].Calls[1].ReturnData.HexToBigInteger(false);

            Assert.Equal(BigInteger.One, firstValue);
            Assert.Equal(new BigInteger(2), secondValue);
        }

        [Fact]
        public async Task Given_ABlockOverrideSettingNumber_When_SimulatingV1_Then_TheReturnedBlockCarriesThatNumber()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var payload = new
            {
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { number = "0xa" },
                        calls = new object[0]
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Equal(10, blocks.Count);
            Assert.Equal(new BigInteger(10), blocks[^1].Number.Value);
        }

        [Fact]
        public async Task Given_ACallThatReverts_When_SimulatingV1_Then_TheCallStatusIsFailureWithAnError()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var payload = new
            {
                blockStateCalls = new object[]
                {
                    new
                    {
                        stateOverrides = new Dictionary<string, object>
                        {
                            [RevertingContract] = new { code = WritesSlotZeroThenReverts }
                        },
                        calls = new object[]
                        {
                            new { to = RevertingContract }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Single(blocks);
            Assert.Single(blocks[0].Calls);
            Assert.Equal("0x0", blocks[0].Calls[0].Status);
            Assert.NotNull(blocks[0].Calls[0].Error);
        }

        private static bool HasSystemTransferLog(EthSimulateCallResult call)
        {
            return call.Logs != null && call.Logs.Any(l =>
                string.Equals(l.Address, Eip7708EthTransferLogRule.SystemAddress, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasSimulateTransferLog(EthSimulateCallResult call)
        {
            return call.Logs != null && call.Logs.Any(l =>
                string.Equals(l.Address, SimulateTraceTransferLogRule.TraceTransferAddress, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Given_AValueTransferCall_When_TraceTransfersTrueOnAPreAmsterdamChain_Then_ATransferLogIsEmitted()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                traceTransfers = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.True(HasSimulateTransferLog(blocks[0].Calls[0]));
        }

        [Fact]
        public async Task Given_AValueTransferCall_When_TraceTransfersFalseOnAPreAmsterdamChain_Then_NoTransferLogIsEmitted()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                traceTransfers = false,
                blockStateCalls = new object[]
                {
                    new
                    {
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.False(HasSystemTransferLog(blocks[0].Calls[0]));
        }

        [Fact]
        public async Task Given_ATopLevelCallThatMakesAnInternalValueTransferringCall_When_TraceTransfersTrueOnSimulateV1_Then_TheInnerTransferAlsoEmitsALog()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                traceTransfers = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        stateOverrides = new Dictionary<string, object>
                        {
                            [NestedCallContract] = new { code = ForwardsOneWeiToNestedRecipientCode }
                        },
                        calls = new object[]
                        {
                            new { from = Caller, to = NestedCallContract, value = "0x2" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Equal("0x1", blocks[0].Calls[0].Status);
            var transferLogs = blocks[0].Calls[0].Logs
                .Where(l => string.Equals(l.Address, SimulateTraceTransferLogRule.TraceTransferAddress, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Equal(2, transferLogs.Count);
        }

        [Fact]
        public async Task Given_AValueTransferCall_When_TraceTransfersFalseOnAnAmsterdamChain_Then_TheConsensusTransferLogStillAppears()
        {
            using var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = "amsterdam" });
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                traceTransfers = false,
                blockStateCalls = new object[]
                {
                    new
                    {
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.True(HasSystemTransferLog(blocks[0].Calls[0]));
        }

        [Fact]
        public async Task Given_ValidationTrueWithMaxFeeBelowBaseFee_When_SimulatingV1_Then_TheCallFailsWithAFeeError()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x3b9aca00" },
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x0", maxFeePerGas = "0x1" }
                        }
                    }
                }
            };

            var error = await SimulateExpectingRpcErrorAsync(node, payload);

            Assert.Equal(-38012, error.Code);
            Assert.Contains("base fee", error.Message);
        }

        [Fact]
        public async Task Given_ValidationTrueWithAWellFundedSender_When_SimulatingV1_Then_TheCallSucceeds()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x3b9aca00" },
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1", maxFeePerGas = "0x77359400" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Equal("0x1", blocks[0].Calls[0].Status);
            Assert.Null(blocks[0].Calls[0].Error);
        }

        [Fact]
        public async Task Given_ValidationTrueWithAnUnfundedSender_When_SimulatingV1_Then_TheCallFailsWithAnInsufficientFundsError()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x0" },
                        calls = new object[]
                        {
                            new { from = UnfundedSender, to = Recipient, value = "0x1", maxFeePerGas = "0x1" }
                        }
                    }
                }
            };

            var error = await SimulateExpectingRpcErrorAsync(node, payload);

            Assert.Equal(-38014, error.Code);
            Assert.Contains("insufficient funds", error.Message);
        }

        [Fact]
        public async Task Given_ValidationFalseWithTheSameUnfundedLowFeeCall_When_SimulatingV1_Then_TheRequestStillFailsOnInsufficientBalance()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync();

            var payload = new
            {
                validation = false,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x3b9aca00" },
                        calls = new object[]
                        {
                            new { from = UnfundedSender, to = Recipient, value = "0x1", maxFeePerGas = "0x1" }
                        }
                    }
                }
            };

            var error = await SimulateExpectingRpcErrorAsync(node, payload);

            Assert.Equal(-38014, error.Code);
            Assert.Contains("Insufficient balance", error.Message);
        }

        [Fact]
        public async Task Given_ValidationTrueWithAWrongNonce_When_SimulatingV1_Then_TheCallFailsWithAnInvalidNonceError()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x0" },
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1", nonce = "0x5" }
                        }
                    }
                }
            };

            var error = await SimulateExpectingRpcErrorAsync(node, payload);

            Assert.Equal(-38010, error.Code);
            Assert.Contains("invalid nonce", error.Message);
        }

        [Fact]
        public async Task Given_ValidationTrueWithTwoSequentialCallsFromTheSameSender_When_SimulatingV1_Then_TheExpectedNonceAdvancesAndBothSucceed()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x0" },
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1" },
                            new { from = Caller, to = Recipient, value = "0x1" }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Equal("0x1", blocks[0].Calls[0].Status);
            Assert.Equal("0x1", blocks[0].Calls[1].Status);
        }

        [Fact]
        public async Task Given_ValidationTrueWithTwoCallsRepeatingTheSameNonce_When_SimulatingV1_Then_TheSecondCallFailsWithAnInvalidNonceError()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x0" },
                        calls = new object[]
                        {
                            new { from = Caller, to = Recipient, value = "0x1", nonce = "0x0" },
                            new { from = Caller, to = Recipient, value = "0x1", nonce = "0x0" }
                        }
                    }
                }
            };

            var error = await SimulateExpectingRpcErrorAsync(node, payload);

            Assert.Equal(-38010, error.Code);
            Assert.Contains("invalid nonce", error.Message);
        }

        [Fact]
        public async Task Given_ValidationTrueWithTwoSequentialCallsDrainingALowBalanceSender_When_SimulatingV1_Then_TheSecondCallFailsWithAnInsufficientFundsError()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { LowBalanceSender }, BigInteger.One);

            var payload = new
            {
                validation = true,
                blockStateCalls = new object[]
                {
                    new
                    {
                        blockOverrides = new { baseFeePerGas = "0x0" },
                        calls = new object[]
                        {
                            new { from = LowBalanceSender, to = Recipient, value = "0x1", maxFeePerGas = "0x0" },
                            new { from = LowBalanceSender, to = Recipient, value = "0x1", maxFeePerGas = "0x0" }
                        }
                    }
                }
            };

            var error = await SimulateExpectingRpcErrorAsync(node, payload);

            Assert.Equal(-38014, error.Code);
            Assert.Contains("insufficient funds", error.Message);
        }

        [Fact]
        public async Task Given_TwoSequentialContractCreationCallsFromTheSameSender_When_SimulatingV1_Then_BothSucceedAtDifferentAddresses()
        {
            using var node = DevChainNode.CreateInMemory();
            await node.StartAsync(new[] { Caller }, BigInteger.Parse("1000000000000000000"));

            var payload = new
            {
                blockStateCalls = new object[]
                {
                    new
                    {
                        calls = new object[]
                        {
                            new { from = Caller, data = EmptyContractInitCode },
                            new { from = Caller, data = EmptyContractInitCode }
                        }
                    }
                }
            };

            var blocks = await SimulateAsync(node, payload);

            Assert.Equal("0x1", blocks[0].Calls[0].Status);
            Assert.Equal("0x1", blocks[0].Calls[1].Status);
        }
    }
}
