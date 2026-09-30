namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
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
