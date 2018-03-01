using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Trace;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class TraceApiService : RpcClientWrapper
    {
        public TraceApiService(IClient client) : base(client)
        {
            TraceBlock = new TraceBlock(client);
            TraceCall = new TraceCall(client);
            TraceFilter = new TraceFilter(client);
            TraceGet = new TraceGet(client);
            TraceRawTransaction = new TraceRawTransaction(client);
            TraceTransaction = new TraceTransaction(client);
        }

        public TraceBlock TraceBlock { get; }
        public TraceCall TraceCall { get; }
        public TraceFilter TraceFilter { get; }
        public TraceGet TraceGet { get; }
        public TraceRawTransaction TraceRawTransaction { get; }
        public TraceTransaction TraceTransaction { get; }
    }
}