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
        public RpcRequestResponseHandlerNoParam(string methodName)
        {
            this.MethodName = methodName;
        }

        public async Task<TResponse> SendRequestAsync(RpcClient client, string id)
        {
            var response = await client.SendRequestAsync(BuildRequest(id));
            if (response.HasError) throw new RPCResponseException(response);
            return response.GetResult<TResponse>();
        }
        public RpcRequest BuildRequest(string id)
        {
            return new RpcRequest(id, MethodName, (object)null);
        }
    }

}
