using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    public class ProgramResult
    {
        public byte[] Result { get; set;}
        public List<FilterLog> Logs { get; set; } = new List<FilterLog>();
        public bool IsRevert { get; set; }
        
    }
}