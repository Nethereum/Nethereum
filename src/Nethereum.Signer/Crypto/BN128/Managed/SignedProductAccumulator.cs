namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal struct SignedProductAccumulator
    {
        private ulong _low;
        private long _high;

        public ulong LowBits => _low;

        public long LowSigned => (long)_low;

        public void AddProduct(long a, long b)
        {
            var unsignedHigh = LimbArithmetic.Multiply((ulong)a, (ulong)b, out var productLow);
            var signedHigh = (long)unsignedHigh - (a < 0 ? b : 0) - (b < 0 ? a : 0);
            _low += productLow;
            _high += signedHigh + (_low < productLow ? 1 : 0);
        }

        public void ShiftRight62()
        {
            _low = (_low >> 62) | ((ulong)_high << 2);
            _high >>= 62;
        }
    }
}
