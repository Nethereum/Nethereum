using Nethereum.Besu.RPC.Debug;

namespace Nethereum.Besu
{
    public interface IDebugApiService
    {
        IDebugStorageRangeAt DebugStorageRangeAt { get; }
        IDebugTraceTransaction DebugTraceTransaction { get; }
        IDebugMetrics DebugMetrics { get; }
    }
}