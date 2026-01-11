using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class Web3ClientVersionHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.web3_clientVersion.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            return Task.FromResult(Success(request.Id, "Nethereum.DevChain/1.0.0"));
        }
    }
}
