using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;

namespace Nethereum.RPC
{
    public class ShhApiService : RpcClientWrapper, IShhApiService
    {
        public ShhApiService(IClient client) : base(client)
        {
            NewKeyPair = new ShhNewKeyPair(client);
            Version = new ShhVersion(client);
            //AddPrivateKey = new ShhAddPrivateKey(client);
        }

        public IShhNewKeyPair NewKeyPair { get; private set; }
        public IShhVersion Version { get; private set; } 
        //public IShhAddPrivateKey AddPrivateKey { get; private set; }
    }
}