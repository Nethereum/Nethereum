using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Net;

namespace Nethereum.Web3
{
    public class Net : RpcClientWrapper
    {
        public NetListening Listening { get; private set; }
        public NetPeerCount PeerCount { get; private set; }
        public NetVersion Version { get; private set; }

        public Net(IClient client) : base(client)
        {
            Listening = new NetListening(client);
            PeerCount = new NetPeerCount(client);
            Version = new NetVersion(client);
        }
    }
}