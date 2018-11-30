using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC
{
    public class RpcClientWrapper
    {
        public RpcClientWrapper(IClient client)
        {
            Client = client;
        }

        public RpcClientWrapper(IStreamingClient client)
        {
            StreamingClient = client;
        }

        public IClient Client { get; protected set; }

        public IStreamingClient StreamingClient { get; protected set; }
    }
}