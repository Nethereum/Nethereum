using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthMiningHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_mining.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            return Task.FromResult(Success(request.Id, true));
        }
    }
}
