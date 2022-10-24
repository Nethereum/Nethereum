using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class ProgramResult
    {
        public byte[] Result { get; set;}
        public List<FilterLog> Logs { get; set; }
        public bool IsRevert { get; set; }
        
    }
}