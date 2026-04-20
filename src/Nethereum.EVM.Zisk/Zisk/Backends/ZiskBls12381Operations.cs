using System;
using Nethereum.Signer.Bls;
using Nethereum.Zisk.Core;

namespace Nethereum.EVM.Zisk.Backends
{
    /// <summary>
    /// Zisk / zkVM BLS12-381 backend for EIP-2537 precompiles (0x0b..0x11,
    /// Prague+). Wraps the native <c>ZiskCrypto.bls12_381_*_c</c>
    /// primitives exported by libziskos.a, which under the hood issue
    /// CSR precompile instructions (0x80C..0x810 + arith384_mod at 0x80B).
    ///
    /// G1Mul / G2Mul are not implemented: no EVM precompile address
    /// exposes single-scalar multiplication (only G1MSM 0x0c and G2MSM 0x0e
    /// are reachable), and Zisk's <c>libziskos.a</c> does not export a
    /// corresponding <c>_c</c> symbol for them.
    /// </summary>
    public sealed class ZiskBls12381Operations : IBls12381Operations
    {
        public static readonly ZiskBls12381Operations Instance = new ZiskBls12381Operations();

        private const int G1PointSize = 128;
        private const int G2PointSize = 256;
        private const int ScalarSize = 32;
        private const int FpSize = 64;
        private const int Fp2Size = 128;
        private const int G1MsmPairSize = G1PointSize + ScalarSize;  // 160
        private const int G2MsmPairSize = G2PointSize + ScalarSize;  // 288
        private const int PairingPairSize = G1PointSize + G2PointSize; // 384

        public byte[] G1Add(byte[] p1, byte[] p2)
        {
            if (p1 == null || p1.Length != G1PointSize)
                throw new ArgumentException($"BLS12-381 G1ADD: p1 must be {G1PointSize} bytes");
            if (p2 == null || p2.Length != G1PointSize)
                throw new ArgumentException($"BLS12-381 G1ADD: p2 must be {G1PointSize} bytes");

            var ret = new byte[G1PointSize];
            byte success = ZiskCrypto.bls12_381_g1_add_c(ret, p1, p2);
            if (success != 0) throw new ArgumentException("BLS12-381 G1ADD failed");
            return ret;
        }

        public byte[] G1Mul(byte[] point, byte[] scalar)
            => throw new NotImplementedException(
                "ZiskBls12381Operations.G1Mul is not available — no EVM precompile exposes single G1 multiplication " +
                "(use G1Msm with k=1 if needed, though no precompile handler currently requires this).");

        public byte[] G1Msm(byte[][] points, byte[][] scalars)
        {
            if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0)
                throw new ArgumentException("BLS12-381 G1MSM: points and scalars must be non-empty and equal length");

            int k = points.Length;
            var pairs = new byte[k * G1MsmPairSize];
            for (int i = 0; i < k; i++)
            {
                if (points[i] == null || points[i].Length != G1PointSize)
                    throw new ArgumentException($"BLS12-381 G1MSM: point {i} must be {G1PointSize} bytes");
                if (scalars[i] == null || scalars[i].Length != ScalarSize)
                    throw new ArgumentException($"BLS12-381 G1MSM: scalar {i} must be {ScalarSize} bytes");

                Array.Copy(points[i], 0, pairs, i * G1MsmPairSize, G1PointSize);
                Array.Copy(scalars[i], 0, pairs, i * G1MsmPairSize + G1PointSize, ScalarSize);
            }

            var ret = new byte[G1PointSize];
            byte success = ZiskCrypto.bls12_381_g1_msm_c(ret, pairs, (nuint)k);
            if (success != 0) throw new ArgumentException("BLS12-381 G1MSM failed");
            return ret;
        }

