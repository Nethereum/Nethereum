using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    public sealed class FinalityLabelledEthGetBlockByNumberHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockByNumber.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockTag = GetParam<string>(request, 0);
            var includeTransactions = GetOptionalParam<bool>(request, 1, false);

            var blockNumber = await ResolveLabelAsync(blockTag, context);

            var blockHeader = await context.Node.GetBlockByNumberAsync(blockNumber);
            if (blockHeader == null)
            {
                return Success(request.Id, null);
            }

            var blockHash = await context.Node.GetBlockHashByNumberAsync(blockNumber);

            var txStore = context.Node.Transactions;
            var signedTxs = txStore != null ? await txStore.GetByBlockHashAsync(blockHash) : null;
            var uncles = context.Node.Uncles != null
                ? await context.Node.Uncles.GetByBlockHashAsync(blockHash)
                : null;
            var withdrawals = await WithdrawalsLoader.LoadAsync(context, blockHeader, blockHash);

            var fullBlockSize = Nethereum.CoreChain.Rpc.BlockHeaderExtensions.CalculateFullBlockSize(
                blockHeader, signedTxs, uncles, withdrawals);

            var uncleHashes = uncles?
                .Select(u => BlockHashCalculator.ForHeader(u).ToHex(true))
                .ToArray() ?? new string[0];

            if (includeTransactions)
            {
                var transactions = signedTxs?
                    .Select((tx, index) => TransactionRpcBuilder.Build(tx, blockHash, blockNumber, index, blockHeader))
                    .ToArray();
                var block = blockHeader.ToBlockWithTransactions(blockHash, transactions, fullBlockSize, withdrawals);
                block.Uncles = uncleHashes;
                return Success(request.Id, block);
            }
            else
            {
                var txHashes = signedTxs?
                    .Select(tx => tx.Hash?.ToHex(true))
                    .Where(h => h != null)
                    .ToArray() ?? new string[0];
                var block = blockHeader.ToBlockWithTransactionHashes(blockHash, txHashes, fullBlockSize, withdrawals);
                block.Uncles = uncleHashes;
                return Success(request.Id, block);
            }
        }

        internal static async Task<BigInteger> ResolveLabelAsync(string blockTag, RpcContext context)
        {
            var cursor = context.GetService<IFinalityCursorProvider>();
            var latest = await context.Node.GetBlockNumberAsync();
            return ResolveLabel(blockTag, cursor, latest);
        }

        public static BigInteger ResolveLabel(string blockTag, IFinalityCursorProvider? cursor, BigInteger latest)
            => FinalityLabelResolver.Resolve(blockTag, cursor, latest);
    }
}
