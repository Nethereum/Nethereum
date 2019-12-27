using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Shh;

namespace Nethereum.RPC
{
    public class ShhApiService : RpcClientWrapper, IShhApiService
    {
        public ShhApiService(IClient client) : base(client)
        {
            KeyPair = new ShhKeyPair(client);
            Version = new ShhVersion(client);
            SymKey = new ShhSymKey(client);
            MessageFilter = new ShhMessageFilter(client);
        }
        public IShhKeyPair KeyPair { get; private set; }
        public IShhVersion Version { get; private set; } 
        public IShhSymKey SymKey { get; private set; } 
        public IShhMessageFilter MessageFilter { get; private set; }
    }
}