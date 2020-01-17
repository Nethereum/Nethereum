using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Structs.StructSample.ContractDefinition
{
    public partial class SubStruct : SubStructBase { }

    public class SubStructBase 
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("tuple", "sub", 2)]
        public virtual SubSubStruct Sub { get; set; }
        [Parameter("string", "id2", 3)]
        public virtual string Id2 { get; set; }
    }
}
