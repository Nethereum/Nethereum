using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.GSN.DTOs
{
    [Function("getNonce", "uint256")]
    public class GetNonceFunction : FunctionMessage
    {
        [Parameter("address", "from", 1)]
        public string From { get; set; }
    }
}
