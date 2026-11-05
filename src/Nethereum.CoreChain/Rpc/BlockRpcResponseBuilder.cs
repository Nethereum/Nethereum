using System.Linq;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.CoreChain.Rpc
{
    internal static class BlockRpcResponseBuilder
    {
        internal static async Task<Block> BuildAsync(
            RpcContext context, BlockHeader blockHeader, byte[] blockHash, bool includeTransactions)
        {
            var txStore = context.Node.Transactions;
            var signedTxs = await txStore.GetByBlockHashAsync(blockHash);
            var withdrawals = await WithdrawalsLoader.LoadAsync(context, blockHeader, blockHash);
            var uncles = await UnclesLoader.LoadAsync(context, blockHash);
            var blockSize = BlockHeaderExtensions.CalculateFullBlockSize(blockHeader, signedTxs, uncles, withdrawals);

            if (includeTransactions)
            {
                var transactions = signedTxs?
                    .Select((tx, index) => TransactionRpcBuilder.Build(tx, blockHash, blockHeader.BlockNumber, index, blockHeader))
                    .ToArray();
                return blockHeader.ToBlockWithTransactions(blockHash, transactions, blockSize, withdrawals, uncles);
            }

            var txHashes = signedTxs?
                .Select(tx => tx.Hash?.ToHex(true))
                .Where(h => h != null)
                .ToArray();
            return blockHeader.ToBlockWithTransactionHashes(blockHash, txHashes, blockSize, withdrawals, uncles);
        }
    }
}
