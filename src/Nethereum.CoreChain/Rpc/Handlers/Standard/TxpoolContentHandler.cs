using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.TxPool.DTOs;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class TxpoolContentHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.txpool_content.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var pending = await context.Node.GetPendingTransactionsAsync();
            var entries = PendingTransactionInfoBuilder.Build(pending);

            var response = new TxPoolContentResponse
            {
                Pending = new Dictionary<string, Dictionary<string, PendingTransactionInfo>>(),
                Queued = new Dictionary<string, Dictionary<string, PendingTransactionInfo>>()
            };

            foreach (var entry in entries)
            {
                if (!response.Pending.TryGetValue(entry.From, out var byNonce))
                {
                    byNonce = new Dictionary<string, PendingTransactionInfo>();
                    response.Pending[entry.From] = byNonce;
                }

                byNonce[entry.Nonce] = entry.Info;
            }

            return Success(request.Id, response);
        }
    }
}
