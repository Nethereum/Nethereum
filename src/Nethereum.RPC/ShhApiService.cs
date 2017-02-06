using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;

namespace Nethereum.Web3
{
    public class ShhApiService : RpcClientWrapper
    {
        public ShhApiService(IClient client) : base(client)
        {
            NewIdentity = new ShhNewIdentity(client);
            Version = new ShhVersion(client);
        }

        public ShhNewIdentity NewIdentity { get; private set; }
        public ShhVersion Version { get; private set; }
    }
}