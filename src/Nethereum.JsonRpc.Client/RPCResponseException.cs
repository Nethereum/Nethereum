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

    public class RpcResponseBatchException : Exception
    {
        public RpcResponseBatchException(RpcError[] rpcErrors) : base($"Rpc Batch Exception,.Number of Errors:{rpcErrors.Length} ")
        {
            RpcErrors = rpcErrors;
        }

        public RpcError[] RpcErrors { get; }
    }

    public class RpcResponseFormatException : Exception
    {
        public RpcResponseFormatException(string message, FormatException innerException)
            : base(message, innerException)
        {
        }
    }
}