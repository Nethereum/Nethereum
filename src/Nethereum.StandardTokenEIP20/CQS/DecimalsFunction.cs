using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;

namespace Nethereum.StandardTokenEIP20.CQS
{
    [Function("decimals", "uint8")]
    public class DecimalsFunction:FunctionMessage
    {

    }
}
