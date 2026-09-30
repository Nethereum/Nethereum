namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip4844BlobGasRule : BlobGasRule
    {
        public const int DEFAULT_BASE_FEE_UPDATE_FRACTION = 3338477;

        public static readonly Eip4844BlobGasRule Instance = new Eip4844BlobGasRule();

        public Eip4844BlobGasRule()
            : this(DEFAULT_BASE_FEE_UPDATE_FRACTION)
        {
        }

        public Eip4844BlobGasRule(int baseFeeUpdateFraction)
            : base(baseFeeUpdateFraction)
        {
        }
    }
}
