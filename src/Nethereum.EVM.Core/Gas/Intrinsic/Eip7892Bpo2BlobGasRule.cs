namespace Nethereum.EVM.Gas.Intrinsic
{
    public sealed class Eip7892Bpo2BlobGasRule : BlobGasRule
    {
        private const int BLOB_BASE_FEE_UPDATE_FRACTION = 11_684_671;

        public static readonly Eip7892Bpo2BlobGasRule Instance = new Eip7892Bpo2BlobGasRule();

        public Eip7892Bpo2BlobGasRule()
            : base(BLOB_BASE_FEE_UPDATE_FRACTION, appliesReservePrice: true)
        {
        }
    }
}
