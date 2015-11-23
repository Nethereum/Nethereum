using edjCase.JsonRpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPCRequestResponseHandlers
{
    public class RPCResponseException:Exception
    {
        public RpcResponse RpcResponse { get; }
        public RPCResponseException(RpcResponse rpcResponse):base(rpcResponse.Error.Message)
        {
            this.RpcResponse = rpcResponse;
        }
    }
}
