using Nethereum.JsonRpc.Client;

namespace Nethereum.Web3
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