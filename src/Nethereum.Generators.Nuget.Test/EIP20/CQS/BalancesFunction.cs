using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Generators.Nuget.Test.EIP20.DTO;
namespace Nethereum.Generators.Nuget.Test.EIP20.CQS
{
    [Function("balances", "uint256")]
    public class BalancesFunction:ContractMessage
    {
        [Parameter("address", "", 1)]
        public string ReturnValue1 {get; set;}
    }
}
