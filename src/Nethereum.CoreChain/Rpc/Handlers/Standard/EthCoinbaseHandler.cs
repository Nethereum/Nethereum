using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;
using Nethereum.Util;

namespace Nethereum.CoreChain.Rpc.Handlers.Standard
{
    public class EthCoinbaseHandler : RpcHandlerBase
    {
        public override string MethodName => ApiMethods.eth_coinbase.ToString();

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            var coinbase = context.Node.Config.Coinbase ?? AddressUtil.ZERO_ADDRESS;
            return Task.FromResult(Success(request.Id, coinbase));
        }
    }
}
