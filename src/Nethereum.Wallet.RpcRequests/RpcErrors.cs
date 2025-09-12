using Nethereum.JsonRpc.Client.RpcMessages;
using RpcError = Nethereum.JsonRpc.Client.RpcMessages.RpcError;

namespace Nethereum.Wallet.RpcRequests
{
    public static class RpcErrors
    {
        public static RpcResponseMessage MethodNotFound(object id) => new(id, new RpcError { Code = -32601, Message = "Method not found" });
        public static RpcResponseMessage InvalidParams(object id, string? message = null) => new(id, new RpcError { Code = -32602, Message = message ?? "Invalid parameters" });
        public static RpcResponseMessage UserRejected(object id) => new(id, new RpcError { Code = 4001, Message = "User rejected the request" });
        public static RpcResponseMessage InternalError(object id, string? message = null) => new(id, new RpcError { Code = -32603, Message = message ?? "Internal error" });
    }

}
