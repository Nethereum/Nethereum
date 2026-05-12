using Nethereum.EVM.Execution.Precompiles.CryptoBackends;
using Nethereum.Util;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    public sealed class ZiskEcRecoverBackend : IEcRecoverBackend
    {
        public static readonly ZiskEcRecoverBackend Instance = new ZiskEcRecoverBackend();

        public byte[] Recover(byte[] hash, byte v, byte[] r, byte[] s)
        {
            if (v < 27 || v > 28) return null;
            byte recid = (byte)(v - 27);

            var sig = new byte[64];
            System.Array.Copy(r, 0, sig, 0, 32);
            System.Array.Copy(s, 0, sig, 32, 32);

            var pubkey = new byte[64];
            uint status = ZiskCrypto.zkvm_secp256k1_ecrecover(hash, sig, recid, pubkey);
            if (status != 0) return null;

            var pubkeyHash = Sha3Keccack.Current.CalculateHash(pubkey);
            var address = new byte[20];
            System.Array.Copy(pubkeyHash, 12, address, 0, 20);
            return address;
        }
    }
}
