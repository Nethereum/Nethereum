using Nethereum.EVM.Types;
using Nethereum.Util;


namespace Nethereum.EVM
{
    public enum CallFrameType
    {
        Initial,
        Call,
        DelegateCall,
        StaticCall,
        CallCode,
        Create,
        Create2
    }

    public class CallFrame
    {
        public Program Program { get; set; }
        public int VmExecutionCounter { get; set; }
        public int ProgramExecutionCounter { get; set; }
        public int Depth { get; set; }
        public bool TraceEnabled { get; set; }
        public CallFrameType FrameType { get; set; }

        public int ResultMemoryDataIndex { get; set; }
        public int ResultMemoryDataLength { get; set; }

        public string NewContractAddress { get; set; }

        public EvmUInt256 Value { get; set; }
        public EvmCallContext CallInput { get; set; }

        public long GasAllocated { get; set; }

        public int? SnapshotId { get; set; }
    }

    public class SubCallSetup
    {
        public bool ShouldCreateSubCall { get; set; }
        public CallFrame NewFrame { get; set; }
        public bool IsPrecompileHandled { get; set; }
        public long GasForwarded { get; set; }
    }
}
