using Nethereum.Util;

namespace Nethereum.EVM.Execution.Storage
{
    /// <summary>
    /// EIP-8038: "Each rule is an adjustment to the transaction's refund counter and is
    /// evaluated on every <c>SSTORE</c>."
    /// </summary>
    public sealed class Eip8038SstoreRefundRule : ISstoreRefundRule
    {
        public static readonly Eip8038SstoreRefundRule Instance = new Eip8038SstoreRefundRule();
        private Eip8038SstoreRefundRule() { }

        public void Apply(Program program, byte[] currentVal, byte[] newVal, byte[] origVal)
        {
            if (SlotIsCleared(currentVal, newVal, origVal))
                program.AddRefund(program.ProgramContext.SstoreClearsSchedule);

            if (ClearedSlotIsRestored(currentVal, newVal, origVal))
                program.AddRefund(-program.ProgramContext.SstoreClearsSchedule);

            if (SlotReturnsToItsTransactionStartValue(currentVal, newVal, origVal))
                program.AddRefund(program.ProgramContext.SstoreSetRefund);
        }

        /// <summary>
        /// EIP-8038: "<c>STORAGE_CLEAR_REFUND</c> is added if the original value is
        /// non-zero, the current value is non-zero and the new value is zero (a slot is
        /// cleared)."
        /// </summary>
        private static bool SlotIsCleared(byte[] currentVal, byte[] newVal, byte[] origVal) =>
            !ByteUtil.IsZero(origVal) && !ByteUtil.IsZero(currentVal) && ByteUtil.IsZero(newVal);

        /// <summary>
        /// EIP-8038: "<c>STORAGE_CLEAR_REFUND</c> is subtracted if the original value is
        /// non-zero, the current value is zero and the new value is non-zero (a slot that
        /// was cleared earlier in the same transaction is restored)."
        /// </summary>
        private static bool ClearedSlotIsRestored(byte[] currentVal, byte[] newVal, byte[] origVal) =>
            !ByteUtil.IsZero(origVal) && ByteUtil.IsZero(currentVal) && !ByteUtil.IsZero(newVal);

        /// <summary>
        /// EIP-8038: "<c>STORAGE_WRITE</c> is refunded if the new value equals the original
        /// value and differs from the current value (a change made earlier in the same
        /// transaction is undone, restoring the slot's transaction-start value)."
        /// </summary>
        private static bool SlotReturnsToItsTransactionStartValue(byte[] currentVal, byte[] newVal, byte[] origVal) =>
            ByteUtil.AreEqual(origVal, newVal) && !ByteUtil.AreEqual(newVal, currentVal);
    }
}
