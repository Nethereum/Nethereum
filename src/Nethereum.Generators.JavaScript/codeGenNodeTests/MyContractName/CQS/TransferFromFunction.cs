using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts.CQS;
using Nethereum.ABI.FunctionEncoding.Attributes;
using StandardToken.MyContractName.DTOs;
namespace StandardToken.MyContractName.CQS
{
    [Function("transferFrom", "bool")]
    public class TransferFromFunction:ContractMessage
    {
        [Parameter("address", "_from", 1)]
        public string From {get; set;}
        [Parameter("address", "_to", 2)]
        public string To {get; set;}
        [Parameter("uint256", "_value", 3)]
        public BigInteger Value {get; set;}
    }
}
