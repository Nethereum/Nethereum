namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface IRipemd160Backend
    {
        byte[] Hash(byte[] input);
    }
}
