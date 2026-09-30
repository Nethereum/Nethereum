namespace Nethereum.EVM.Execution.Precompiles
{
    public interface IPrecompileHandler
    {
        int AddressNumeric { get; }

        byte[] Execute(byte[] input);
    }
}
