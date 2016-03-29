using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;

namespace Nethereum.JsonRpc.Client
{
    public class RpcRequestResponseHandler<TResponse>: IRpcRequestHandler
    {
        public string MethodName { get; }

        public IClient Client { get; }
        public RpcRequestResponseHandler(IClient client, string methodName)
        {
            this.MethodName = methodName;
            this.Client = client;
        }

        public async Task<TResponse> SendRequestAsync(object id, params object[] paramList)
        {
            var request = BuildRequest(id, paramList);
            var response = await Client.SendRequestAsync(request);
            if (response.HasError) throw new RpcResponseException(response);
            return response.GetResult<TResponse>();
        }

        public RpcRequest BuildRequest(object id,  params object[] paramList)
        {
            if (id == null) id = Configuration.DefaultRequestId;
         
            return new RpcRequest(id, MethodName, paramList);
        }
    }
}
