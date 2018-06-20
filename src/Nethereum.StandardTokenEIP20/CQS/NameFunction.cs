using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;

namespace Nethereum.StandardTokenEIP20.CQS
{
    [Function("name", "bytes32")]
    public class NameFunction:ContractMessage
    {

    }
}
