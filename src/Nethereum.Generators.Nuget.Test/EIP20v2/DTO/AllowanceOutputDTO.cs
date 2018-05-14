using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [FunctionOutput]
    public class AllowanceOutputDTO
    {
        [Parameter("uint256", "remaining", 1)]
        public BigInteger Remaining {get; set;}
    }
}
