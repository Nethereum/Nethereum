using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.AppChain.Sequencer;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainRpcClient : ClientBase
    {
        private readonly RpcDispatcher _dispatcher;
        private readonly JsonSerializerOptions _serializerOptions;

        public AppChainRpcClient(AppChainNode node, long chainId)
        {
            var registry = new RpcHandlerRegistry();
            registry.AddStandardHandlers();

            var services = new ServiceCollection().BuildServiceProvider();
            var context = new RpcContext(node, chainId, services);

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = CoreChainJsonContext.Default
            };

            _dispatcher = new RpcDispatcher(registry, context, null, _serializerOptions);
        }

        public override T DecodeResult<T>(RpcResponseMessage rpcResponseMessage)
        {
            return rpcResponseMessage.GetResultSTJ<T>(true, _serializerOptions);
        }

        public override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null)
        {
            return await _dispatcher.DispatchAsync(rpcRequestMessage);
        }

        protected override async Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            var responses = new RpcResponseMessage[requests.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                responses[i] = await _dispatcher.DispatchAsync(requests[i]);
            }
            return responses;
        }
    }
}
