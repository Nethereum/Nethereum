using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.CQS;

namespace Nethereum.StandardTokenEIP20.CQS
{
    [Function("symbol", "bytes32")]
    public class SymbolFunction:ContractMessage
    {

    }
}
