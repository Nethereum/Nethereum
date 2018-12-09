using Nethereum.Geth.RPC.Debug;

namespace Nethereum.Geth
{
    public interface IDebugApiService
    {
        IDebugBacktraceAt BacktraceAt { get; }
        IDebugBlockProfile BlockProfile { get; }
        IDebugCpuProfile CpuProfile { get; }
        IDebugDumpBlock DumpBlock { get; }
        IDebugGcStats GcStats { get; }
        IDebugGetBlockRlp GetBlockRlp { get; }
        IDebugGoTrace GoTrace { get; }
        IDebugMemStats MemStats { get; }
        IDebugSeedHash SeedHash { get; }
        IDebugSetBlockProfileRate SetBlockProfileRate { get; }
        VmStackErrorChecker StackErrorChecker { get; }
        IDebugStacks Stacks { get; }
        IDebugStartCPUProfile StartCPUProfile { get; }
        IDebugStartGoTrace StartGoTrace { get; }
        IDebugStopCPUProfile StopCPUProfile { get; }
        IDebugStopGoTrace StopGoTrace { get; }
        IDebugTraceBlock TraceBlock { get; }
        IDebugTraceBlockByHash TraceBlockByHash { get; }
        IDebugTraceBlockByNumber TraceBlockByNumber { get; }
        IDebugTraceBlockFromFile TraceBlockFromFile { get; }
        IDebugTraceTransaction TraceTransaction { get; }
        IDebugVerbosity Verbosity { get; }
        IDebugVmodule Vmodule { get; }
    }
}