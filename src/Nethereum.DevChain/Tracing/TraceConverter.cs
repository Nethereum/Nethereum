using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Geth.RPC.Debug.Tracers;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;

namespace Nethereum.DevChain.Tracing
{
    public static class TraceConverter
    {
        public static OpcodeTracerResponse ConvertToOpcodeResponse(
            Program program,
            OpcodeTracerConfigDto config = null)
        {
            config = config ?? new OpcodeTracerConfigDto();

            var structLogs = new List<StructLog>();
            var traces = program.Trace ?? new List<ProgramTrace>();

            BigInteger gasRemaining = program.ProgramContext?.Gas ?? 0;
            int count = 0;
            int limit = config.Limit > 0 ? config.Limit : int.MaxValue;

            foreach (var trace in traces)
            {
                if (count >= limit)
                    break;

                var structLog = new StructLog
                {
                    Pc = (ulong)(trace.Instruction?.Step ?? 0),
                    Op = trace.Instruction?.Instruction?.ToString() ?? "UNKNOWN",
                    Gas = gasRemaining < 0 ? 0 : (gasRemaining > ulong.MaxValue ? ulong.MaxValue : (ulong)gasRemaining),
                    GasCost = trace.GasCost < 0 ? 0 : (trace.GasCost > ulong.MaxValue ? ulong.MaxValue : (ulong)trace.GasCost),
                    Depth = trace.Depth
                };

                gasRemaining -= trace.GasCost;

                if (!config.DisableStack && trace.Stack != null)
                {
                    structLog.Stack = new List<HexBigInteger>();
                    foreach (var stackItem in trace.Stack)
                    {
                        var value = stackItem.HexToBigInteger(false);
                        structLog.Stack.Add(new HexBigInteger(value));
                    }
                }

                if (config.EnableMemory && trace.Memory != null)
                {
                    structLog.Memory = trace.Memory;
                    structLog.MemSize = trace.Memory.Length / 2;
                }

                if (!config.DisableStorage && trace.Storage != null)
                {
                    structLog.Storage = new Dictionary<string, string>(trace.Storage);
                }

                structLogs.Add(structLog);
                count++;
            }

            var returnValue = program.ProgramResult?.Result?.ToHex(true) ?? "0x";

            return new OpcodeTracerResponse
            {
                Gas = (ulong)program.TotalGasUsed,
                Failed = program.ProgramResult?.IsRevert ?? false,
                ReturnValue = returnValue,
                StructLogs = structLogs
            };
        }
    }
}
