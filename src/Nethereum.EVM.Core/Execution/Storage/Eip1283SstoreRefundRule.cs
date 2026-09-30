using Nethereum.Util;

namespace Nethereum.EVM.Execution.Storage
{
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
