using System;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcException : Exception
    {
        public int Code { get; }
        public object Data { get; }

        public RpcException(int code, string message, object data = null)
            : base(message)
        {
            Code = code;
            Data = data;
        }

        public static RpcException InvalidParams(string message) => new RpcException(-32602, message);
        public static RpcException InternalError(string message) => new RpcException(-32603, message);
        public static RpcException MethodNotFound(string method) => new RpcException(-32601, $"Method not found: {method}");
        public static RpcException ExecutionError(string message, object data = null) => new RpcException(3, message, data);
    }
}
