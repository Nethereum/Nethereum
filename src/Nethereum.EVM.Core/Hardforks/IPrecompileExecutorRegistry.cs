namespace Nethereum.EVM.Hardforks
{
    public interface IPrecompileExecutorRegistry
    {
        IPrecompileExecutor Get(PrecompileKind kind);
    }

    public interface IPrecompileExecutor
    {
        long GetGasCost(byte[] input);

        byte[] Execute(byte[] input);
    }
}
