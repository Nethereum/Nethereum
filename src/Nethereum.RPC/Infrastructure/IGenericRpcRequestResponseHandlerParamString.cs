using Nethereum.JsonRpc.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.RPC.Infrastructure
{
    public interface IGenericRpcRequestResponseHandlerParamString<T>
    {
        Task<T> SendRequestAsync(string str, object id = null);
        RpcRequest BuildRequest(string str, object id = null);
    }
}
