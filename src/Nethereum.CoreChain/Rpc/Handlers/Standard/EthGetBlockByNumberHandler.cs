using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockByNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var includeTransactions = GetOptionalParam<bool>(request, 1, false);

            var blockNumber = await ResolveBlockNumberAsync(blockTag, context);

            var blockHeader = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (blockHeader == null)
            {
                return Success(request.Id, null);
            }

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);

            var txStore = context.Node.Transactions;
            var signedTxs = await txStore.GetByBlockHashAsync(blockHash);
            var withdrawals = await WithdrawalsLoader.LoadAsync(context, blockHeader, blockHash);
            var uncles = await UnclesLoader.LoadAsync(context, blockHash);
            var blockSize = BlockHeaderExtensions.CalculateFullBlockSize(blockHeader, signedTxs, uncles, withdrawals);

            if (includeTransactions)
            {
                var transactions = signedTxs?
                    .Select((tx, index) => TransactionRpcBuilder.Build(tx, blockHash, blockNumber, index, blockHeader))
                    .ToArray();
                return Success(request.Id, blockHeader.ToBlockWithTransactions(blockHash, transactions, blockSize, withdrawals, uncles));
            }
            else
            {
                var txHashes = signedTxs?
                    .Select(tx => tx.Hash?.ToHex(true))
                    .Where(h => h != null)
                    .ToArray();
                return Success(request.Id, blockHeader.ToBlockWithTransactionHashes(blockHash, txHashes, blockSize, withdrawals, uncles));
            }
        }
    }
}
