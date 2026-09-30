using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;

namespace Nethereum.EVM.Hardforks
{
    public interface IPrecompileExecutorFactory
    {
        IPrecompileHandler GetHandler(PrecompileSpec spec);

        IPrecompileGasCalculator GetGasCalculator(PrecompileSpec spec);
    }
}
