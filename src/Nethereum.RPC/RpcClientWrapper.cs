using edjCase.JsonRpc.Client;

namespace Nethereum.Web3
{
    public class RpcClientWrapper
    {
        protected RpcClient Client { get; set; }

        public RpcClientWrapper(RpcClient client)
        {
            Client = client;
        }
    }
}