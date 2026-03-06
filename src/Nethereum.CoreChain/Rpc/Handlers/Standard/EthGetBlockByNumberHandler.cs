using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;

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

            var txStore = context.GetService<ITransactionStore>();
            if (includeTransactions)
            {
                var signedTxs = txStore != null ? await txStore.GetByBlockHashAsync(blockHash) : null;
                var transactions = signedTxs?
                    .Select((tx, index) => SignedTransactionExtensions.ToRpcTransaction(tx, blockHash, blockNumber, index))
                    .ToArray();
                return Success(request.Id, blockHeader.ToBlockWithTransactions(blockHash, transactions));
            }
            else
            {
                var hashes = txStore != null ? await txStore.GetHashesByBlockHashAsync(blockHash) : null;
                var txHashes = hashes?
                    .Select(h => h?.ToHex(true))
                    .Where(h => h != null)
                    .ToArray();
                return Success(request.Id, blockHeader.ToBlockWithTransactionHashes(blockHash, txHashes));
            }
        }
    }
}
