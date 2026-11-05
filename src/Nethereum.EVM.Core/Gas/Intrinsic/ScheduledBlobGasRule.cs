namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class ScheduledBlobGasRule : BlobGasRule
    {
        public ScheduledBlobGasRule(int baseFeeUpdateFraction, bool appliesReservePrice)
            : base(baseFeeUpdateFraction, appliesReservePrice)
        {
        }
    }
}
