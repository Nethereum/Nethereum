using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;

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
            var response = await Client.SendRequestAsync(BuildRequest(id));
            if (response.HasError) throw new RpcResponseException(response);
            return response.GetResult<TResponse>();
        }
        public RpcRequest BuildRequest(object id)
        {
            if (id == null) id = Configuration.DefaultRequestId;
        
            return new RpcRequest(id  , MethodName, (object)null);
        }
    }

}
