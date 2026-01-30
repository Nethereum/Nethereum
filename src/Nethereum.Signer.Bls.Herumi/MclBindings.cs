using System;
using System.Runtime.InteropServices;

namespace Nethereum.Signer.Bls.Herumi
{
    public static class MclBindings
    {
        public const string dllName = "bls_eth";

        public const int FP_SIZE = 48;
        public const int FR_SIZE = 32;
        public const int G1_SIZE = FP_SIZE * 2;
        public const int G2_SIZE = FP_SIZE * 4;
        public const int GT_SIZE = FP_SIZE * 12;

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MclBnFp
        {
            public fixed byte data[FP_SIZE];
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MclBnFp2
        {
            public MclBnFp c0;
            public MclBnFp c1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MclBnG1
        {
            public MclBnFp x;
            public MclBnFp y;
            public MclBnFp z;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MclBnG2
        {
            public MclBnFp2 x;
            public MclBnFp2 y;
            public MclBnFp2 z;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MclBnGT
        {
            public fixed byte data[GT_SIZE];
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MclBnFr
        {
            public fixed byte data[FR_SIZE];
        }

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBn_pairing(ref MclBnGT z, in MclBnG1 x, in MclBnG2 y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBn_millerLoop(ref MclBnGT z, in MclBnG1 x, in MclBnG2 y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBn_millerLoopVec(ref MclBnGT z, in MclBnG1 x, in MclBnG2 y, ulong n);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBn_finalExp(ref MclBnGT y, in MclBnGT x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnGT_isOne(in MclBnGT x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnGT_mul(ref MclBnGT z, in MclBnGT x, in MclBnGT y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnFp_mapToG1(ref MclBnG1 y, in MclBnFp x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnFp2_mapToG2(ref MclBnG2 y, in MclBnFp2 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnG1_serialize([Out] byte[] buf, ulong maxBufSize, in MclBnG1 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnG1_deserialize(ref MclBnG1 x, [In] byte[] buf, ulong bufSize);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnG2_serialize([Out] byte[] buf, ulong maxBufSize, in MclBnG2 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnG2_deserialize(ref MclBnG2 x, [In] byte[] buf, ulong bufSize);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnFp_serialize([Out] byte[] buf, ulong maxBufSize, in MclBnFp x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnFp_deserialize(ref MclBnFp x, [In] byte[] buf, ulong bufSize);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnFp2_serialize([Out] byte[] buf, ulong maxBufSize, in MclBnFp2 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnFp2_deserialize(ref MclBnFp2 x, [In] byte[] buf, ulong bufSize);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG1_add(ref MclBnG1 z, in MclBnG1 x, in MclBnG1 y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG2_add(ref MclBnG2 z, in MclBnG2 x, in MclBnG2 y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG1_mul(ref MclBnG1 z, in MclBnG1 x, in MclBnFr y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG2_mul(ref MclBnG2 z, in MclBnG2 x, in MclBnFr y);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG1_mulVec(ref MclBnG1 z, in MclBnG1 x, in MclBnFr y, ulong n);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG2_mulVec(ref MclBnG2 z, in MclBnG2 x, in MclBnFr y, ulong n);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnFr_serialize([Out] byte[] buf, ulong maxBufSize, in MclBnFr x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong mclBnFr_deserialize(ref MclBnFr x, [In] byte[] buf, ulong bufSize);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnG1_isValid(in MclBnG1 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnG2_isValid(in MclBnG2 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnG1_isZero(in MclBnG1 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnG2_isZero(in MclBnG2 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG1_clear(ref MclBnG1 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mclBnG2_clear(ref MclBnG2 x);

        [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int mclBnFp_isValid(in MclBnFp x);
    }
}
