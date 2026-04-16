using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM RIPEMD-160 backend for precompile 0x03. Delegates to
    /// the managed <see cref="Ripemd160"/> helper in
    /// <c>Nethereum.Zisk.Core</c> so the EVM precompile stays free of
    /// BouncyCastle while running inside the zkVM. Returns the raw
    /// 20-byte digest; the handler right-pads it to 32 bytes.
    /// </summary>
    public sealed class ZiskRipemd160Backend : IRipemd160Backend
    {
        public static readonly ZiskRipemd160Backend Instance = new ZiskRipemd160Backend();

        public byte[] Hash(byte[] input)
        {
            var data = input ?? new byte[0];
            return Ripemd160.ComputeHash(data);
        }
    }
}
