using Nethereum.RPC.Eth.DTOs;
using System.Numerics;

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

        public BigInteger Value { get; set; }
        public CallInput CallInput { get; set; }

        public BigInteger GasAllocated { get; set; }

        public int? SnapshotId { get; set; }
    }

    public class SubCallSetup
    {
        public bool ShouldCreateSubCall { get; set; }
        public CallFrame NewFrame { get; set; }
        public bool IsPrecompileHandled { get; set; }
        public BigInteger GasForwarded { get; set; }  // For trace: gas sent to child (matches geth's gasCost for CALL)
    }
}
