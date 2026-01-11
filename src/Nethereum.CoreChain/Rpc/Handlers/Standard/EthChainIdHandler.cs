using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthChainIdHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_chainId.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            return Task.FromResult(Success(request.Id, new HexBigInteger(context.ChainId)));
        }
    }
}
