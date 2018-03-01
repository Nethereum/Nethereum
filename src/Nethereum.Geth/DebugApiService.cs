using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class DebugApiService : RpcClientWrapper
    {
        public DebugApiService(IClient client) : base(client)
        {
            BacktraceAt = new DebugBacktraceAt(client);
            BlockProfile = new DebugBlockProfile(client);
            CpuProfile = new DebugCpuProfile(client);
            DumpBlock = new DebugDumpBlock(client);
            GcStats = new DebugGcStats(client);
            GetBlockRlp = new DebugGetBlockRlp(client);
            GoTrace = new DebugGoTrace(client);
            MemStats = new DebugMemStats(client);
            SeedHash = new DebugSeedHash(client);
            SetBlockProfileRate = new DebugSetBlockProfileRate(client);
            Stacks = new DebugStacks(client);
            StartCPUProfile = new DebugStartCPUProfile(client);
            StartGoTrace = new DebugStartGoTrace(client);
            StopCPUProfile = new DebugStopCPUProfile(client);
            StopGoTrace = new DebugStopGoTrace(client);
            TraceBlock = new DebugTraceBlock(client);
            TraceBlockByHash = new DebugTraceBlockByHash(client);
            TraceBlockByNumber = new DebugTraceBlockByNumber(client);
            TraceBlockFromFile = new DebugTraceBlockFromFile(client);
            TraceTransaction = new DebugTraceTransaction(client);
            Verbosity = new DebugVerbosity(client);
            Vmodule = new DebugVmodule(client);
            StackErrorChecker = new VmStackErrorChecker();
        }

        public DebugBacktraceAt BacktraceAt { get; }
        public DebugBlockProfile BlockProfile { get; }
        public DebugCpuProfile CpuProfile { get; }
        public DebugDumpBlock DumpBlock { get; }
        public DebugGcStats GcStats { get; }
        public DebugGetBlockRlp GetBlockRlp { get; }
        public DebugGoTrace GoTrace { get; }
        public DebugMemStats MemStats { get; }
        public DebugSeedHash SeedHash { get; }
        public DebugSetBlockProfileRate SetBlockProfileRate { get; }
        public DebugStacks Stacks { get; }
        public DebugStartCPUProfile StartCPUProfile { get; }
        public DebugStartGoTrace StartGoTrace { get; }
        public DebugStopCPUProfile StopCPUProfile { get; }
        public DebugStopGoTrace StopGoTrace { get; }
        public DebugTraceBlock TraceBlock { get; }
        public DebugTraceBlockByHash TraceBlockByHash { get; }
        public DebugTraceBlockByNumber TraceBlockByNumber { get; }
        public DebugTraceBlockFromFile TraceBlockFromFile { get; }
        public DebugTraceTransaction TraceTransaction { get; }
        public DebugVerbosity Verbosity { get; }
        public DebugVmodule Vmodule { get; }

        public VmStackErrorChecker StackErrorChecker { get; }
    }
}