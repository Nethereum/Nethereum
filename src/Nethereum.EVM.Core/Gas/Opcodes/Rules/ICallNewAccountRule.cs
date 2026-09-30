namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// Prices one CALL for one fork, from a transfer somebody else read.
    ///
    /// <para>EIP-7928: "Pre-state validation: Gas costs determinable without
    /// state access […] Post-state validation: Gas costs requiring state
    /// access". The two halves are the two halves of this interface, in that
    /// order. A rule is handed values and nothing that can read more, so it
    /// cannot access state ahead of the validation the EIP orders - and the
    /// same body compiles into the async host, the <c>EVM_SYNC</c> flavour and
    /// the Zisk guest.</para>
    ///
    /// <para>Nothing here is shared with the <c>SELFDESTRUCT</c> family and
    /// nothing here may become shared with it. The two opcodes charge
    /// differently - EIP-8038 gives SELFDESTRUCT an account write <i>and</i> a
    /// state creation where a <c>CALL</c>'s creation charge is state gas alone
    /// - and they read the same EIP-161 question through different accessors,
    /// one materialising and one not. Both differences are tracked, and a
    /// common base class, rule or value object would erase them.</para>
    ///
    /// <para>Cold and warm access is not this rule's. Every account-reading
    /// opcode takes its access price from the shared
    /// <see cref="IAccessAccountRule"/> seam, so the price arrives here as a
    /// number and Berlin rebinds nothing in this interface.</para>
    /// </summary>
    public interface ICallNewAccountRule
    {
        /// <summary>
        /// EIP-7928: "Gas costs determinable without state access (memory
        /// expansion, base opcode cost, warm/cold access cost)."
        /// </summary>
        long PreStateValidationGas(long accessCost, long memoryExpansionCost, bool carriesValue);

        CallNewAccountVerdict JudgeTheTransfer(CallTransfer transfer);

        long NewAccountExecutionGas { get; }

        long NewAccountStateGas { get; }
    }
}
