namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface IBn128Backend
    {
        byte[] Add(byte[] input);

        byte[] Mul(byte[] input);

        byte[] Pairing(byte[] input);
    }
}
