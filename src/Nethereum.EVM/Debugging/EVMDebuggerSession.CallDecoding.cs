using Nethereum.ABI.Model;
using Nethereum.ABI.FunctionEncoding;
using System;
using System.Collections.Generic;

namespace Nethereum.EVM.Debugging
{
    public partial class EVMDebuggerSession
    {
        private const int CallTargetStackIndex = 1;

        private Dictionary<int, string> _functionNameCache = new Dictionary<int, string>();

        public string GetFunctionNameForStep(int stepIndex)
        {
            if (!IsWithinTrace(stepIndex))
                return null;

            if (_functionNameCache.TryGetValue(stepIndex, out var cached))
                return cached;

            var functionName = FindDispatchedFunctionNameAtOrBefore(stepIndex);
            _functionNameCache[stepIndex] = functionName;
            return functionName;
        }

        public string GetCurrentContractName()
        {
            return GetContractNameForAddress(CurrentCodeAddress);
        }

        public CallStepInfo GetCallInfoForStep(int stepIndex)
        {
            if (!IsWithinTrace(stepIndex))
                return null;

            var trace = Trace[stepIndex];
            var opcode = trace.Instruction?.Instruction;

            if (!IsCallOpcode(opcode))
                return null;

            if (trace.Stack == null || trace.Stack.Count < 4)
                return null;

            var argsOffsetIndex = opcode == Instruction.CALL ? 3 : 2;
            var argsLengthIndex = opcode == Instruction.CALL ? 4 : 3;

            if (trace.Stack.Count <= argsLengthIndex)
                return null;

            var targetAddress = ReadCallTargetAddressFromStack(trace.Stack);

            var result = new CallStepInfo
            {
                TargetAddress = targetAddress,
                ContractName = GetContractNameForAddress(targetAddress),
                CallType = opcode.Value.ToString()
            };

            DescribeCalldataHeldInMemory(result, trace, argsOffsetIndex, argsLengthIndex);

            return result;
        }

        private static bool IsCallOpcode(Instruction? opcode) =>
            opcode == Instruction.CALL || opcode == Instruction.STATICCALL ||
            opcode == Instruction.DELEGATECALL || opcode == Instruction.CALLCODE;

        private static string ReadCallTargetAddressFromStack(List<string> stack)
        {
            var rawTo = stack[CallTargetStackIndex].PadLeft(40, '0');
            return "0x" + rawTo.Substring(rawTo.Length - 40);
        }

        private void DescribeCalldataHeldInMemory(CallStepInfo result, ProgramTrace trace, int argsOffsetIndex, int argsLengthIndex)
        {
            if (string.IsNullOrEmpty(trace.Memory))
                return;

            try
            {
                var argsOffset = ParseHexToInt(trace.Stack[argsOffsetIndex]);
                var argsLength = ParseHexToInt(trace.Stack[argsLengthIndex]);

                if (MemoryHoldsCalldataWithSelector(argsOffset, argsLength, trace.Memory))
                {
                    var calldata = trace.Memory.Substring(argsOffset * 2, argsLength * 2);
                    result.Selector = "0x" + calldata.Substring(0, 8);
                    result.RawCalldata = calldata;

                    DescribeCalledFunction(result, calldata);
                }
            }
            catch
            {
            }
        }

        private static bool MemoryHoldsCalldataWithSelector(int argsOffset, int argsLength, string memory) =>
            argsLength >= 4 && argsOffset * 2 + argsLength * 2 <= memory.Length;

        private void DescribeCalledFunction(CallStepInfo result, string calldata)
        {
            var abiInfo = GetABIInfoForAddress(result.TargetAddress);
            if (abiInfo == null)
                return;

            abiInfo.InitialiseContractABI();
            if (abiInfo.ContractABI == null)
                return;

            var funcAbi = abiInfo.ContractABI.FindFunctionABIFromInputData("0x" + calldata);
            if (funcAbi == null)
                return;

            result.FunctionName = funcAbi.Name;
            result.FunctionSignature = funcAbi.Signature;

            if (funcAbi.InputParameters == null || funcAbi.InputParameters.Length == 0)
                return;

            try
            {
                var decoder = new FunctionCallDecoder();
                result.DecodedInputs = decoder.DecodeInput(funcAbi, "0x" + calldata);
            }
            catch { }
        }

        private string FindDispatchedFunctionNameAtOrBefore(int stepIndex)
        {
            var trace = Trace[stepIndex];
            var addr = trace.CodeAddress?.ToLowerInvariant();
            if (addr == null || !_functionMaps.TryGetValue(addr, out var funcMap))
                return null;

            var depth = trace.Depth;
            for (int i = stepIndex; i >= 0; i--)
            {
                var t = Trace[i];
                if (t.CodeAddress?.ToLowerInvariant() != addr) break;
                if (t.Depth != depth) break;

                var pc = t.Instruction?.Step ?? -1;
                if (funcMap.TryGetValue(pc, out var name))
                    return name;
            }

            return null;
        }

        private static int ParseHexToInt(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return 0;
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);
            if (hex.Length == 0)
                return 0;
            if (hex.Length > 8)
                hex = hex.Substring(hex.Length - 8);
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
            return 0;
        }
    }
}
