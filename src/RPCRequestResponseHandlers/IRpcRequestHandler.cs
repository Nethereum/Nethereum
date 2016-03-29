using edjCase.JsonRpc.Client;

namespace RPCRequestResponseHandlers
{
    public interface IRpcRequestHandler
    {
        string MethodName { get; }
        IClient Client { get; }
    }
}