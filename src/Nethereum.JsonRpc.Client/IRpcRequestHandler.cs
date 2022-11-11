using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{
    public interface IRpcRequestHandler<TResponse>: IRpcRequestResponse
    {
        string MethodName { get; }
        IClient Client { get; }
        TResponse DecodeResponse(RpcResponseMessage rpcResponseMessage);
    }

    public interface IRpcRequestResponse
    {

    }
}