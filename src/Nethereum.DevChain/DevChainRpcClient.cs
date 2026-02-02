using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.DevChain
{
    /// <summary>
    /// RPC client adapter that wraps a DevChain RpcDispatcher as an IClient.
    /// This allows using DevChainNode with standard Web3 and contract services.
    ///
    /// Usage:
    /// <code>
    /// var node = new DevChainNode(config);
    /// await node.StartAsync(prefundedAddresses, initialBalance);
    ///
    /// var registry = new RpcHandlerRegistry();
    /// registry.AddStandardHandlers();
    ///
    /// var context = new RpcContext(node, chainId, serviceProvider);
    /// var dispatcher = new RpcDispatcher(registry, context);
    ///
    /// var rpcClient = new DevChainRpcClient(dispatcher);
    /// var web3 = new Web3(account, rpcClient);
    /// </code>
    /// </summary>
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
