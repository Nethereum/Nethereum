using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskBlake2fBackend : IBlake2fBackend
    {
        public static readonly ZiskBlake2fBackend Instance = new ZiskBlake2fBackend();

        public void Compress(uint rounds, ulong[] h, ulong[] m, ulong t0, ulong t1, bool finalBlock)
        {
            var offset = new ulong[] { t0, t1 };
            byte f = (byte)(finalBlock ? 1 : 0);
            uint status = ZiskCrypto.zkvm_blake2f(rounds, h, m, offset, f);
            if (status != 0) throw new System.ArgumentException($"BLAKE2F compression failed (status {status})");
        }
    }
}
