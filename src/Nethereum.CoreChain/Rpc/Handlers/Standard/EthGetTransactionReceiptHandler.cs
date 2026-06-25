using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionReceiptHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionReceipt.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var hash = hashHex.HexToByteArray();

            var receiptInfo = await context.Node.GetTransactionReceiptInfoAsync(hash);
            if (receiptInfo == null)
            {
                return Success(request.Id, null);
            }

            var tx = await context.Node.GetTransactionByHashAsync(hash);
            var from = tx != null ? GetSenderAddress(tx) : null;
            var to = GetReceiverAddress(tx);

            // LogIndex must be the block-wide cumulative count. For single-receipt
            // lookups we sum logs across earlier txs in the same block by index.
            var startingLogIndex = 0;
            if (tx != null && receiptInfo.TransactionIndex > 0)
            {
                var blockTxs = await context.Node.Transactions.GetByBlockHashAsync(receiptInfo.BlockHash);
                if (blockTxs != null)
                {
                    for (int i = 0; i < (int)receiptInfo.TransactionIndex && i < blockTxs.Count; i++)
                    {
                        var earlier = await context.Node.Receipts.GetInfoByTxHashAsync(blockTxs[i].Hash);
                        startingLogIndex += earlier?.Receipt?.Logs?.Count ?? 0;
                    }
                }
            }

            var txType = tx?.TransactionType ?? Nethereum.Model.TransactionType.LegacyTransaction;
            var receipt = receiptInfo.ToTransactionReceipt(from, to, txType, startingLogIndex);
            return Success(request.Id, receipt);
        }
    }
}
