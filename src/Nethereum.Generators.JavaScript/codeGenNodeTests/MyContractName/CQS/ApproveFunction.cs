using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using StandardToken.MyContractName.DTOs;
namespace StandardToken.MyContractName.CQS
{
    [Function("approve", "bool")]
    public class ApproveFunction:ContractMessage
    {
        [Parameter("address", "_spender", 1)]
        public string Spender {get; set;}
        [Parameter("uint256", "_value", 2)]
        public BigInteger Value {get; set;}
    }
}
