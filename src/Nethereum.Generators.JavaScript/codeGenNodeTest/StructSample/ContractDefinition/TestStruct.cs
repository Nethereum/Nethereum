using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Structs.StructSample.ContractDefinition
{
    public partial class TestStruct : TestStructBase { }

    public class TestStructBase 
    {
        [Parameter("uint256", "id", 1)]
        public virtual BigInteger Id { get; set; }
        [Parameter("tuple", "subStruct1", 2)]
        public virtual SubStruct SubStruct1 { get; set; }
        [Parameter("tuple", "subStruct2", 3)]
        public virtual SubStruct SubStruct2 { get; set; }
        [Parameter("string", "id2", 4)]
        public virtual string Id2 { get; set; }
    }
}
