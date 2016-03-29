
namespace Nethereum.JsonRpc.Client
{
    public interface IRpcRequestHandler
    {
        string MethodName { get; }
        IClient Client { get; }
    }
}