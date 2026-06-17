using Nethereum.Util;

namespace Nethereum.EVM.Execution.Storage
{
    /// <summary>
    /// Pre-EIP-1283 SSTORE refund: only the "non-zero to zero" transition on
    /// the current value adds <c>SstoreClearsSchedule</c> to the refund.
    /// Original-value tracking and the SubRefund(-clearsSchedule) net-gas
    /// adjustments are EIP-1283-specific and must NOT fire here.
    /// Used at Frontier through Byzantium and at Petersburg (EIP-1283 reverted).
    /// Mirrors geth core/vm/gas_table.go gasSStore's legacy branch.
    /// </summary>
    public sealed class LegacySstoreRefundRule : ISstoreRefundRule
    {
        public static readonly LegacySstoreRefundRule Instance = new LegacySstoreRefundRule();
        private LegacySstoreRefundRule() { }

        public void Apply(Program program, byte[] currentVal, byte[] newVal, byte[] origVal)
        {
            if (!ByteUtil.IsZero(currentVal) && ByteUtil.IsZero(newVal))
            {
                program.AddRefund(program.ProgramContext.SstoreClearsSchedule);
            }
        }
    }
}
