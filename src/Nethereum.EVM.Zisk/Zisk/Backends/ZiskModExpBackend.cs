using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM MODEXP backend for precompile 0x05. Wraps the native
    /// <c>ZiskCrypto.modexp_bytes_c</c> primitive — keeps
    /// <see cref="System.Numerics.BigInteger"/> off the zkVM hot path.
    /// The handler has already parsed the length header and extracted
    /// operand byte slices; this backend just computes
    /// <c>base^exp mod modulus</c> and returns the modLen-byte big-endian
    /// result.
    /// </summary>
    public sealed class ZiskModExpBackend : IModExpBackend
    {
        public static readonly ZiskModExpBackend Instance = new ZiskModExpBackend();

        public byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus)
        {
            var result = new byte[modulus.Length];
            ZiskCrypto.modexp_bytes_c(
                baseBytes, (nuint)baseBytes.Length,
                expBytes, (nuint)expBytes.Length,
                modulus, (nuint)modulus.Length,
                result);
            return result;
        }
    }
}
