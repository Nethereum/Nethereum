using System;
using System.Runtime.InteropServices;

namespace Nethereum.Zisk.Core
{
    public static class ZiskCrypto
    {
        [DllImport("__Internal")]
        public static extern uint zkvm_keccak256(byte[] data, nuint len, byte[] output);

        [DllImport("__Internal")]
        public static extern uint zkvm_sha256(byte[] data, nuint len, byte[] output);

        [DllImport("__Internal")]
        public static extern uint zkvm_ripemd160(byte[] data, nuint len, byte[] output);

        [DllImport("__Internal")]
        public static extern uint zkvm_secp256k1_ecrecover(
            byte[] msg, byte[] sig, byte recid, byte[] output);

        [DllImport("__Internal")]
        public static extern uint zkvm_modexp(
            byte[] base_ptr, nuint base_len,
            byte[] exp_ptr, nuint exp_len,
            byte[] modulus_ptr, nuint mod_len,
            byte[] output);

        [DllImport("__Internal")]
        public static extern uint zkvm_bn254_g1_add(byte[] p1, byte[] p2, byte[] result);

        [DllImport("__Internal")]
        public static extern uint zkvm_bn254_g1_mul(byte[] point, byte[] scalar, byte[] result);

        [DllImport("__Internal")]
        public static extern byte bn254_pairing_check_c(byte[] pairs, nuint num_pairs);

        [DllImport("__Internal")]
        public static extern byte bls12_381_pairing_check_c(byte[] pairs, nuint num_pairs);

        [DllImport("__Internal")]
        public static extern uint zkvm_blake2f(
            uint rounds, ulong[] h, ulong[] m, ulong[] t, byte f);

        [DllImport("__Internal")]
        public static extern unsafe uint zkvm_kzg_point_eval(
            byte[] commitment, byte[] z, byte[] y, byte[] proof, bool* verified);

        [DllImport("__Internal")]
        public static extern uint zkvm_bls12_g1_add(byte[] p1, byte[] p2, byte[] result);

        [DllImport("__Internal")]
        public static extern uint zkvm_bls12_g1_msm(byte[] pairs, nuint num_pairs, byte[] result);

        [DllImport("__Internal")]
        public static extern uint zkvm_bls12_g2_add(byte[] p1, byte[] p2, byte[] result);

        [DllImport("__Internal")]
        public static extern uint zkvm_bls12_g2_msm(byte[] pairs, nuint num_pairs, byte[] result);

        [DllImport("__Internal")]
        public static extern uint zkvm_bls12_map_fp_to_g1(byte[] fp, byte[] result);

        [DllImport("__Internal")]
        public static extern uint zkvm_bls12_map_fp2_to_g2(byte[] fp2, byte[] result);

        [DllImport("__Internal")]
        public static extern unsafe uint zkvm_secp256r1_verify(
            byte[] msg, byte[] sig, byte[] pk, bool* verified);

        [DllImport("__Internal")]
        public static extern unsafe void poseidon2_c(ulong* state);
    }
}
