using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace Nethereum.StandardTokenEIP20.CQS
{
    [Function("transferFrom", "bool")]
    public class TransferFromFunction:FunctionMessage
    {
        [Parameter("address", "_from", 1)]
        public string From {get; set;}
        [Parameter("address", "_to", 2)]
        public string To {get; set;}
        [Parameter("uint256", "_value", 3)]
        public BigInteger Value {get; set;}
    }
}
