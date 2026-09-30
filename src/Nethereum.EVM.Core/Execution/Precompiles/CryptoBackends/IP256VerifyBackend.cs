namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface IP256VerifyBackend
    {
        bool Verify(byte[] hash, byte[] r, byte[] s, byte[] publicKeyX, byte[] publicKeyY);
    }
}
