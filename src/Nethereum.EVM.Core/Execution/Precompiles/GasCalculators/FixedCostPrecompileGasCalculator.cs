namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// Constant-gas precompile gas calculator. Takes a fixed cost at
    /// construction and returns it regardless of input. Used by precompiles
    /// whose gas is independent of input length:
    /// ECRECOVER (3000), BN128_ADD (150, EIP-1108), BN128_MUL (6000, EIP-1108),
    /// KZG_POINT_EVAL (50000, EIP-4844), BLS12_G1ADD (375, EIP-2537),
    /// BLS12_G2ADD (600, EIP-2537), BLS12_MAP_FP_TO_G1 (5500, EIP-2537),
    /// BLS12_MAP_FP2_TO_G2 (23800, EIP-2537), P256VERIFY (6900, EIP-7951).
    /// </summary>
    public sealed class FixedCostPrecompileGasCalculator : IPrecompileGasCalculator
    {
        private readonly long _cost;

        public FixedCostPrecompileGasCalculator(long cost)
        {
            _cost = cost;
        }

        public long GetGasCost(byte[] input) => _cost;
    }
}
