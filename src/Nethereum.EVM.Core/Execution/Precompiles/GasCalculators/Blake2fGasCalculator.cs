namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    public sealed class Blake2fGasCalculator : IPrecompileGasCalculator
    {
        public long GetGasCost(byte[] input)
        {
            if (input == null || input.Length < 4) return 0;
            return (uint)((input[0] << 24) | (input[1] << 16) | (input[2] << 8) | input[3]);
        }
    }
}
