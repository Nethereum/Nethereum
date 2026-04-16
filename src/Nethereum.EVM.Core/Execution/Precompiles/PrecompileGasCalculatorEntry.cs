using Nethereum.EVM.Execution.Precompiles.GasCalculators;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// One slot in a <see cref="PrecompileGasCalculators"/> bundle — the
    /// numeric precompile address paired with its gas calculator strategy.
    /// Used as the element type of the composition constructor and the
    /// <c>.With(...)</c> bulk-override API.
    ///
    /// Declared as an explicit struct (not a named <c>ValueTuple</c>) so
    /// the public surface of <see cref="PrecompileGasCalculators"/> stays
    /// compatible with the older .NET Framework target frameworks that
    /// <c>Nethereum.EVM</c> links the EVM.Core source into
    /// (<c>net451</c>/<c>net461</c>), which do not carry the
    /// <c>System.ValueTuple</c> metadata out of the box.
    /// </summary>
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
