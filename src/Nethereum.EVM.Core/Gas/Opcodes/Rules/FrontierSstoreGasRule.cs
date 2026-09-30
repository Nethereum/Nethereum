namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public sealed class FrontierSstoreGasRule : ISstoreGasRule
    {
        public static readonly FrontierSstoreGasRule Instance = new FrontierSstoreGasRule();

        public bool RefusesTheFrameBeforeReadingTheSlot(long gasRemaining) => false;

        public long GetGasCost(SstoreSlotState slot) =>
            SlotIsBeingFilledFromZero(slot) ? GasConstants.SSTORE_SET : GasConstants.SSTORE_RESET_PRE_BERLIN;

        private static bool SlotIsBeingFilledFromZero(SstoreSlotState slot) =>
            slot.CurrentValueIsZero && !slot.NewValueIsZero;
    }
}
