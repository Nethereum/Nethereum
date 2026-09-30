namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// Prices one SELFDESTRUCT for one fork, from a sweep somebody else read.
    ///
    /// <para>EIP-7928: "Pre-state validation: Gas costs determinable without
    /// state access […] Post-state validation: Gas costs requiring state
    /// access". The two halves are the two halves of this interface, in that
    /// order. A rule is handed values and nothing that can read more, so it
    /// cannot access state ahead of the validation the EIP orders — and the
    /// same body compiles into the async host, the <c>EVM_SYNC</c> flavour and
    /// the Zisk guest.</para>
    ///
    /// <para>Nothing here is shared with the <c>CALL</c> family and nothing
    /// here may become shared with it: SELFDESTRUCT charges an account write
    /// <i>and</i> a state creation where <c>CALL</c> charges the creation
    /// alone, and the two opcodes disagree about which accessor answers the
    /// same EIP-161 question. Both differences are tracked, and a common base
    /// class would erase them.</para>
    /// </summary>
    public interface ISelfDestructBeneficiaryRule
    {
        bool TheBeneficiaryEntersTheAccessList { get; }

        /// <summary>
        /// EIP-7928: "Gas costs determinable without state access (memory
        /// expansion, base opcode cost, warm/cold access cost)."
        /// </summary>
        long PreStateValidationGas(bool isColdAccess);

        SelfDestructSweepVerdict JudgeTheSweep(SelfDestructSweep sweep);

        long NewAccountExecutionGas { get; }

        long NewAccountStateGas { get; }
    }
}
