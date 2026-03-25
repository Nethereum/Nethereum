using System;
using System.Runtime.InteropServices;

namespace Nethereum.ZkProofs.RapidSnark
{
    public static class RapidSnarkBindings
    {
        public const string LibName = "rapidsnark";

        public const int RAPIDSNARK_OK = 0;
        public const int RAPIDSNARK_ERROR = 1;
        public const int RAPIDSNARK_ERROR_SHORT_BUFFER = 2;

        // --- One-shot prover (loads zkey each call) ---

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int groth16_prover(
            byte[] zkey_buffer, ulong zkey_size,
            byte[] wtns_buffer, ulong wtns_size,
            [Out] byte[] proof_buffer, ref ulong proof_size,
            [Out] byte[] public_buffer, ref ulong public_size,
            [Out] byte[] error_msg, ulong error_msg_maxsize);

        // --- Reusable prover (create once, prove many) ---

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int groth16_prover_create(
            out IntPtr prover_object,
            byte[] zkey_buffer, ulong zkey_size,
            [Out] byte[] error_msg, ulong error_msg_maxsize);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int groth16_prover_prove(
            IntPtr prover_object,
            byte[] wtns_buffer, ulong wtns_size,
            [Out] byte[] proof_buffer, ref ulong proof_size,
            [Out] byte[] public_buffer, ref ulong public_size,
            [Out] byte[] error_msg, ulong error_msg_maxsize);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void groth16_prover_destroy(IntPtr prover_object);

        // --- Buffer size queries ---

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void groth16_proof_size(ref ulong proof_size);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void groth16_public_size_for_zkey_buf(
            byte[] zkey_buffer, ulong zkey_size,
            ref ulong public_size,
            [Out] byte[] error_msg, ulong error_msg_maxsize);

        // --- Verifier ---

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int groth16_verify(
            string proof, string inputs, string verification_key,
            [Out] byte[] error_msg, ulong error_msg_maxsize);
    }
}