        public byte[] G2Add(byte[] p1, byte[] p2)
        {
            if (p1 == null || p1.Length != G2PointSize)
                throw new ArgumentException($"BLS12-381 G2ADD: p1 must be {G2PointSize} bytes");
            if (p2 == null || p2.Length != G2PointSize)
                throw new ArgumentException($"BLS12-381 G2ADD: p2 must be {G2PointSize} bytes");

            var ret = new byte[G2PointSize];
            byte success = ZiskCrypto.bls12_381_g2_add_c(ret, p1, p2);
            if (success != 0) throw new ArgumentException("BLS12-381 G2ADD failed");
            return ret;
        }

        public byte[] G2Mul(byte[] point, byte[] scalar)
            => throw new NotImplementedException(
                "ZiskBls12381Operations.G2Mul is not available — no EVM precompile exposes single G2 multiplication.");

        public byte[] G2Msm(byte[][] points, byte[][] scalars)
        {
            if (points == null || scalars == null || points.Length != scalars.Length || points.Length == 0)
                throw new ArgumentException("BLS12-381 G2MSM: points and scalars must be non-empty and equal length");

            int k = points.Length;
            var pairs = new byte[k * G2MsmPairSize];
            for (int i = 0; i < k; i++)
            {
                if (points[i] == null || points[i].Length != G2PointSize)
                    throw new ArgumentException($"BLS12-381 G2MSM: point {i} must be {G2PointSize} bytes");
                if (scalars[i] == null || scalars[i].Length != ScalarSize)
                    throw new ArgumentException($"BLS12-381 G2MSM: scalar {i} must be {ScalarSize} bytes");

                Array.Copy(points[i], 0, pairs, i * G2MsmPairSize, G2PointSize);
                Array.Copy(scalars[i], 0, pairs, i * G2MsmPairSize + G2PointSize, ScalarSize);
            }

            var ret = new byte[G2PointSize];
            byte success = ZiskCrypto.bls12_381_g2_msm_c(ret, pairs, (nuint)k);
            if (success != 0) throw new ArgumentException("BLS12-381 G2MSM failed");
            return ret;
        }

        public bool Pairing(byte[][] g1Points, byte[][] g2Points)
        {
            if (g1Points == null || g2Points == null || g1Points.Length != g2Points.Length || g1Points.Length == 0)
                throw new ArgumentException("BLS12-381 pairing: g1/g2 arrays must be non-empty and equal length");

            int k = g1Points.Length;
            var pairs = new byte[k * PairingPairSize];
            for (int i = 0; i < k; i++)
            {
                if (g1Points[i] == null || g1Points[i].Length != G1PointSize)
                    throw new ArgumentException($"BLS12-381 pairing: g1Points[{i}] must be {G1PointSize} bytes");
                if (g2Points[i] == null || g2Points[i].Length != G2PointSize)
                    throw new ArgumentException($"BLS12-381 pairing: g2Points[{i}] must be {G2PointSize} bytes");

                Array.Copy(g1Points[i], 0, pairs, i * PairingPairSize, G1PointSize);
                Array.Copy(g2Points[i], 0, pairs, i * PairingPairSize + G1PointSize, G2PointSize);
            }

            byte status = ZiskCrypto.bls12_381_pairing_check_c(pairs, (nuint)k);
            return status == 0;
        }

        public byte[] MapFpToG1(byte[] fp)
        {
            if (fp == null || fp.Length != FpSize)
                throw new ArgumentException($"BLS12-381 MAP_FP_TO_G1: input must be {FpSize} bytes");

            var ret = new byte[G1PointSize];
            byte success = ZiskCrypto.bls12_381_fp_to_g1_c(ret, fp);
            if (success != 0) throw new ArgumentException("BLS12-381 MAP_FP_TO_G1 failed");
            return ret;
        }

        public byte[] MapFp2ToG2(byte[] fp2)
        {
            if (fp2 == null || fp2.Length != Fp2Size)
                throw new ArgumentException($"BLS12-381 MAP_FP2_TO_G2: input must be {Fp2Size} bytes");

            var ret = new byte[G2PointSize];
            byte success = ZiskCrypto.bls12_381_fp2_to_g2_c(ret, fp2);
            if (success != 0) throw new ArgumentException("BLS12-381 MAP_FP2_TO_G2 failed");
            return ret;
        }
    }
}
