namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip7691BlobGasRule : BlobGasRule
    {
        public const int DEFAULT_BASE_FEE_UPDATE_FRACTION = 5_007_716;

        public static readonly Eip7691BlobGasRule Instance = new Eip7691BlobGasRule();

        public Eip7691BlobGasRule()
            : this(DEFAULT_BASE_FEE_UPDATE_FRACTION)
        {
        }

        public Eip7691BlobGasRule(int baseFeeUpdateFraction)
            : base(baseFeeUpdateFraction)
        {
        }
    }
}
