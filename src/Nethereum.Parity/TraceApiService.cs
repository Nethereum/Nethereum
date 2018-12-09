using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Trace;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class TraceApiService : RpcClientWrapper, ITraceApiService
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

        public ITraceBlock TraceBlock { get; }
        public ITraceCall TraceCall { get; }
        public ITraceFilter TraceFilter { get; }
        public ITraceGet TraceGet { get; }
        public ITraceRawTransaction TraceRawTransaction { get; }
        public ITraceTransaction TraceTransaction { get; }
    }
}