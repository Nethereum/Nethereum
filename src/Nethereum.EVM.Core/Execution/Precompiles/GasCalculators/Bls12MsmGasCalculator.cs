namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// BLS12-381 multi-scalar-multiplication gas calculator per EIP-2537.
    /// Covers both G1MSM (0x0c, pairSize = 160, base = 12000) and
    /// G2MSM (0x0e, pairSize = 288, base = 22500). Formula:
    /// <c>k × baseGas × discount(k) / 1000</c> where <c>k = input.Length / pairSize</c>
    /// and <c>discount(k)</c> comes from <see cref="MsmDiscountTable.Discount"/>.
    /// For <c>k = 0</c> the cost collapses to <c>0</c> since MSM with no
    /// pairs has no work to do (matches the legacy implementation).
    /// </summary>
    public sealed class Bls12MsmGasCalculator : IPrecompileGasCalculator
    {
        private readonly int _pairSize;
        private readonly long _baseGas;

        public Bls12MsmGasCalculator(long baseGas, int pairSize)
        {
            _baseGas = baseGas;
            _pairSize = pairSize;
        }

        public long GetGasCost(byte[] input)
        {
            int dataLen = input?.Length ?? 0;
            int k = dataLen / _pairSize;
            if (k == 0) return _baseGas;
            int discount = k <= MsmDiscountTable.Discount.Length
                ? MsmDiscountTable.Discount[k - 1]
                : MsmDiscountTable.Discount[MsmDiscountTable.Discount.Length - 1];
            return (long)k * _baseGas * discount / 1000;
        }
    }
}
