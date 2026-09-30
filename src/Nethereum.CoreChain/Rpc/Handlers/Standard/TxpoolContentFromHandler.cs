using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class TxpoolContentFromHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.txpool_contentFrom.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0).ToLowerInvariant();

            var pending = await context.Node.GetPendingTransactionsAsync();
            var entries = PendingTransactionInfoBuilder.Build(pending)
                .Where(entry => entry.From == address);

            var response = new TxPoolContentFromResponse
            {
                Pending = new Dictionary<string, PendingTransactionInfo>(),
                Queued = new Dictionary<string, PendingTransactionInfo>()
            };

            foreach (var entry in entries)
            {
                response.Pending[entry.Nonce] = entry.Info;
            }

            return Success(request.Id, response);
        }
    }
}
