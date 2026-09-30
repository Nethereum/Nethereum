using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
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
            var from = tx != null ? RpcTransactionAddress.ResolveSender(tx) : null;
            var to = RpcTransactionAddress.ResolveReceiver(tx);

            var startingLogIndex = 0;
            if (tx != null && receiptInfo.TransactionIndex > 0)
            {
                var blockTxs = await context.Node.Transactions.GetByBlockHashAsync(receiptInfo.BlockHash);
                if (blockTxs != null)
                {
                    var precedingTxCount = System.Math.Min((int)receiptInfo.TransactionIndex, blockTxs.Count);
                    var precedingReceipts = new List<ReceiptInfo>(precedingTxCount);
                    for (int i = 0; i < precedingTxCount; i++)
                    {
                        precedingReceipts.Add(await context.Node.Receipts.GetInfoByTxHashAsync(blockTxs[i].Hash));
                    }
                    startingLogIndex = ReceiptExtensions.ComputeStartingLogIndex(precedingReceipts, precedingReceipts.Count);
                }
            }

            var blockHeader = await context.Node.GetBlockByHashAsync(receiptInfo.BlockHash);
            var (blobGasUsed, blobGasPrice) = ReceiptBlobGas.Resolve(tx, blockHeader, context.Node.Config);

            var txType = tx?.TransactionType ?? Nethereum.Model.TransactionType.LegacyTransaction;
            var receipt = receiptInfo.ToTransactionReceipt(from, to, txType, startingLogIndex, blobGasUsed, blobGasPrice, blockHeader?.Timestamp);
            return Success(request.Id, receipt);
        }
    }
}
