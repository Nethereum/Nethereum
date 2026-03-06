using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockReceiptsHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockReceipts.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);
            if (blockHash == null)
            {
                return Success(request.Id, null);
            }

            var transactions = await context.Node.Transactions.GetByBlockNumberAsync(blockNumber);
            if (transactions == null || transactions.Count == 0)
            {
                return Success(request.Id, new List<object>());
            }

            var result = new List<object>();
            foreach (var tx in transactions)
            {
                var receiptInfo = await context.Node.Receipts.GetInfoByTxHashAsync(tx.Hash);
                if (receiptInfo != null)
                {
                    var from = GetSenderAddress(tx);
                    var to = GetReceiverAddress(tx);
                    result.Add(receiptInfo.ToTransactionReceipt(from, to));
                }
            }

            return Success(request.Id, result);
        }
    }
}
