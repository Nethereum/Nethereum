namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// EIP-2200: "If <i>current value</i> equals <i>new value</i> (this is a
    /// no-op), <c>SLOAD_GAS</c> is deducted. If <i>current value</i> does not
    /// equal <i>new value</i>: If <i>original value</i> equals <i>current
    /// value</i> (this storage slot has not been changed by the current
    /// execution context): If <i>original value</i> is 0, <c>SSTORE_SET_GAS</c>
    /// is deducted. Otherwise, <c>SSTORE_RESET_GAS</c> gas is deducted. […] If
    /// <i>original value</i> does not equal <i>current value</i> (this storage
    /// slot is dirty), <c>SLOAD_GAS</c> gas is deducted."
    ///
    /// <para>One tree, three bindings, because each later EIP rebinds its
    /// parameters rather than restating it. EIP-2929: "modify the parameters
    /// defined in EIP-2200 as follows: <c>SLOAD_GAS</c> […]
    /// <c>= WARM_STORAGE_READ_COST</c>; <c>SSTORE_RESET_GAS</c> […]
    /// <c>5000 - COLD_SLOAD_COST</c>", and adds the surcharge: "check if the
    /// <c>(address, storage_key)</c> pair is in <c>accessed_storage_keys</c>.
    /// If it is not, charge an additional <c>COLD_SLOAD_COST</c> gas".</para>
    /// </summary>
    public sealed class Eip2200NetMeteredSstoreGasRule : ISstoreGasRule
    {
        private readonly long _sloadGas;
        private readonly long _resetGas;
        private readonly long _coldAccessSurcharge;
        private readonly bool _repeatsTheGasStipendCheck;

        private Eip2200NetMeteredSstoreGasRule(
            long sloadGas, long resetGas, long coldAccessSurcharge, bool repeatsTheGasStipendCheck)
        {
            _sloadGas = sloadGas;
            _resetGas = resetGas;
            _coldAccessSurcharge = coldAccessSurcharge;
            _repeatsTheGasStipendCheck = repeatsTheGasStipendCheck;
        }

        public static Eip2200NetMeteredSstoreGasRule AsIntroducedByEip1283(long sloadGas) =>
            new Eip2200NetMeteredSstoreGasRule(
                sloadGas,
                GasConstants.SSTORE_RESET_PRE_BERLIN,
                coldAccessSurcharge: 0,
                repeatsTheGasStipendCheck: false);

        public static Eip2200NetMeteredSstoreGasRule WithTheEip2200GasStipend(long sloadGas) =>
            new Eip2200NetMeteredSstoreGasRule(
                sloadGas,
                GasConstants.SSTORE_RESET_PRE_BERLIN,
                coldAccessSurcharge: 0,
                repeatsTheGasStipendCheck: true);

        public static Eip2200NetMeteredSstoreGasRule AsReboundByEip2929() =>
            new Eip2200NetMeteredSstoreGasRule(
                GasConstants.SSTORE_NOOP,
                GasConstants.SSTORE_RESET,
                GasConstants.COLD_SLOAD_COST,
                repeatsTheGasStipendCheck: false);

        public bool RefusesTheFrameBeforeReadingTheSlot(long gasRemaining) =>
            _repeatsTheGasStipendCheck && gasRemaining <= GasConstants.CALL_STIPEND;

        public long GetGasCost(SstoreSlotState slot) =>
            ColdAccessSurcharge(slot) + NetMeteredCost(slot);

        private long ColdAccessSurcharge(SstoreSlotState slot) =>
            slot.IsColdAccess ? _coldAccessSurcharge : 0;

        private long NetMeteredCost(SstoreSlotState slot)
        {
            if (IsNoOp(slot)) return _sloadGas;
            if (slot.SlotStillHoldsItsTransactionStartValue)
                return slot.OriginalValueIsZero ? GasConstants.SSTORE_SET : _resetGas;
            return _sloadGas;
        }

        private static bool IsNoOp(SstoreSlotState slot) => slot.NewValueEqualsCurrent;
    }
}
