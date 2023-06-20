using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{
    public class RpcClientWrapper : IRpcClientWrapper
    {
        public RpcClientWrapper(IClient client)
        {
            Client = client;
        }

        public IClient Client { get; protected set; }
    }
}