namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip7892Bpo1BlobGasRule : BlobGasRule
    {
        private const int BLOB_BASE_FEE_UPDATE_FRACTION = 8_346_193;

        public static readonly Eip7892Bpo1BlobGasRule Instance = new Eip7892Bpo1BlobGasRule();

        public Eip7892Bpo1BlobGasRule()
            : base(BLOB_BASE_FEE_UPDATE_FRACTION, appliesReservePrice: true)
        {
        }
    }
}
