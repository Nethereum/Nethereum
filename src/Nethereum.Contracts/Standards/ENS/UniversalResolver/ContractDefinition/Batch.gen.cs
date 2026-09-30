using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.UniversalResolver.ContractDefinition
{
    public partial class Batch : BatchBase { }

    public class BatchBase 
    {
        [Parameter("tuple[]", "lookups", 1)]
        public virtual List<Lookup> Lookups { get; set; }
        [Parameter("string[]", "gateways", 2)]
        public virtual List<string> Gateways { get; set; }
    }
}
