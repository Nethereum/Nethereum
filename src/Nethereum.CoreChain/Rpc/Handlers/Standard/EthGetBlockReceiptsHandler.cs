using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
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

            System.Numerics.BigInteger blockNumber;
            if (BlockTagResolver.IsBlockHash(blockTag))
            {
                // EIP-1898: a 32-byte parameter is a block hash. An unknown block hash is null, not [].
                var byHash = await BlockTagResolver.TryResolveBlockHashAsync(blockTag, context);
                if (!byHash.HasValue)
                {
                    return Success(request.Id, null);
                }
                blockNumber = byHash.Value;
            }
            else
            {
                blockNumber = await ResolveBlockNumberAsync(blockTag, context);
            }

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

            var header = await context.Node.GetBlockByNumberAsync(blockNumber);

            var receiptInfos = new List<ReceiptInfo>(transactions.Count);
            foreach (var tx in transactions)
            {
                receiptInfos.Add(await context.Node.Receipts.GetInfoByTxHashAsync(tx.Hash));
            }

            var result = new List<object>();
            for (int i = 0; i < transactions.Count; i++)
            {
                var receiptInfo = receiptInfos[i];
                if (receiptInfo != null)
                {
                    var tx = transactions[i];
                    var from = RpcTransactionAddress.ResolveSender(tx);
                    var to = RpcTransactionAddress.ResolveReceiver(tx);
                    var (blobGasUsed, blobGasPrice) = ReceiptBlobGas.Resolve(tx, header, context.Node.Config);
                    var startingLogIndex = ReceiptExtensions.ComputeStartingLogIndex(receiptInfos, i);
                    result.Add(receiptInfo.ToTransactionReceipt(from, to, tx.TransactionType, startingLogIndex, blobGasUsed, blobGasPrice, header?.Timestamp));
                }
            }

            return Success(request.Id, result);
        }
    }
}
