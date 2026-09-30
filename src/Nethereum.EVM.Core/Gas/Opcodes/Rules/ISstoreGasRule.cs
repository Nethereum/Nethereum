namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// Prices one SSTORE for one fork, from a slot somebody else resolved.
    ///
    /// <para>EIP-7928: "Pre-state validation: Gas costs determinable without
    /// state access […] Post-state validation: Gas costs requiring state
    /// access". A rule is handed values and nothing that can read more, so it
    /// cannot access state ahead of the validation the EIP orders — and the same
    /// body compiles into the async host, the <c>EVM_SYNC</c> flavour and the
    /// Zisk guest.</para>
    ///
    /// <para>The refusal is the pre-state half and is therefore asked first,
    /// from the frame's remaining gas alone. EIP-7928 requires a slot that fails
    /// this check to appear in neither <c>storage_reads</c> nor
    /// <c>storage_changes</c>; the sentence is quoted where the engine enforces
    /// it, at <c>EVMSimulator.RefusedBeforePricing</c>.</para>
    /// </summary>
    public interface ISstoreGasRule
    {
        /// <summary>
        /// EIP-2200: "If <i>gasleft</i> is less than or equal to gas stipend,
        /// fail the current call frame with 'out of gas' exception."
        /// </summary>
        bool RefusesTheFrameBeforeReadingTheSlot(long gasRemaining);

        long GetGasCost(SstoreSlotState slot);
    }
}
