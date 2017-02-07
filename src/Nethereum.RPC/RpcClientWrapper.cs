using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{
    public class RpcClientWrapper
    {
        public RpcClientWrapper(IClient client)
        {
            Client = client;
        }

        protected IClient Client { get; set; }
    }
}