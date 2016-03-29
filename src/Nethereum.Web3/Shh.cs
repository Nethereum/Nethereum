using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;

namespace Nethereum.Web3
{
    public class Shh : RpcClientWrapper
    {
        public ShhNewIdentity NewIdentity { get; private set; }
        public ShhVersion Version { get; private set; }

        public Shh(IClient client) : base(client)
        {
            NewIdentity = new ShhNewIdentity(client);
            Version = new ShhVersion(client);

        }
    }
}