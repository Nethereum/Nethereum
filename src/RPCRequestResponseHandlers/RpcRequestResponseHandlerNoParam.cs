using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPCRequestResponseHandlers
{
 

    public class RpcRequestResponseHandlerNoParam<TResponse>
    {
        public string MethodName { get; }
        public RpcClient Client { get; }
        public RpcRequestResponseHandlerNoParam(RpcClient client, string methodName)
        {
            this.MethodName = methodName;
            this.Client = client;
        }

        public virtual async Task<TResponse> SendRequestAsync(string id)
        {
            var response = await Client.SendRequestAsync(BuildRequest(id));
            if (response.HasError) throw new RPCResponseException(response);
            return response.GetResult<TResponse>();
        }
        public RpcRequest BuildRequest(string id)
        {
            return new RpcRequest(id, MethodName, (object)null);
        }
    }

}
