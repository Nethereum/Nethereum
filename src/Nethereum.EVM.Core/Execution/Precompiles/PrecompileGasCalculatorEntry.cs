using Nethereum.EVM.Execution.Precompiles.GasCalculators;

namespace Nethereum.EVM.Execution.Precompiles
{
    public readonly struct PrecompileGasCalculatorEntry
    {
        public readonly int Address;
        public readonly IPrecompileGasCalculator Calculator;

        public PrecompileGasCalculatorEntry(int address, IPrecompileGasCalculator calculator)
        {
            Address = address;
            Calculator = calculator;
        }
    }
}
