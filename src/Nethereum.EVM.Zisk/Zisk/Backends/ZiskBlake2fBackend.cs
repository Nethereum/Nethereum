using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM BLAKE2F backend for precompile 0x09. Wraps the native
    /// <c>ZiskCrypto.blake2b_compress_c</c> primitive. The handler passes
    /// already-parsed state/message/counter/flag; this backend just
    /// delegates into the native compressor which mutates <paramref
    /// name="h"/> in place.
    /// </summary>
    public sealed class ZiskBlake2fBackend : IBlake2fBackend
    {
        public static readonly ZiskBlake2fBackend Instance = new ZiskBlake2fBackend();

        public void Compress(uint rounds, ulong[] h, ulong[] m, ulong t0, ulong t1, bool finalBlock)
        {
            var offset = new ulong[] { t0, t1 };
            byte f = (byte)(finalBlock ? 1 : 0);
            ZiskCrypto.blake2b_compress_c(rounds, h, m, offset, f);
        }
    }
}
