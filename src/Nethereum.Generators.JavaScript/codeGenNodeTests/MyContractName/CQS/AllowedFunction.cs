using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using StandardToken.MyContractName.DTOs;
namespace StandardToken.MyContractName.CQS
{
    [Function("allowed", "uint256")]
    public class AllowedFunction:ContractMessage
    {
        [Parameter("address", "", 1)]
        public string B {get; set;}
        [Parameter("address", "", 2)]
        public string C {get; set;}
    }
}
