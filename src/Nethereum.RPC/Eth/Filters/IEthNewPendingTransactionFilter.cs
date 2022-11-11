using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth.Filters
{
    public interface IEthNewPendingTransactionFilter : IGenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {

    }
}