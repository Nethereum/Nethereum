using Nethereum.JsonRpc.Client;
using Nethereum.Quorum.RPC.Debug;
using Nethereum.RPC;

namespace Nethereum.Quorum.RPC.Services
{
    public class DebugQuorumService : RpcClientWrapper, IDebugQuorumService
    {
        public DebugQuorumService(IClient client):base(client)
        {
            DebugDumpAddress = new DebugDumpAddress(client);
            DebugPrivateStateRoot = new DebugPrivateStateRoot(client);
        }

        public IDebugDumpAddress DebugDumpAddress { get; }
        public IDebugPrivateStateRoot DebugPrivateStateRoot { get; }
    }
}