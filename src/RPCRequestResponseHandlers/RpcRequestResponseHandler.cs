using edjCase.JsonRpc.Client;
using edjCase.JsonRpc.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPCRequestResponseHandlers
{
    public class RpcRequestResponseHandler<TResponse>: IRpcRequestHandler
    {
        public string MethodName { get; }

        public RpcClient Client { get; }
        public RpcRequestResponseHandler(RpcClient client, string methodName)
        {
            this.MethodName = methodName;
            this.Client = client;
        }

        public async Task<TResponse> SendRequestAsync(object id, params object[] paramList)
        {
            var request = BuildRequest(id, paramList);
            var response = await Client.SendRequestAsync(request);
            if (response.HasError) throw new RPCResponseException(response);
            return response.GetResult<TResponse>();
        }

        public RpcRequest BuildRequest(object id,  params object[] paramList)
        {
            if (id == null) id = Configuration.DefaultRequestId;
         
            return new RpcRequest(id, MethodName, paramList);
        }
    }
}
