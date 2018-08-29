using System;

namespace Nethereum.JsonRpc.Client
{
    public class RpcResponseException : Exception
    {
        public RpcResponseException(RpcError rpcError) : base(rpcError.Message)
        {
            RpcError = rpcError;
        }

        public RpcError RpcError { get; }
    }

    public class RpcResponseFormatException : Exception
    {
        public RpcResponseFormatException(string message, FormatException innerException)
            : base(message, innerException)
        {
        }
    }
}