using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskModExpBackend : IModExpBackend
    {
        public static readonly ZiskModExpBackend Instance = new ZiskModExpBackend();

        public byte[] ModExp(byte[] baseBytes, byte[] expBytes, byte[] modulus)
        {
            if (baseBytes == null) baseBytes = System.Array.Empty<byte>();
            if (expBytes == null) expBytes = System.Array.Empty<byte>();
            var result = new byte[modulus.Length];
            uint status = ZiskCrypto.zkvm_modexp(
                baseBytes, (nuint)baseBytes.Length,
                expBytes, (nuint)expBytes.Length,
                modulus, (nuint)modulus.Length,
                result);
            if (status != 0) throw new System.ArgumentException($"MODEXP failed (status {status})");
            return result;
        }
    }
}
