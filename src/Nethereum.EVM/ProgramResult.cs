using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class ProgramResult
    {
        public byte[] Result { get; set;}
        public List<FilterLog> Logs { get; set; } = new List<FilterLog>();
        public bool IsRevert { get; set; }
        public bool IsSelfDestruct { get; set; }
        public List<string> DeletedContractAccounts { get; set; } = new List<string>();
        public List<string> CreatedContractAccounts { get; set; } = new List<string>();
        public List<CallInput> InnerCalls { get; set; } = new List<CallInput>();
        
    }
}