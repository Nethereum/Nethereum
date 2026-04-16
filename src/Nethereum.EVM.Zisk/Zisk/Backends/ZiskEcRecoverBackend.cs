using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM ECRECOVER backend for precompile 0x01. Wraps the
    /// native <c>ZiskCrypto.secp256k1_ecdsa_address_recover_c</c>
    /// primitive. Returns the raw 20-byte recovered address on success,
    /// or null on any recovery failure (the handler maps that into the
    /// consensus "empty output").
    /// </summary>
    public sealed class ZiskEcRecoverBackend : IEcRecoverBackend
    {
        public static readonly ZiskEcRecoverBackend Instance = new ZiskEcRecoverBackend();

        public byte[] Recover(byte[] hash, byte v, byte[] r, byte[] s)
        {
            // v must be 27 or 28 for the native recover entry; the handler
            // has already validated bounds on r/s but not the v range here.
            if (v < 27 || v > 28) return null;
            byte recid = (byte)(v - 27);

            var sig = new byte[64];
            System.Array.Copy(r, 0, sig, 0, 32);
            System.Array.Copy(s, 0, sig, 32, 32);

            var output = new byte[20];
            byte success = ZiskCrypto.secp256k1_ecdsa_address_recover_c(sig, recid, hash, output);
            if (success != 0) return null;
            return output;
        }
    }
}
