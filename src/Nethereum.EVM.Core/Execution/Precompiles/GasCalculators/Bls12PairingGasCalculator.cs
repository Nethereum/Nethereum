namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
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
