namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// BLAKE2F (0x09) gas calculator per EIP-152: one gas per round,
    /// where <c>rounds</c> is a big-endian u32 in the first four input
    /// bytes. Malformed input (fewer than four bytes) returns 0 — the
    /// precompile itself will fail at execution time, consuming the
    /// available call gas.
    /// </summary>
    public sealed class Blake2fGasCalculator : IPrecompileGasCalculator
    {
        public long GetGasCost(byte[] input)
        {
            if (input == null || input.Length < 4) return 0;
            return (uint)((input[0] << 24) | (input[1] << 16) | (input[2] << 8) | input[3]);
        }
    }
}
