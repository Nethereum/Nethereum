using Nethereum.JsonRpc.Client;
using Nethereum.RPC.DebugGeth;

namespace Nethereum.Web3
{
    public class DebugGeth : RpcClientWrapper
    {
        public DebugBacktraceAt BacktraceAt { get; private set; }
        public DebugBlockProfile BlockProfile { get; private set; }
        public DebugCpuProfile CpuProfile { get; private set; }
        public DebugDumpBlock DumpBlock { get; private set; }
        public DebugGcStats GcStats { get; private set; }
        public DebugGetBlockRlp GetBlockRlp { get; private set; }
        public DebugGoTrace GoTrace { get; private set; }
        public DebugMemStats MemStats { get; private set; }
        public DebugSeedHash SeedHash { get; private set; }
        public DebugSetBlockProfileRate SetBlockProfileRate { get; private set; }
        public DebugStacks Stacks { get; private set; }
        public DebugStartCPUProfile StartCPUProfile { get; private set; }
        public DebugStartGoTrace StartGoTrace { get; private set; }
        public DebugStopCPUProfile StopCPUProfile { get; private set; }
        public DebugStopGoTrace StopGoTrace { get; private set; }
        public DebugTraceBlock TraceBlock { get; private set; }
        public DebugTraceBlockByHash TraceBlockByHash { get; private set; }
        public DebugTraceBlockByNumber TraceBlockByNumber { get; private set; }
        public DebugTraceBlockFromFile TraceBlockFromFile { get; private set; }
        public DebugTraceTransaction TraceTransaction { get; private set; }
        public DebugVerbosity Verbosity { get; private set; }
        public DebugVmodule Vmodule { get; private set; }

        public VmStackErrorChecker StackErrorChecker { get; private set; }

        public DebugGeth(IClient client) : base(client)
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


    }
}