namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
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
