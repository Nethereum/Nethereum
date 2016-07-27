using System.Threading.Tasks;
using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseHandlerNoParam<TResponse>: IRpcRequestHandler
    {
        public string MethodName { get; }
        public IClient Client { get; }

        public RpcRequestResponseHandlerNoParam(IClient client, string methodName)
        {
            this.MethodName = methodName;
            this.Client = client;
        }

        public virtual async Task<TResponse> SendRequestAsync(object id)
        {
            var response = await Client.SendRequestAsync(BuildRequest(id)).ConfigureAwait(false);
            if (response.HasError) throw new RpcResponseException(response);
            return response.GetResult<TResponse>();
        }
        public RpcRequest BuildRequest(object id)
        {
            if (id == null) id = Configuration.DefaultRequestId;
        
            return new RpcRequest(id, MethodName);
        }
    }

}
