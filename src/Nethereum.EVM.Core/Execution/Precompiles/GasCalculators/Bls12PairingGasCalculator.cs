namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// BLS12-381 pairing check (0x0f) gas calculator per EIP-2537:
    /// <c>baseGas + perPairGas × (inputLength / 384)</c>. Currently
    /// wired with the EIP-2537 final values (37700 base, 32600 per pair)
    /// in <see cref="PrecompileGasCalculatorSets.Prague"/>. The constants
    /// are ctor parameters so an L2 can install a different pairing cost
    /// without forking the calculator.
    /// </summary>
    public sealed class Bls12PairingGasCalculator : IPrecompileGasCalculator
    {
        private const int PairSize = 384;

        private readonly long _baseGas;
        private readonly long _perPairGas;

        public Bls12PairingGasCalculator(long baseGas, long perPairGas)
        {
            _baseGas = baseGas;
            _perPairGas = perPairGas;
        }

        public long GetGasCost(byte[] input)
        {
            int dataLen = input?.Length ?? 0;
            int k = dataLen / PairSize;
            return _baseGas + (long)k * _perPairGas;
        }
    }
}
