namespace Nethereum.EVM.Core.Tests
{
    public partial class TraceDigestParityTests
    {
        private static Program ExecuteTraced(EVMSimulator simulator, Program program)
            => simulator.ExecuteWithCallStack(program, traceEnabled: true);
    }
}
