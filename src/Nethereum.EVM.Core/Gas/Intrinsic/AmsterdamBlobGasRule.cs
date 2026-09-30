namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class AmsterdamBlobGasRule : BlobGasRule
    {
        public const int DEFAULT_BASE_FEE_UPDATE_FRACTION = 11_684_671;

        public static readonly AmsterdamBlobGasRule Instance = new AmsterdamBlobGasRule();

        public AmsterdamBlobGasRule()
            : this(DEFAULT_BASE_FEE_UPDATE_FRACTION)
        {
        }

        public AmsterdamBlobGasRule(int baseFeeUpdateFraction)
            : base(baseFeeUpdateFraction, appliesReservePrice: true)
        {
        }
    }
}
