using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using StandardToken.MyContractName.DTOs;
namespace StandardToken.MyContractName.CQS
{
    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction:ContractMessage
    {
        [Parameter("address", "_owner", 1)]
        public string Owner {get; set;}
    }
}
