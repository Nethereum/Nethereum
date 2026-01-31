using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthGetTransactionCountHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_getTransactionCount.ToString();

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var address = GetParam<string>(request, 0);
            var blockTag = GetOptionalParam<string>(request, 1, "latest");

            ValidateBlockParameterIsLatest(blockTag, MethodName);

            var nonce = await context.Node.GetNonceAsync(address);
            return Success(request.Id, new HexBigInteger(nonce));
        }
    }
}
