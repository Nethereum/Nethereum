namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// BLS12-381 multi-scalar-multiplication gas calculator per EIP-2537.
    /// Covers both G1MSM (0x0c, pairSize = 160, base = 12000) and
    /// G2MSM (0x0e, pairSize = 288, base = 22500). Formula:
    /// <c>k × baseGas × discount(k) / 1000</c> where <c>k = input.Length / pairSize</c>
    /// and <c>discount(k)</c> comes from the supplied discount table.
    /// G1 and G2 use distinct discount tables per the spec (see
    /// <see cref="MsmDiscountTable.G1Discount"/> / <see cref="MsmDiscountTable.G2Discount"/>).
    /// For <c>k = 0</c> the cost is <c>0</c> per EIP-2537 — MSM with no
    /// pairs performs no work.
    /// </summary>
    public sealed class Bls12MsmGasCalculator : IPrecompileGasCalculator
    {
        private readonly int _pairSize;
        private readonly long _baseGas;
        private readonly int[] _discountTable;

        public Bls12MsmGasCalculator(long baseGas, int pairSize, int[] discountTable)
        {
            _baseGas = baseGas;
            _pairSize = pairSize;
            _discountTable = discountTable;
        }

        public Bls12MsmGasCalculator(long baseGas, int pairSize)
            : this(baseGas, pairSize, MsmDiscountTable.G1Discount)
        {
        }

        public long GetGasCost(byte[] input)
        {
            int dataLen = input?.Length ?? 0;
            int k = dataLen / _pairSize;
            if (k == 0) return 0;
            int discount = k <= _discountTable.Length
                ? _discountTable[k - 1]
                : _discountTable[_discountTable.Length - 1];
            return (long)k * _baseGas * discount / 1000;
        }
    }
}
