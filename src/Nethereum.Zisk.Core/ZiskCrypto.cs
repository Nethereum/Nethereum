using System;
using System.Runtime.InteropServices;

namespace Nethereum.Zisk.Core
{
    public static class ZiskCrypto
    {
        [DllImport("__Internal")]
        public static extern void keccak256_c(byte[] input, nuint input_len, byte[] output);

        [DllImport("__Internal")]
        public static extern void sha256_c(byte[] input, nuint input_len, byte[] output);

        [DllImport("__Internal")]
        public static extern byte secp256k1_ecdsa_address_recover_c(
            byte[] sig, byte recid, byte[] msg, byte[] output);

        [DllImport("__Internal")]
        public static extern nuint modexp_bytes_c(
            byte[] base_ptr, nuint base_len,
            byte[] exp_ptr, nuint exp_len,
            byte[] modulus_ptr, nuint modulus_len,
            byte[] result_ptr);

        [DllImport("__Internal")]
        public static extern byte bn254_g1_add_c(byte[] p1, byte[] p2, byte[] ret);

        [DllImport("__Internal")]
        public static extern byte bn254_g1_mul_c(byte[] point, byte[] scalar, byte[] ret);

        [DllImport("__Internal")]
        public static extern byte bn254_pairing_check_c(byte[] pairs, nuint num_pairs);

        [DllImport("__Internal")]
        public static extern void blake2b_compress_c(
            uint rounds, ulong[] state, ulong[] message, ulong[] offset, byte final_block);

        [DllImport("__Internal")]
        public static extern byte verify_kzg_proof_c(
            byte[] z, byte[] y, byte[] commitment, byte[] proof);

        [DllImport("__Internal")]
        public static extern byte bls12_381_g1_add_c(byte[] ret, byte[] a, byte[] b);

        [DllImport("__Internal")]
        public static extern byte bls12_381_g1_msm_c(byte[] ret, byte[] pairs, nuint num_pairs);

        [DllImport("__Internal")]
        public static extern byte bls12_381_g2_add_c(byte[] ret, byte[] a, byte[] b);

        [DllImport("__Internal")]
        public static extern byte bls12_381_g2_msm_c(byte[] ret, byte[] pairs, nuint num_pairs);

        [DllImport("__Internal")]
        public static extern byte bls12_381_pairing_check_c(byte[] pairs, nuint num_pairs);

        [DllImport("__Internal")]
        public static extern byte bls12_381_fp_to_g1_c(byte[] ret, byte[] fp);

        [DllImport("__Internal")]
        public static extern byte bls12_381_fp2_to_g2_c(byte[] ret, byte[] fp2);
    }
}
