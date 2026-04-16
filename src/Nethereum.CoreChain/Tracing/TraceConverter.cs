using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Util;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Tracing
{
    public static class TraceConverter
    {
        public static OpcodeTraceResult ConvertToOpcodeResult(
            Program program,
            OpcodeTraceConfig config = null)
        {
            config = config ?? new OpcodeTraceConfig();

            var structLogs = new List<OpcodeTraceStep>();
            var traces = program.Trace ?? new List<ProgramTrace>();

            BigInteger gasRemaining = program.ProgramContext?.Gas ?? 0;
            int count = 0;
            int limit = config.Limit > 0 ? config.Limit : int.MaxValue;

            foreach (var trace in traces)
            {
                if (count >= limit)
                    break;

                var step = new OpcodeTraceStep
                {
                    Pc = (ulong)(trace.Instruction?.Step ?? 0),
                    Op = trace.Instruction?.Instruction?.ToString() ?? "UNKNOWN",
                    Gas = gasRemaining < 0 ? 0 : (gasRemaining > ulong.MaxValue ? ulong.MaxValue : (ulong)gasRemaining),
                    GasCost = trace.GasCost < 0 ? 0 : (ulong)trace.GasCost,
                    Depth = trace.Depth + 1,
                    Address = trace.CodeAddress
                };

                gasRemaining -= trace.GasCost;

                if (!config.DisableStack && trace.Stack != null)
                {
                    step.Stack = new List<HexBigInteger>();
                    foreach (var stackItem in trace.Stack)
                    {
                        var value = stackItem.HexToBigInteger(false);
                        step.Stack.Add(new HexBigInteger(value));
                    }
                }

                if (config.EnableMemory && trace.Memory != null)
                {
                    step.Memory = trace.Memory;
                    step.MemSize = trace.Memory.Length / 2;
                }

                if (!config.DisableStorage && trace.Storage != null)
                {
                    step.Storage = new Dictionary<string, string>(trace.Storage);
                }

                structLogs.Add(step);
                count++;
            }

            var returnValue = program.ProgramResult?.Result?.ToHex(true) ?? "0x";

            return new OpcodeTraceResult
            {
                Gas = (ulong)program.TotalGasUsed,
                Failed = program.ProgramResult?.IsRevert ?? false,
                ReturnValue = returnValue,
                StructLogs = structLogs
            };
        }

        public static CallTraceResult ConvertToCallTraceResult(
            Program program,
            CallInput callInput,
            bool isContractCreation)
        {
            var result = program.ProgramResult;
            var output = result?.Result?.ToHex(true) ?? "0x";

            var response = new CallTraceResult
            {
                Type = isContractCreation ? "CREATE" : "CALL",
                From = callInput.From,
                To = isContractCreation ? null : callInput.To,
                Value = callInput.Value ?? new HexBigInteger(0),
                Gas = callInput.Gas ?? new HexBigInteger(0),
                GasUsed = new HexBigInteger(program.TotalGasUsed),
                Input = callInput.Data ?? "0x",
                Output = output
            };

            if (result?.IsRevert == true)
            {
                response.Error = "execution reverted";
                response.RevertReason = result.GetRevertMessage();
            }

            var innerResults = result?.InnerCallResults;
            if (innerResults != null && innerResults.Count > 0)
            {
                response.Calls = BuildNestedCalls(innerResults);
            }

            return response;
        }

        private static readonly string[] FrameTypeNames = { "CALL", "DELEGATECALL", "STATICCALL", "CALLCODE", "CREATE", "CREATE2" };

        private static string GetCallType(int frameType)
        {
            if (frameType >= 1 && frameType <= 6)
                return FrameTypeNames[frameType - 1];
            return "CALL";
        }

        private static List<CallTraceResult> BuildNestedCalls(List<InnerCallResult> flatResults)
        {
            var rootChildren = new List<CallTraceResult>();
            if (flatResults == null || flatResults.Count == 0)
                return rootChildren;

            var stack = new Stack<(CallTraceResult node, int depth)>();

            foreach (var inner in flatResults)
            {
                var node = new CallTraceResult
                {
                    Type = GetCallType(inner.FrameType),
                    From = inner.CallInput?.From,
                    To = inner.CallInput?.To,
                    Value = new HexBigInteger((BigInteger)(inner.CallInput?.Value ?? EvmUInt256.Zero)),
                    Gas = new HexBigInteger(inner.CallInput?.Gas ?? 0),
                    GasUsed = new HexBigInteger(inner.GasUsed),
                    Input = inner.CallInput?.Data != null ? inner.CallInput.Data.ToHex(true) : "0x",
                    Output = inner.Output?.ToHex(true) ?? "0x"
                };

                if (!inner.Success)
                {
                    node.Error = inner.Error;
                    node.RevertReason = inner.RevertReason;
                }

                while (stack.Count > 0 && stack.Peek().depth >= inner.Depth)
                    stack.Pop();

                if (stack.Count == 0)
                {
                    rootChildren.Add(node);
                }
                else
                {
                    var parent = stack.Peek().node;
                    if (parent.Calls == null)
                        parent.Calls = new List<CallTraceResult>();
                    parent.Calls.Add(node);
                }

                stack.Push((node, inner.Depth));
            }

            return rootChildren;
        }

        public static PrestateTraceResult ConvertToPrestateResult(
            ExecutionStateService stateService,
            IStateReader nodeDataService)
        {
            var pre = new Dictionary<string, PrestateAccountInfo>();
            var post = new Dictionary<string, PrestateAccountInfo>();

            foreach (var kvp in stateService.AccountsState)
            {
                var address = kvp.Key;
                var accountState = kvp.Value;

                var preBalance = accountState.Balance.InitialChainBalance ?? BigInteger.Zero;
                var postBalance = accountState.Balance.GetTotalBalance();

                var nonce = (long)(accountState.Nonce ?? 0);

                var preItem = new PrestateAccountInfo
                {
                    Balance = new HexBigInteger(preBalance),
                    Nonce = nonce
                };

                var postItem = new PrestateAccountInfo
                {
                    Balance = new HexBigInteger(postBalance),
                    Nonce = nonce
                };

                if (accountState.Code != null && accountState.Code.Length > 0)
                {
                    preItem.Code = accountState.Code.ToHex(true);
                    postItem.Code = accountState.Code.ToHex(true);
                }

                if (accountState.OriginalStorageValues.Count > 0 || accountState.Storage.Count > 0)
                {
                    preItem.Storage = new Dictionary<string, string>();
                    postItem.Storage = new Dictionary<string, string>();

                    foreach (var storageKvp in accountState.OriginalStorageValues)
                    {
                        var slot = "0x" + storageKvp.Key.ToBigEndian().ToHex().PadLeft(64, '0');
                        preItem.Storage[slot] = storageKvp.Value?.ToHex(true) ?? "0x0";
                    }

                    foreach (var storageKvp in accountState.Storage)
                    {
                        var slot = "0x" + storageKvp.Key.ToBigEndian().ToHex().PadLeft(64, '0');
                        postItem.Storage[slot] = storageKvp.Value?.ToHex(true) ?? "0x0";
                    }
                }

                bool hasChanges = preBalance != postBalance
                    || (preItem.Storage != null && postItem.Storage != null);

                if (hasChanges)
                {
                    pre[address] = preItem;
                    post[address] = postItem;
                }
            }

            return new PrestateTraceResult { Pre = pre, Post = post };
        }
    }
}
