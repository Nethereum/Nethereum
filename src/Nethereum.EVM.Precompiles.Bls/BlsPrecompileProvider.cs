using System;
using System.Numerics;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Bls;

namespace Nethereum.EVM.Precompiles.Bls
{
    public class BlsPrecompileProvider : IPrecompileProvider
    {
        private readonly IBls12381Operations _blsOperations;

        public BlsPrecompileProvider(IBls12381Operations blsOperations)
        {
            _blsOperations = blsOperations ?? throw new ArgumentNullException(nameof(blsOperations));
        }
        public const string G1ADD_ADDRESS = "b";
        public const string G1MSM_ADDRESS = "c";
        public const string G2ADD_ADDRESS = "d";
        public const string G2MSM_ADDRESS = "e";
        public const string PAIRING_ADDRESS = "f";
        public const string MAP_FP_TO_G1_ADDRESS = "10";
        public const string MAP_FP2_TO_G2_ADDRESS = "11";

        public const int G1_POINT_SIZE = 128;  // 64 bytes x, 64 bytes y (padded)
        public const int G2_POINT_SIZE = 256;  // 128 bytes x (Fp2), 128 bytes y (Fp2)
        public const int SCALAR_SIZE = 32;
        public const int FP_SIZE = 64;         // Padded to 64 bytes
        public const int FP2_SIZE = 128;       // Two Fp elements

        public const int G1ADD_GAS = 375;
        public const int G2ADD_GAS = 600;
        public const int MAP_FP_TO_G1_GAS = 5500;
        public const int MAP_FP2_TO_G2_GAS = 23800;
        public const int PAIRING_BASE_GAS = 37700;
        public const int PAIRING_PER_PAIR_GAS = 32600;

        private static readonly int[] MsmDiscountTable = new int[]
        {
            1000, 949, 848, 797, 764, 750, 738, 728, 719, 712,
            705, 698, 692, 687, 682, 677, 673, 669, 665, 661,
            658, 654, 651, 648, 645, 642, 640, 637, 635, 632,
            630, 627, 625, 623, 621, 619, 617, 615, 613, 611,
            609, 608, 606, 604, 603, 601, 599, 598, 596, 595,
            593, 592, 591, 589, 588, 586, 585, 584, 582, 581,
            580, 579, 577, 576, 575, 574, 573, 572, 570, 569,
            568, 567, 566, 565, 564, 563, 562, 561, 560, 559,
            558, 557, 556, 555, 554, 553, 552, 551, 550, 549,
            548, 547, 547, 546, 545, 544, 543, 542, 542, 541,
            540, 539, 538, 538, 537, 536, 535, 535, 534, 533,
            532, 532, 531, 530, 530, 529, 528, 528, 527, 526,
            526, 525, 524, 524, 523, 522, 522, 521, 520, 520
        };

        public bool CanHandle(string address)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            return compact == G1ADD_ADDRESS ||
                   compact == G1MSM_ADDRESS ||
                   compact == G2ADD_ADDRESS ||
                   compact == G2MSM_ADDRESS ||
                   compact == PAIRING_ADDRESS ||
                   compact == MAP_FP_TO_G1_ADDRESS ||
                   compact == MAP_FP2_TO_G2_ADDRESS;
        }

        public BigInteger GetGasCost(string address, byte[] data)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            int dataLen = data?.Length ?? 0;

