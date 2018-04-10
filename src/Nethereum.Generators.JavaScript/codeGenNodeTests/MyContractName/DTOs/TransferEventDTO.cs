using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
namespace StandardToken.MyContractName.DTOs
{
    [Event("Transfer")]
    public class TransferEventDTO
    {
        [Parameter("address", "_from", 1, true )]
        public string From {get; set;}
        [Parameter("address", "_to", 2, true )]
        public string To {get; set;}
        [Parameter("uint256", "_value", 3, false )]
        public BigInteger Value {get; set;}
    }
}
