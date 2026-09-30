namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// EIP-8038: "The complete <c>SSTORE</c> charge is the sum of its three cost
    /// components: 1. Access component: <c>COLD_STORAGE_ACCESS</c> or
    /// <c>WARM_ACCESS</c>, depending on whether the storage slot is cold or
    /// warm. 2. Write component: additionally charge <c>STORAGE_WRITE</c> if the
    /// new value is different from the current value and the current value
    /// equals the original value (the write moves the slot away from its
    /// transaction-start value). 3. State-creation component: additionally
    /// charge <c>GAS_STORAGE_SET</c> (per EIP-8037, metered in state-gas) […]"
    ///
    /// <para>The state-creation component is charged in
    /// <c>EvmStorageMemoryExecution</c>, where state gas is applied.</para>
    ///
    /// <para>The EIP-2200 stipend is not re-checked here. EIP-8038:
    /// "<c>COLD_STORAGE_ACCESS</c> and <c>WARM_ACCESS</c> are unchanged" — so
    /// the access component stays below <c>CALL_STIPEND</c> + 1, the threshold
    /// the central sentry in <c>EVMSimulator</c> already enforces.</para>
    /// </summary>
    public sealed class Eip8038SstoreGasRule : ISstoreGasRule
    {
        public static readonly Eip8038SstoreGasRule Instance = new Eip8038SstoreGasRule();

        public bool RefusesTheFrameBeforeReadingTheSlot(long gasRemaining) => false;

        public long GetGasCost(SstoreSlotState slot) =>
            AccessComponent(slot) + WriteComponent(slot);

        private static long AccessComponent(SstoreSlotState slot) =>
            slot.IsColdAccess ? GasConstants.COLD_SLOAD_COST : GasConstants.WARM_STORAGE_READ_COST;

        private static long WriteComponent(SstoreSlotState slot) =>
            WriteMovesTheSlotAwayFromItsTransactionStartValue(slot) ? GasConstants.EIP8038_STORAGE_WRITE : 0;

        private static bool WriteMovesTheSlotAwayFromItsTransactionStartValue(SstoreSlotState slot) =>
            slot.NewValueDiffersFromCurrent && slot.SlotStillHoldsItsTransactionStartValue;
    }
}
