namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface IEcRecoverBackend
    {
        byte[] Recover(byte[] hash, byte v, byte[] r, byte[] s);
    }
}
