using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{
    public interface IRpcClientWrapper
    {
        IClient Client { get; }
    }
}