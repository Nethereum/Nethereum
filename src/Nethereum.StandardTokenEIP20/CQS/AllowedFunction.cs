using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace Nethereum.StandardTokenEIP20.CQS
{
    [Function("allowed", "uint256")]
    public class AllowedFunction:FunctionMessage
    {
        [Parameter("address", "", 1)]
        public string B {get; set;}
        [Parameter("address", "", 2)]
        public string C {get; set;}
    }
}
