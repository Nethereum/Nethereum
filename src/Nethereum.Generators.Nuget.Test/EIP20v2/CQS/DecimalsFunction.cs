using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Generators.Nuget.Test.EIP20v2.DTO;
namespace Nethereum.Generators.Nuget.Test.EIP20v2.CQS
{
    [Function("decimals", "uint8")]
    public class DecimalsFunction:ContractMessage
    {

    }
}
