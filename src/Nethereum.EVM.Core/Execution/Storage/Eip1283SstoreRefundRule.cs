using Nethereum.Util;

namespace Nethereum.EVM.Execution.Storage
{
    /// <summary>
    /// EIP-1283 / EIP-2200 / EIP-2929 net-gas SSTORE refund.
    /// Used at Constantinople, Istanbul, Berlin, London, Paris, Shanghai,
    /// Cancun, Prague, Osaka. Per-fork constants come from
    /// <see cref="ProgramContext.SstoreClearsSchedule"/>,
    /// <see cref="ProgramContext.SstoreSetRefund"/>, and
    /// <see cref="ProgramContext.SstoreResetRefund"/>.
    /// Implements the EIP-1283 / EIP-2200 net-gas refund accounting.
    /// </summary>
    public sealed class Eip1283SstoreRefundRule : ISstoreRefundRule
    {
        public static readonly Eip1283SstoreRefundRule Instance = new Eip1283SstoreRefundRule();
        private Eip1283SstoreRefundRule() { }

        public void Apply(Program program, byte[] currentVal, byte[] newVal, byte[] origVal)
        {
            var clearsSchedule = program.ProgramContext.SstoreClearsSchedule;
            var setRefund = program.ProgramContext.SstoreSetRefund;
            var resetRefund = program.ProgramContext.SstoreResetRefund;

            if (ByteUtil.AreEqual(currentVal, origVal))
            {
                if (!ByteUtil.IsZero(origVal) && ByteUtil.IsZero(newVal))
                {
                    program.AddRefund(clearsSchedule);
                }
            }
            else
            {
                if (!ByteUtil.IsZero(origVal))
                {
                    if (ByteUtil.IsZero(currentVal))
                    {
                        program.AddRefund(-clearsSchedule);
                    }
                    else if (ByteUtil.IsZero(newVal))
                    {
                        program.AddRefund(clearsSchedule);
                    }
                }

                if (ByteUtil.AreEqual(newVal, origVal))
                {
                    if (ByteUtil.IsZero(origVal))
                    {
                        program.AddRefund(setRefund);
                    }
                    else
                    {
                        program.AddRefund(resetRefund);
                    }
                }
            }
        }
    }
}
