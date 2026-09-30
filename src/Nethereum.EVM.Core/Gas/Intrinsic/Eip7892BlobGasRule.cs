namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip7892BlobGasRule : BlobGasRule
    {
        public const int DEFAULT_BASE_FEE_UPDATE_FRACTION = 5_007_716;

        public static readonly Eip7892BlobGasRule Instance = new Eip7892BlobGasRule();

        public Eip7892BlobGasRule()
            : this(DEFAULT_BASE_FEE_UPDATE_FRACTION)
        {
        }

        public Eip7892BlobGasRule(int baseFeeUpdateFraction)
            : base(baseFeeUpdateFraction, appliesReservePrice: true)
        {
        }
    }
}
