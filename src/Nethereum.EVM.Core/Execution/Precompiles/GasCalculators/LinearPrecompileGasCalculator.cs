namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    public sealed class LinearPrecompileGasCalculator : IPrecompileGasCalculator
    {
        private readonly long _baseGas;
        private readonly long _perWordGas;

        public LinearPrecompileGasCalculator(long baseGas, long perWordGas)
        {
            _baseGas = baseGas;
            _perWordGas = perWordGas;
        }

        public long GetGasCost(byte[] input)
        {
            int dataLen = input?.Length ?? 0;
            int words = (dataLen + 31) / 32;
            return _baseGas + _perWordGas * words;
        }
    }
}
