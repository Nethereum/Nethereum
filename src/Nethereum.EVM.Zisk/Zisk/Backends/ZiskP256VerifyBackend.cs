using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM P-256 ECDSA verification backend for EIP-7951
    /// precompile 0x100 (Osaka+). Wraps the native
    /// <c>ZiskCrypto.secp256r1_ecdsa_verify_c</c> which issues CSR 0x817
    /// (secp256r1_add) and 0x818 (secp256r1_dbl) under the hood.
    ///
    /// The Zisk guest expects 32-byte message, 64-byte signature (r || s),
    /// and 64-byte public key (x || y), all big-endian. The handler
    /// (<c>P256VerifyPrecompile</c>) slices the 160-byte precompile input
    /// into these components and passes them here.
    /// </summary>
    public sealed class ZiskP256VerifyBackend : IP256VerifyBackend
    {
        public static readonly ZiskP256VerifyBackend Instance = new ZiskP256VerifyBackend();

        public bool Verify(byte[] hash, byte[] r, byte[] s, byte[] publicKeyX, byte[] publicKeyY)
        {
            if (hash == null || hash.Length != 32) return false;
            if (r == null || r.Length != 32) return false;
            if (s == null || s.Length != 32) return false;
            if (publicKeyX == null || publicKeyX.Length != 32) return false;
            if (publicKeyY == null || publicKeyY.Length != 32) return false;

            var sig = new byte[64];
            Array.Copy(r, 0, sig, 0, 32);
            Array.Copy(s, 0, sig, 32, 32);

            var pk = new byte[64];
            Array.Copy(publicKeyX, 0, pk, 0, 32);
            Array.Copy(publicKeyY, 0, pk, 32, 32);

            return ZiskCrypto.secp256r1_ecdsa_verify_c(hash, sig, pk) != 0;
        }
    }
}
