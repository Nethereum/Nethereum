using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionByHashHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionByHash.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var hash = hashHex.HexToByteArray();

            var signedTx = await context.Node.GetTransactionByHashAsync(hash);
            if (signedTx == null)
            {
                return Success(request.Id, null);
            }

            var location = await context.Node.Transactions.GetLocationAsync(hash);
            var blockHeader = location != null
                ? await context.Node.GetBlockByHashAsync(location.BlockHash)
                : null;

            var transaction = TransactionRpcBuilder.Build(
                signedTx,
                location?.BlockHash,
                location?.BlockNumber,
                location?.TransactionIndex,
                blockHeader);

            return Success(request.Id, transaction);
        }
    }
}
