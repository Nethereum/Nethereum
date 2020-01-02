using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.GSN.DTOs
{
    [Function("version", "string")]
    public class VersionFunction : FunctionMessage
    {
    }
}
