using Nethereum.Pantheon.RPC.Debug;

namespace Nethereum.Pantheon
{
    public interface IDebugApiService
    {
        IDebugStorageRangeAt DebugStorageRangeAt { get; }
        IDebugTraceTransaction DebugTraceTransaction { get; }
        IDebugMetrics DebugMetrics { get; }
    }
}