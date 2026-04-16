namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// Linear "base + per-word" precompile gas calculator. Computes
    /// <c>baseGas + perWordGas × ⌈inputLength / 32⌉</c>. Used by
    /// SHA256 (60, 12), RIPEMD160 (600, 120), and IDENTITY (15, 3) —
    /// the three Frontier hash/copy precompiles.
    /// </summary>
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
