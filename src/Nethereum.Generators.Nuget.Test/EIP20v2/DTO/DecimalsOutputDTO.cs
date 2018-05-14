using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [FunctionOutput]
    public class DecimalsOutputDTO
    {
        [Parameter("uint8", "", 1)]
        public byte ReturnValue1 {get; set;}
    }
}
