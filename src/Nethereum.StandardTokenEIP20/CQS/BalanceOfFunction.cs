using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace Nethereum.StandardTokenEIP20.CQS
{
    [Function("balanceOf", "uint256")]
    public class BalanceOfFunction:FunctionMessage
    {
        [Parameter("address", "_owner", 1)]
        public string Owner {get; set;}
    }
}
