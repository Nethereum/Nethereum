using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution.CallFrame
{
    public class CallFrameSetupContext
    {
        public Program Program { get; set; }
        public string CodeAddress { get; set; }
        public byte[] ByteCode { get; set; }
        public CallFrameType CallType { get; set; }
        public ExecutionStateService ExecutionState { get; set; }
        public long GasToForward { get; set; } = -1;
    }
}
