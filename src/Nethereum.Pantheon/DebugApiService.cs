using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.Debug;
using Nethereum.RPC;

namespace Nethereum.Pantheon
{
    public class DebugApiService : RpcClientWrapper, IDebugApiService
    {
        public DebugApiService(IClient client) : base(client)
        {
            DebugStorageRangeAt = new DebugStorageRangeAt(client);
            DebugTraceTransaction = new DebugTraceTransaction(client);
            DebugMetrics = new DebugMetrics(client);
        }

        public IDebugStorageRangeAt DebugStorageRangeAt { get; }
        public IDebugTraceTransaction DebugTraceTransaction { get; }
        public IDebugMetrics DebugMetrics { get; }
    }
}