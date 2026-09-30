namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface IModExpBackend
    {
        byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus);
    }
}
