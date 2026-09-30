namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    public interface IPrecompileGasCalculator
    {
        long GetGasCost(byte[] input);
    }
}
