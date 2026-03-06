using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockByHashHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockByHash.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var includeTransactions = GetOptionalParam<bool>(request, 1, false);

            var blockHash = hashHex.HexToByteArray();
            var blockHeader = await context.Node.GetBlockByHashAsync(blockHash);

            if (blockHeader == null)
            {
                return Success(request.Id, null);
            }

            var txStore = context.GetService<ITransactionStore>();
            if (includeTransactions)
            {
                var signedTxs = txStore != null ? await txStore.GetByBlockHashAsync(blockHash) : null;
                var transactions = signedTxs?
                    .Select((tx, index) => SignedTransactionExtensions.ToRpcTransaction(tx, blockHash, blockHeader.BlockNumber, index))
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
