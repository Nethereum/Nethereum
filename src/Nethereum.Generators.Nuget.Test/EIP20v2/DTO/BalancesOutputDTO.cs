using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.DTO
{
    [FunctionOutput]
    public class BalancesOutputDTO
    {
        [Parameter("uint256", "", 1)]
        public BigInteger ReturnValue1 {get; set;}
    }
}
