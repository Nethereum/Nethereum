using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures
{
    public class EstimateGasRejectingRpcClient : ClientBase
    {
        private readonly IClient _innerClient;

        public EstimateGasRejectingRpcClient(IClient innerClient)
        {
            _innerClient = innerClient;
        }

        public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
        {
            if (rpcRequestMessage.Method == ApiMethods.eth_estimateGas.ToString())
                throw new InvalidOperationException(
                    "eth_estimateGas was sent to the node; AA gas estimation must go to the bundler");

            return _innerClient.SendAsync(rpcRequestMessage, route);
        }

        protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            throw new NotSupportedException("Batch requests are not used in these tests");
        }
    }
}
