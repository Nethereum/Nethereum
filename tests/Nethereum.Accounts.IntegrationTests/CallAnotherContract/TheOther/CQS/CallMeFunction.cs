using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using SolidityCallAnotherContract.Contracts.TheOther.DTOs;
namespace SolidityCallAnotherContract.Contracts.TheOther.CQS
{
    [Function("CallMe", "bytes")]
    public class CallMeFunction:FunctionMessage
    {
        [Parameter("string", "name", 1)]
        public string Name {get; set;}
        [Parameter("string", "greeting", 2)]
        public string Greeting {get; set;}
    }
}
