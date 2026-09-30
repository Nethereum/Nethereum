using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetUncleCountByBlockHashHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getUncleCountByBlockHash.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var hashHex = GetParam<string>(request, 0);
            var hash = hashHex.HexToByteArray();

            var header = await context.Node.GetBlockByHashAsync(hash);
            if (header == null)
            {
                return Success(request.Id, null);
            }

            var uncleStore = context.Node.Uncles;
            var uncles = uncleStore != null ? await uncleStore.GetByBlockHashAsync(hash) : null;
            return Success(request.Id, new HexBigInteger(uncles?.Count ?? 0));
        }
    }
}
