using Nethereum.EVM.Types;

namespace Nethereum.EVM
{
    public class InnerCallResult
    {
        public EvmCallContext CallInput { get; set; }
        public int FrameType { get; set; }
        public int Depth { get; set; }
        public long GasUsed { get; set; }
        public byte[] Output { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
#if EVM_SYNC
        public string RevertReason { get; set; }
#else
        private string revertReason;
        private bool revertReasonResolved;

        public bool Reverted { get; set; }

        public string RevertReason
        {
            get
            {
                if (!revertReasonResolved)
                {
                    revertReason = Reverted ? ProgramResult.DecodeRevertMessage(Output) : null;
                    revertReasonResolved = true;
                }
                return revertReason;
            }
            set
            {
                revertReason = value;
                revertReasonResolved = true;
            }
        }
#endif
    }
}
