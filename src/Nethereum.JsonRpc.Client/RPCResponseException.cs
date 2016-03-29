using System;
using edjCase.JsonRpc.Core;

namespace Nethereum.JsonRpc.Client
{
    public class RpcResponseException:Exception
    {
        public RpcResponse RpcResponse { get; }
        public RpcResponseException(RpcResponse rpcResponse):base(rpcResponse.Error.Message)
        {
            this.RpcResponse = rpcResponse;
        }
    }
}