            switch (compact)
            {
                case G1ADD_ADDRESS:
                    return G1ADD_GAS;

                case G1MSM_ADDRESS:
                    return GetMsmGas(dataLen, G1_POINT_SIZE + SCALAR_SIZE, 12000);

                case G2ADD_ADDRESS:
                    return G2ADD_GAS;

                case G2MSM_ADDRESS:
                    return GetMsmGas(dataLen, G2_POINT_SIZE + SCALAR_SIZE, 22500);

                case PAIRING_ADDRESS:
                    int pairSize = G1_POINT_SIZE + G2_POINT_SIZE;
                    int numPairs = dataLen / pairSize;
                    return PAIRING_BASE_GAS + (PAIRING_PER_PAIR_GAS * numPairs);

                case MAP_FP_TO_G1_ADDRESS:
                    return MAP_FP_TO_G1_GAS;

                case MAP_FP2_TO_G2_ADDRESS:
                    return MAP_FP2_TO_G2_GAS;

                default:
                    return 0;
            }
        }

        private BigInteger GetMsmGas(int dataLen, int elementSize, int baseGas)
        {
            if (dataLen == 0 || dataLen % elementSize != 0)
                return 0;

            int k = dataLen / elementSize;
            if (k == 0) return 0;

            int discount = k <= MsmDiscountTable.Length ? MsmDiscountTable[k - 1] : MsmDiscountTable[MsmDiscountTable.Length - 1];
            return (BigInteger)k * baseGas * discount / 1000;
        }

        public byte[] Execute(string address, byte[] data)
        {
            var compact = address.ToHexCompact().ToLowerInvariant();
            data = data ?? Array.Empty<byte>();

            switch (compact)
            {
                case G1ADD_ADDRESS:
                    return G1Add(data);

                case G1MSM_ADDRESS:
                    return G1Msm(data);

                case G2ADD_ADDRESS:
                    return G2Add(data);

                case G2MSM_ADDRESS:
                    return G2Msm(data);

                case PAIRING_ADDRESS:
                    return Pairing(data);

                case MAP_FP_TO_G1_ADDRESS:
                    return MapFpToG1(data);

                case MAP_FP2_TO_G2_ADDRESS:
                    return MapFp2ToG2(data);

                default:
                    throw new ArgumentException($"Unknown BLS precompile address: {address}");
            }
        }

        private byte[] G1Add(byte[] data)
        {
            if (data.Length != G1_POINT_SIZE * 2)
                throw new ArgumentException($"Invalid G1ADD input length: expected {G1_POINT_SIZE * 2}, got {data.Length}");

            var p1 = DecodeG1Point(data, 0);
            var p2 = DecodeG1Point(data, G1_POINT_SIZE);

            var result = _blsOperations.G1Add(p1, p2);
            return EncodeG1Point(result);
        }

        private byte[] G1Msm(byte[] data)
        {
            int elementSize = G1_POINT_SIZE + SCALAR_SIZE;
            if (data.Length == 0 || data.Length % elementSize != 0)
                throw new ArgumentException($"Invalid G1MSM input length: {data.Length}");

            int k = data.Length / elementSize;
            var points = new byte[k][];
            var scalars = new byte[k][];

            for (int i = 0; i < k; i++)
            {
                int offset = i * elementSize;
                points[i] = DecodeG1Point(data, offset);
                scalars[i] = new byte[SCALAR_SIZE];
                Array.Copy(data, offset + G1_POINT_SIZE, scalars[i], 0, SCALAR_SIZE);
            }

            var result = _blsOperations.G1Msm(points, scalars);
            return EncodeG1Point(result);
        }

        private byte[] G2Add(byte[] data)
        {
            if (data.Length != G2_POINT_SIZE * 2)
                throw new ArgumentException($"Invalid G2ADD input length: expected {G2_POINT_SIZE * 2}, got {data.Length}");

            var p1 = DecodeG2Point(data, 0);
            var p2 = DecodeG2Point(data, G2_POINT_SIZE);

            var result = _blsOperations.G2Add(p1, p2);
            return EncodeG2Point(result);
        }

        private byte[] G2Msm(byte[] data)
        {
            int elementSize = G2_POINT_SIZE + SCALAR_SIZE;
            if (data.Length == 0 || data.Length % elementSize != 0)
                throw new ArgumentException($"Invalid G2MSM input length: {data.Length}");

            int k = data.Length / elementSize;
            var points = new byte[k][];
            var scalars = new byte[k][];

            for (int i = 0; i < k; i++)
            {
                int offset = i * elementSize;
                points[i] = DecodeG2Point(data, offset);
                scalars[i] = new byte[SCALAR_SIZE];
                Array.Copy(data, offset + G2_POINT_SIZE, scalars[i], 0, SCALAR_SIZE);
            }

            var result = _blsOperations.G2Msm(points, scalars);
            return EncodeG2Point(result);
        }

        private byte[] Pairing(byte[] data)
        {
            int pairSize = G1_POINT_SIZE + G2_POINT_SIZE;
            if (data.Length % pairSize != 0)
                throw new ArgumentException($"Invalid PAIRING input length: {data.Length}");

            if (data.Length == 0)
            {
                // Empty input returns 1 (pairing of empty set is 1)
                var result = new byte[32];
                result[31] = 1;
                return result;
            }

            int k = data.Length / pairSize;
            var g1Points = new byte[k][];
            var g2Points = new byte[k][];

            for (int i = 0; i < k; i++)
            {
                int offset = i * pairSize;
                g1Points[i] = DecodeG1Point(data, offset);
                g2Points[i] = DecodeG2Point(data, offset + G1_POINT_SIZE);
            }

            bool pairingResult = _blsOperations.Pairing(g1Points, g2Points);
            var output = new byte[32];
            if (pairingResult)
                output[31] = 1;
            return output;
        }

        private byte[] MapFpToG1(byte[] data)
        {
            if (data.Length != FP_SIZE)
                throw new ArgumentException($"Invalid MAP_FP_TO_G1 input length: expected {FP_SIZE}, got {data.Length}");

            var result = _blsOperations.MapFpToG1(data);
            return EncodeG1Point(result);
        }

        private byte[] MapFp2ToG2(byte[] data)
        {
            if (data.Length != FP2_SIZE)
                throw new ArgumentException($"Invalid MAP_FP2_TO_G2 input length: expected {FP2_SIZE}, got {data.Length}");

            var result = _blsOperations.MapFp2ToG2(data);
            return EncodeG2Point(result);
        }

        private byte[] DecodeG1Point(byte[] data, int offset)
        {
            var point = new byte[G1_POINT_SIZE];
            Array.Copy(data, offset, point, 0, G1_POINT_SIZE);
            return point;
        }

        private byte[] DecodeG2Point(byte[] data, int offset)
        {
            var point = new byte[G2_POINT_SIZE];
            Array.Copy(data, offset, point, 0, G2_POINT_SIZE);
            return point;
        }

        private byte[] EncodeG1Point(byte[] point)
        {
            if (point.Length != G1_POINT_SIZE)
            {
                var padded = new byte[G1_POINT_SIZE];
                Array.Copy(point, 0, padded, G1_POINT_SIZE - point.Length, point.Length);
                return padded;
            }
            return point;
        }

        private byte[] EncodeG2Point(byte[] point)
        {
            if (point.Length != G2_POINT_SIZE)
            {
                var padded = new byte[G2_POINT_SIZE];
                Array.Copy(point, 0, padded, G2_POINT_SIZE - point.Length, point.Length);
                return padded;
            }
            return point;
        }
    }
}
