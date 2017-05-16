using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseHandlerNoParam<TResponse> : IRpcRequestHandler
    {
        public RpcRequestResponseHandlerNoParam(IClient client, string methodName)
        {
            MethodName = methodName;
            Client = client;
        }

        public string MethodName { get; }
        public IClient Client { get; }

        public virtual Task<TResponse> SendRequestAsync(object id)
        {
            return Client.SendRequestAsync<TResponse>(BuildRequest(id));
        }

        public RpcRequest BuildRequest(object id = null)
        {
            if (id == null) id = Configuration.DefaultRequestId;

            return new RpcRequest(id, MethodName);
        }
    }
}