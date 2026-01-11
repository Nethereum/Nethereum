using System.Threading.Tasks;
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

            if (includeTransactions)
            {
                return Success(request.Id, blockHeader.ToBlockWithTransactions(blockHash));
            }
            else
            {
                return Success(request.Id, blockHeader.ToBlockWithTransactionHashes(blockHash));
            }
        }
    }
}
