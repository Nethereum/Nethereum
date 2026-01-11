using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetBlockTransactionCountByHashHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getBlockTransactionCountByHash.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var blockHashHex = GetParam<string>(request, 0);
            var blockHash = blockHashHex.HexToByteArray();

            var transactions = await context.Node.Transactions.GetByBlockHashAsync(blockHash);
            if (transactions == null)
            {
                return Success(request.Id, null);
            }

            return Success(request.Id, new HexBigInteger(transactions.Count));
        }
    }
}
