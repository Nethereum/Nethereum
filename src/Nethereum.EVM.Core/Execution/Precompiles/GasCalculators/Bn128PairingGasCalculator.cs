namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// alt_bn128 pairing check (0x08) gas calculator:
    /// <c>baseGas + perPairGas × (inputLength / 192)</c>. Construction
    /// parameters distinguish Byzantium (100000 + 80000k) from the
    /// Istanbul EIP-1108 repricing (45000 + 34000k).
    /// </summary>
    public sealed class Bn128PairingGasCalculator : IPrecompileGasCalculator
    {
        private const int PairSize = 192;

        private readonly long _baseGas;
        private readonly long _perPairGas;

        public Bn128PairingGasCalculator(long baseGas, long perPairGas)
        {
            _baseGas = baseGas;
            _perPairGas = perPairGas;
        }

        public long GetGasCost(byte[] input)
        {
            int dataLen = input?.Length ?? 0;
            int k = dataLen / PairSize;
            return _baseGas + _perPairGas * k;
        }
    }
}
