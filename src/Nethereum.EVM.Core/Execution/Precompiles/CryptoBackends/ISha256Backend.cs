namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface ISha256Backend
    {
        byte[] Hash(byte[] input);
    }
}
