using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.EVM.Decoding
{
    public class DecodedLog
    {
        public string ContractAddress { get; set; }
        public string ContractName { get; set; }
        public EventABI Event { get; set; }
        public List<ParameterOutput> Parameters { get; set; } = new List<ParameterOutput>();
        public bool IsDecoded { get; set; }
        public FilterLog OriginalLog { get; set; }
        public int LogIndex { get; set; }
        public int CallDepth { get; set; }

        public string GetEventSignature()
        {
            if (Event == null) return null;
            return Event.Sha3Signature;
        }

        public string GetEventName()
        {
            if (Event == null) return null;
            return Event.Name;
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(ContractName) && Event != null)
            {
                return $"{ContractName}.{Event.Name}";
            }
            if (Event != null)
            {
                return Event.Name;
            }
            return "UnknownEvent";
        }
    }
}
