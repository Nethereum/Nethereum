using System.Threading.Tasks;
using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;

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


        protected async Task<TResponse> SendRequestAsync(object id, params object[] paramList)
        {
            var request = BuildRequest(id, paramList);
            var response = await Client.SendRequestAsync(request).ConfigureAwait(false);
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
