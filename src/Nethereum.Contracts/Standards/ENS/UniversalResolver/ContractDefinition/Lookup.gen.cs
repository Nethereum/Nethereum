using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.Standards.ENS.UniversalResolver.ContractDefinition
{
    public partial class Lookup : LookupBase { }

    public class LookupBase 
    {
        [Parameter("address", "target", 1)]
        public virtual string Target { get; set; }
        [Parameter("bytes", "call", 2)]
        public virtual byte[] Call { get; set; }
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }
        [Parameter("uint256", "flags", 4)]
        public virtual BigInteger Flags { get; set; }
    }
}
