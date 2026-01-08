using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.Decoding
{
    public enum CallType
    {
        Call,
        DelegateCall,
        StaticCall,
        CallCode,
        Create,
        Create2
    }

    public class DecodedCall
    {
        public string From { get; set; }
        public string To { get; set; }
        public string ContractName { get; set; }
        public FunctionABI Function { get; set; }
        public List<ParameterOutput> InputParameters { get; set; } = new List<ParameterOutput>();
        public List<ParameterOutput> OutputParameters { get; set; } = new List<ParameterOutput>();
        public List<DecodedCall> InnerCalls { get; set; } = new List<DecodedCall>();
        public List<DecodedLog> Logs { get; set; } = new List<DecodedLog>();
        public CallType CallType { get; set; } = CallType.Call;
        public int Depth { get; set; }
        public bool IsDecoded { get; set; }
        public string RawInput { get; set; }
        public string RawOutput { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger GasUsed { get; set; }
        public bool IsRevert { get; set; }
        public DecodedError Error { get; set; }
        public CallInput OriginalCall { get; set; }

        public string GetFunctionSignature()
        {
            if (Function == null) return null;
            return Function.Sha3Signature;
        }

        public string GetFunctionName()
        {
            if (Function == null) return null;
            return Function.Name;
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(ContractName) && Function != null)
            {
                return $"{ContractName}.{Function.Name}";
            }
            if (Function != null)
            {
                return Function.Name;
            }
            if (!string.IsNullOrEmpty(To))
            {
                return $"call({To.Substring(0, 10)}...)";
            }
            return "unknown";
        }
    }
}
