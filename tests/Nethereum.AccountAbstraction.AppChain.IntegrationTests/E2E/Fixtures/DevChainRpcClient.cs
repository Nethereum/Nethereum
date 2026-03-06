using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Fixtures
{
    public class DevChainRpcClient : ClientBase
    {
        private readonly RpcDispatcher _dispatcher;

        public DevChainRpcClient(RpcDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
        {
            return await _dispatcher.DispatchAsync(rpcRequestMessage);
        }

        protected override async Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            var responses = await _dispatcher.DispatchBatchAsync(requests);
            return responses.ToArray();
        }
    }
}
