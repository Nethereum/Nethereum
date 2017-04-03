using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseHandler<TResponse> : IRpcRequestHandler
    {
        public RpcRequestResponseHandler(IClient client, string methodName)
        {
            MethodName = methodName;
            Client = client;
        }

        public string MethodName { get; }

        public IClient Client { get; }

        protected async Task<TResponse> SendRequestAsync(object id, params object[] paramList)
        {
            var request = BuildRequest(id, paramList);
            return await Client.SendRequestAsync<TResponse>(request).ConfigureAwait(false);
        }

        public RpcRequest BuildRequest(object id, params object[] paramList)
        {
            if (id == null) id = Configuration.DefaultRequestId;

            return new RpcRequest(id, MethodName, paramList);
        }
    }
}