namespace Nethereum.EVM.Execution.Precompiles.CryptoBackends
{
    public interface IBlake2fBackend
    {
        void Compress(uint rounds, ulong[] h, ulong[] m, ulong t0, ulong t1, bool finalBlock);
    }
}
