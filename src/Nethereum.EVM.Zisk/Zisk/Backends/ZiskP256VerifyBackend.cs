using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
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

            bool verified = false;
            uint status;
            unsafe { status = ZiskCrypto.zkvm_secp256r1_verify(hash, sig, pk, &verified); }
            return status == 0 && verified;
        }
    }
}
