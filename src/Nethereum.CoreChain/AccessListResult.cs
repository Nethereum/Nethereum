using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class AccessListResult
    {
        public List<AccessListItem> AccessList { get; set; } = new();
        public BigInteger GasUsed { get; set; }
        public string Error { get; set; }
    }
}
