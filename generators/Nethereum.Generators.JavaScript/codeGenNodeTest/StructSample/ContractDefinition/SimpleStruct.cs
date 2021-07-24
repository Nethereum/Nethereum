using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Structs.StructSample.ContractDefinition
{
    public partial class SimpleStruct : SimpleStructBase { }

    public class SimpleStructBase 
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("uint256", "id2", 2)]
        public virtual BigInteger Id2 { get; set; }
    }
}
