using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.GSN.DTOs
{
    [Function("depositFor")]
    public class DepositForFunction : FunctionMessage
    {
        [Parameter("address", "target", 1)]
        public string Target { get; set; }
    }
}
