using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC;

namespace Nethereum.Geth
{
    public class DebugApiService : RpcClientWrapper, IDebugApiService
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
            TraceCall = new DebugTraceCall(client);
            Verbosity = new DebugVerbosity(client);
            Vmodule = new DebugVmodule(client);
            StackErrorChecker = new VmStackErrorChecker();
        }

        public IDebugBacktraceAt BacktraceAt { get; }
        public IDebugBlockProfile BlockProfile { get; }
        public IDebugCpuProfile CpuProfile { get; }
        public IDebugDumpBlock DumpBlock { get; }
        public IDebugGcStats GcStats { get; }
        public IDebugGetBlockRlp GetBlockRlp { get; }
        public IDebugGoTrace GoTrace { get; }
        public IDebugMemStats MemStats { get; }
        public IDebugSeedHash SeedHash { get; }
        public IDebugSetBlockProfileRate SetBlockProfileRate { get; }
        public IDebugStacks Stacks { get; }
        public IDebugStartCPUProfile StartCPUProfile { get; }
        public IDebugStartGoTrace StartGoTrace { get; }
        public IDebugStopCPUProfile StopCPUProfile { get; }
        public IDebugStopGoTrace StopGoTrace { get; }
        public IDebugTraceBlock TraceBlock { get; }
        public IDebugTraceBlockByHash TraceBlockByHash { get; }
        public IDebugTraceBlockByNumber TraceBlockByNumber { get; }
        public IDebugTraceBlockFromFile TraceBlockFromFile { get; }
        public IDebugTraceTransaction TraceTransaction { get; }
        public IDebugTraceCall TraceCall { get; set; }
        public IDebugVerbosity Verbosity { get; }
        public IDebugVmodule Vmodule { get; }

        public VmStackErrorChecker StackErrorChecker { get; }
    }
}