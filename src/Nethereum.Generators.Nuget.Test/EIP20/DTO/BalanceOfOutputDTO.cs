using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace Nethereum.Generators.Nuget.Test.EIP20.DTO
{
    [FunctionOutput]
    public class BalanceOfOutputDTO
    {
        [Parameter("uint256", "balance", 1)]
        public BigInteger Balance {get; set;}
    }
}
