namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
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
