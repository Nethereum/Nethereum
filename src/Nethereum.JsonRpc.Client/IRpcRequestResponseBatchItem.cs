using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{
    public interface IRpcRequestResponseBatchItem
    {
        bool HasError { get; }
        object RawResponse { get; }
        RpcError RpcError { get; }
        RpcRequestMessage RpcRequestMessage { get; }
        void DecodeResponse(RpcResponseMessage rpcResponse);
    }
}