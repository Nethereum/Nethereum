using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Infrastructure
{
    public class GenericRpcRequestResponseHandlerParamString<T> : RpcRequestResponseHandler<T>, IGenericRpcRequestResponseHandlerParamString<T>
    {
        public GenericRpcRequestResponseHandlerParamString(IClient client, string methodName) : base(client, methodName)
        {
        }

        public RpcRequest BuildRequest(string str, object id = null)
        {
            if (string.IsNullOrEmpty(str)) throw new ArgumentNullException(nameof(str));
            return base.BuildRequest(id, str);
        }

        public Task<T> SendRequestAsync(string str, object id = null)
        {
            if (string.IsNullOrEmpty(str)) throw new ArgumentNullException(nameof(str));
            return base.SendRequestAsync(id, str);
        }
    }
}
