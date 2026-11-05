using Nethereum.Parity.RPC.Trace;

namespace Nethereum.Parity
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "ITraceApiService")]
    public interface ITraceApiService
    {
        ITraceBlock TraceBlock { get; }
        ITraceCall TraceCall { get; }
        ITraceFilter TraceFilter { get; }
        ITraceGet TraceGet { get; }
        ITraceRawTransaction TraceRawTransaction { get; }
        ITraceTransaction TraceTransaction { get; }
    }
}