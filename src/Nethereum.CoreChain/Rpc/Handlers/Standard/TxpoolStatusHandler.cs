using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class TxpoolStatusHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.txpool_status.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var pending = await context.Node.GetPendingTransactionsAsync();

            var response = new TxPoolStatusResponse
            {
                Pending = new HexBigInteger(pending.Count),
                Queued = new HexBigInteger(0)
            };

            return Success(request.Id, response);
        }
    }
}
