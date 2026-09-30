using Nethereum.Util;

namespace Nethereum.EVM.Execution.Storage
{
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
