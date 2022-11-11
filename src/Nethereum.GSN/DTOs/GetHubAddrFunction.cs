using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.GSN.DTOs
{
    [Function("getHubAddr", "address")]
    public class GetHubAddrFunction : FunctionMessage
    {
    }
}
