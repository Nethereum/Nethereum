using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.Tracing;
using Nethereum.CoreChain.Rpc;
using Nethereum.EVM;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Tracing
{
    public class TraceConverterCallTraceTests
    {
        private static string Addr(string a) => RpcTransactionAddress.Normalize(a);

        private static CallInput MakeCallInput(string from, string to, string data = "0x")
        {
            return new CallInput
            {
                From = from,
                To = to,
                Data = data,
                Value = new HexBigInteger(0),
                Gas = new HexBigInteger(100000)
            };
        }

        private static EvmCallContext MakeCall(string from, string to, string data = "0x")
        {
            return new EvmCallContext
            {
                From = from,
                To = to,
                Data = data != null ? data.HexToByteArray() : Array.Empty<byte>(),
                Value = EvmUInt256.Zero,
                Gas = 100000
            };
        }

        private static Program CreateProgramWithInnerCalls(List<InnerCallResult> innerCalls)
        {
            var program = new Program(new byte[] { 0x00 });
            program.ProgramResult.InnerCallResults.AddRange(innerCalls);
            return program;
        }

        [Fact]
        public void FlatSiblingCalls_AllAtSameDepth_AreSiblings()
        {
            var innerCalls = new List<InnerCallResult>
            {
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xA"),
                    FrameType = 1,
                    Depth = 1,
                    GasUsed = 1000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xB"),
                    FrameType = 3,
                    Depth = 1,
                    GasUsed = 2000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xC"),
                    FrameType = 2,
                    Depth = 1,
                    GasUsed = 500,
                    Output = new byte[0],
                    Success = true
                }
            };

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Equal("CALL", result.Type);
            Assert.NotNull(result.Calls);
            Assert.Equal(3, result.Calls.Count);

            Assert.Equal("CALL", result.Calls[0].Type);
            Assert.Equal(Addr("0xA"),result.Calls[0].To);
            Assert.Null(result.Calls[0].Calls);

            Assert.Equal("STATICCALL", result.Calls[1].Type);
            Assert.Equal(Addr("0xB"),result.Calls[1].To);
            Assert.Null(result.Calls[1].Calls);

            Assert.Equal("DELEGATECALL", result.Calls[2].Type);
            Assert.Equal(Addr("0xC"),result.Calls[2].To);
            Assert.Null(result.Calls[2].Calls);
        }

        [Fact]
        public void NestedCalls_TwoLevelsDeep_BuildsCorrectHierarchy()
        {
            var innerCalls = new List<InnerCallResult>
            {
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xA"),
                    FrameType = 1,
                    Depth = 1,
                    GasUsed = 5000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xA", "0xB"),
                    FrameType = 1,
                    Depth = 2,
                    GasUsed = 1000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xA", "0xC"),
                    FrameType = 3,
                    Depth = 2,
                    GasUsed = 500,
                    Output = new byte[0],
                    Success = true
                }
            };

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Single(result.Calls);
            var callA = result.Calls[0];
            Assert.Equal(Addr("0xA"),callA.To);
            Assert.NotNull(callA.Calls);
            Assert.Equal(2, callA.Calls.Count);
            Assert.Equal(Addr("0xB"),callA.Calls[0].To);
            Assert.Equal("CALL", callA.Calls[0].Type);
            Assert.Equal(Addr("0xC"),callA.Calls[1].To);
            Assert.Equal("STATICCALL", callA.Calls[1].Type);
        }

        [Fact]
        public void DeeplyNestedCalls_ThreeLevels_BuildsCorrectHierarchy()
        {
            var innerCalls = new List<InnerCallResult>
            {
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xA"),
                    FrameType = 1,
                    Depth = 1,
                    GasUsed = 10000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xA", "0xB"),
                    FrameType = 1,
                    Depth = 2,
                    GasUsed = 3000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xB", "0xE"),
                    FrameType = 1,
                    Depth = 3,
                    GasUsed = 500,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xA", "0xC"),
                    FrameType = 2,
                    Depth = 2,
                    GasUsed = 200,
                    Output = new byte[0],
                    Success = true
                }
            };

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Single(result.Calls);
            var callA = result.Calls[0];
            Assert.Equal(Addr("0xA"),callA.To);

            Assert.Equal(2, callA.Calls.Count);
            var callB = callA.Calls[0];
            var callC = callA.Calls[1];
            Assert.Equal(Addr("0xB"),callB.To);
            Assert.Equal(Addr("0xC"),callC.To);
            Assert.Equal("DELEGATECALL", callC.Type);

            Assert.Single(callB.Calls);
            Assert.Equal(Addr("0xE"),callB.Calls[0].To);
            Assert.Null(callB.Calls[0].Calls);

            Assert.Null(callC.Calls);
        }

        [Fact]
        public void MultipleBranches_ComplexTree_BuildsCorrectly()
        {
            var innerCalls = new List<InnerCallResult>
            {
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xA"),
                    FrameType = 1, Depth = 1, GasUsed = 5000,
                    Output = new byte[0], Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xA", "0xB"),
                    FrameType = 1, Depth = 2, GasUsed = 1000,
                    Output = new byte[0], Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xA", "0xC"),
                    FrameType = 3, Depth = 2, GasUsed = 500,
                    Output = new byte[0], Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xD"),
                    FrameType = 1, Depth = 1, GasUsed = 3000,
                    Output = new byte[0], Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xD", "0xF"),
                    FrameType = 1, Depth = 2, GasUsed = 200,
                    Output = new byte[0], Success = true
                }
            };

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Equal(2, result.Calls.Count);

            var callA = result.Calls[0];
            Assert.Equal(Addr("0xA"),callA.To);
            Assert.Equal(2, callA.Calls.Count);
            Assert.Equal(Addr("0xB"),callA.Calls[0].To);
            Assert.Equal(Addr("0xC"),callA.Calls[1].To);

            var callD = result.Calls[1];
            Assert.Equal(Addr("0xD"),callD.To);
            Assert.Single(callD.Calls);
            Assert.Equal(Addr("0xF"),callD.Calls[0].To);
        }

        [Fact]
        public void FailedCall_PropagatesErrorAndRevertReason()
        {
            var innerCalls = new List<InnerCallResult>
            {
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xA"),
                    FrameType = 1,
                    Depth = 1,
                    GasUsed = 1000,
                    Output = new byte[0],
                    Success = false,
                    Error = "execution reverted",
                    RevertReason = "insufficient balance"
                }
            };

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Single(result.Calls);
            Assert.Equal("execution reverted", result.Calls[0].Error);
            Assert.Equal("insufficient balance", result.Calls[0].RevertReason);
        }

        [Fact]
        public void CreateCalls_SetCorrectTypeAndNullTo()
        {
            var innerCalls = new List<InnerCallResult>
            {
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xNewContract"),
                    FrameType = 5,
                    Depth = 1,
                    GasUsed = 30000,
                    Output = new byte[0],
                    Success = true
                },
                new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", "0xNewContract2"),
                    FrameType = 6,
                    Depth = 1,
                    GasUsed = 32000,
                    Output = new byte[0],
                    Success = true
                }
            };

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Equal(2, result.Calls.Count);
            Assert.Equal("CREATE", result.Calls[0].Type);
            Assert.Equal("CREATE2", result.Calls[1].Type);
        }

        [Fact]
        public void NoInnerCalls_ReturnsNullCalls()
        {
            var program = new Program(new byte[] { 0x00 });
            var callInput = MakeCallInput("0xsender", "0xcontract");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Equal("CALL", result.Type);
            Assert.Equal(Addr("0xsender"), result.From);
            Assert.Equal(Addr("0xcontract"), result.To);
            Assert.Null(result.Calls);
        }

        [Fact]
        public void ContractCreation_SetsTypeToCreate()
        {
            var program = new Program(new byte[] { 0x00 });
            var callInput = MakeCallInput("0xsender", null);

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, true);

            Assert.Equal("CREATE", result.Type);
            Assert.Null(result.To);
        }

        [Fact]
        public void AllFrameTypes_MapCorrectly()
        {
            var innerCalls = new List<InnerCallResult>();
            var expectedTypes = new[] { "CALL", "DELEGATECALL", "STATICCALL", "CALLCODE", "CREATE", "CREATE2" };

            for (int i = 1; i <= 6; i++)
            {
                innerCalls.Add(new InnerCallResult
                {
                    CallInput = MakeCall("0xroot", $"0x{i:x}"),
                    FrameType = i,
                    Depth = 1,
                    GasUsed = 100,
                    Output = new byte[0],
                    Success = true
                });
            }

            var program = CreateProgramWithInnerCalls(innerCalls);
            var callInput = MakeCallInput("0xsender", "0xroot");

            var result = TraceConverter.ConvertToCallTraceResult(program, callInput, false);

            Assert.Equal(6, result.Calls.Count);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(expectedTypes[i], result.Calls[i].Type);
            }
        }
    }
}
