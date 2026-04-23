using System.Numerics;
using Nethereum.Util;
using Nethereum.Util.Poseidon;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Util.UnitTests
{
    public class PoseidonCrossValidationTests
    {
        private readonly ITestOutputHelper _output;

        public PoseidonCrossValidationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(PoseidonParameterPreset.CircomT2)]
        [InlineData(PoseidonParameterPreset.CircomT3)]
        [InlineData(PoseidonParameterPreset.CircomT6)]
        public void BigInteger_And_EvmUInt256_ProduceSameHash_SingleInput(PoseidonParameterPreset preset)
        {
            var (biCore, evmCore) = CreateCores(preset);

            var biResult = biCore.Hash(BigInteger.One);
            var evmResult = evmCore.Hash(EvmUInt256.One);

            var biBytes = new BigIntegerPoseidonField(GetPrime()).ToBytes(biResult);
            var evmBytes = new EvmUInt256PoseidonField(PoseidonPrecomputedConstants.Prime).ToBytes(evmResult);

            Assert.Equal(biBytes, evmBytes);
            _output.WriteLine($"{preset} single input: {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(evmBytes, true)}");
        }

        [Theory]
        [InlineData(PoseidonParameterPreset.CircomT2)]
        [InlineData(PoseidonParameterPreset.CircomT3)]
        [InlineData(PoseidonParameterPreset.CircomT6)]
        public void BigInteger_And_EvmUInt256_ProduceSameHash_TwoInputs(PoseidonParameterPreset preset)
        {
            var (biCore, evmCore) = CreateCores(preset);

            var biResult = biCore.Hash(BigInteger.One, new BigInteger(2));
            var evmResult = evmCore.Hash(EvmUInt256.One, (EvmUInt256)2);

            var biBytes = new BigIntegerPoseidonField(GetPrime()).ToBytes(biResult);
            var evmBytes = new EvmUInt256PoseidonField(PoseidonPrecomputedConstants.Prime).ToBytes(evmResult);

            Assert.Equal(biBytes, evmBytes);
        }

        [Theory]
        [InlineData(PoseidonParameterPreset.CircomT2)]
        [InlineData(PoseidonParameterPreset.CircomT3)]
        [InlineData(PoseidonParameterPreset.CircomT6)]
        public void BigInteger_And_EvmUInt256_ProduceSameHash_LargeInputs(PoseidonParameterPreset preset)
        {
            var (biCore, evmCore) = CreateCores(preset);

            var largeA = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
            var largeB = BigInteger.Parse("98765432109876543210987654321098765432109876543210");

            var evmA = EvmUInt256BigIntegerExtensions.FromBigInteger(largeA);
            var evmB = EvmUInt256BigIntegerExtensions.FromBigInteger(largeB);

            var biResult = biCore.Hash(largeA, largeB);
            var evmResult = evmCore.Hash(evmA, evmB);

            var biBytes = new BigIntegerPoseidonField(GetPrime()).ToBytes(biResult);
            var evmBytes = new EvmUInt256PoseidonField(PoseidonPrecomputedConstants.Prime).ToBytes(evmResult);

            Assert.Equal(biBytes, evmBytes);
        }

        [Theory]
        [InlineData(PoseidonParameterPreset.CircomT2)]
        [InlineData(PoseidonParameterPreset.CircomT3)]
        [InlineData(PoseidonParameterPreset.CircomT6)]
        public void BigInteger_And_EvmUInt256_ProduceSameHashBytesToBytes(PoseidonParameterPreset preset)
        {
            var (biCore, evmCore) = CreateCores(preset);

            var input1 = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var input2 = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };

            var biResult = biCore.HashBytesToBytes(input1, input2);
            var evmResult = evmCore.HashBytesToBytes(input1, input2);

            Assert.Equal(biResult, evmResult);
        }

        [Fact]
        public void OriginalHasher_Matches_GenericBigIntegerCore_CircomT3()
        {
            var original = new PoseidonHasher();
            var parameters = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);
            var biField = new BigIntegerPoseidonField(parameters.Prime);
            var biCore = new PoseidonCore<BigInteger>(
                biField,
                parameters.RoundConstants,
                parameters.MdsMatrix,
                parameters.StateWidth,
                parameters.Rate,
                parameters.FullRounds,
                parameters.PartialRounds,
                (BigInteger)parameters.SBoxExponent);

            var inputs = new BigInteger[] { 1, 2, 0 };
            var originalResult = original.Hash(inputs);
            var coreResult = biCore.Hash(inputs);

            Assert.Equal(originalResult, coreResult);
            _output.WriteLine($"Original == Core<BigInteger>: {originalResult}");
        }

        private static (PoseidonCore<BigInteger> bi, PoseidonCore<EvmUInt256> evm) CreateCores(PoseidonParameterPreset preset)
        {
            var parameters = PoseidonParameterFactory.GetPreset(preset);
            var prime = parameters.Prime;
            var totalRounds = parameters.FullRounds + parameters.PartialRounds;

            var biField = new BigIntegerPoseidonField(prime);
            var biCore = new PoseidonCore<BigInteger>(
                biField,
                parameters.RoundConstants,
                parameters.MdsMatrix,
                parameters.StateWidth,
                parameters.Rate,
                parameters.FullRounds,
                parameters.PartialRounds,
                (BigInteger)parameters.SBoxExponent);

            var evmPrime = PoseidonPrecomputedConstants.Prime;
            var evmField = new EvmUInt256PoseidonField(evmPrime);

            var evmRc = new EvmUInt256[totalRounds, parameters.StateWidth];
            for (int r = 0; r < totalRounds; r++)
                for (int c = 0; c < parameters.StateWidth; c++)
                    evmRc[r, c] = EvmUInt256BigIntegerExtensions.FromBigInteger(parameters.RoundConstants[r, c]);

            var evmMds = new EvmUInt256[parameters.StateWidth, parameters.StateWidth];
            for (int r = 0; r < parameters.StateWidth; r++)
                for (int c = 0; c < parameters.StateWidth; c++)
                    evmMds[r, c] = EvmUInt256BigIntegerExtensions.FromBigInteger(parameters.MdsMatrix[r, c]);

            var evmCore = new PoseidonCore<EvmUInt256>(
                evmField,
                evmRc,
                evmMds,
                parameters.StateWidth,
                parameters.Rate,
                parameters.FullRounds,
                parameters.PartialRounds,
                (EvmUInt256)parameters.SBoxExponent);

            return (biCore, evmCore);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(0, 1)]
        public void BN254Montgomery_Matches_EvmUInt256_TwoInputs(ulong a, ulong b)
        {
            var (_, evmCore) = CreateCores(PoseidonParameterPreset.CircomT2);
            var bn254Core = CreateBN254Core();

            var evmResult = evmCore.HashBytesToBytes(
                ToBytes32(a),
                ToBytes32(b));
            var bn254Result = bn254Core.HashBytesToBytes(
                ToBytes32(a),
                ToBytes32(b));

            Assert.Equal(evmResult, bn254Result);
            _output.WriteLine($"BN254 two-input ({a},{b}): {Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(bn254Result, true)}");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(0)]
        [InlineData(42)]
        public void BN254Montgomery_Matches_EvmUInt256_SingleInput(ulong a)
        {
            var (_, evmCore) = CreateCores(PoseidonParameterPreset.CircomT2);
            var bn254Core = CreateBN254Core();

            var evmResult = evmCore.HashBytesToBytes(ToBytes32(a));
            var bn254Result = bn254Core.HashBytesToBytes(ToBytes32(a));

            Assert.Equal(evmResult, bn254Result);
        }

        [Fact]
        public void BN254Montgomery_Matches_EvmUInt256_LargeInputs()
        {
            var (_, evmCore) = CreateCores(PoseidonParameterPreset.CircomT2);
            var bn254Core = CreateBN254Core();

            var largeA = BigInteger.Parse("12345678901234567890123456789012345678901234567890");
            var largeB = BigInteger.Parse("98765432109876543210987654321098765432109876543210");

            var bytesA = EvmUInt256BigIntegerExtensions.FromBigInteger(largeA).ToBigEndian();
            var bytesB = EvmUInt256BigIntegerExtensions.FromBigInteger(largeB).ToBigEndian();

            var evmResult = evmCore.HashBytesToBytes(bytesA, bytesB);
            var bn254Result = bn254Core.HashBytesToBytes(bytesA, bytesB);

            Assert.Equal(evmResult, bn254Result);
        }

        [Fact]
        public void BN254Montgomery_Matches_EvmUInt256_RandomInputs()
        {
            var (_, evmCore) = CreateCores(PoseidonParameterPreset.CircomT2);
            var bn254Core = CreateBN254Core();
            var rng = new System.Random(12345);

            for (int i = 0; i < 100; i++)
            {
                var a = new byte[32];
                var b = new byte[32];
                rng.NextBytes(a);
                rng.NextBytes(b);

                var evmResult = evmCore.HashBytesToBytes(a, b);
                var bn254Result = bn254Core.HashBytesToBytes(a, b);

                Assert.Equal(evmResult, bn254Result);
            }
        }

        [Fact]
        public void BN254Montgomery_Matches_BigInteger_And_EvmUInt256_AllThree()
        {
            var (biCore, evmCore) = CreateCores(PoseidonParameterPreset.CircomT2);
            var bn254Core = CreateBN254Core();
            var biField = new BigIntegerPoseidonField(GetPrime());

            var inputs = new ulong[] { 1, 7, 42, 0, 999999 };
            foreach (var val in inputs)
            {
                var bytes = ToBytes32(val);

                var biResult = biField.ToBytes(biCore.Hash(
                    biCore.Hash(new BigInteger(val), BigInteger.Zero)));
                var evmResult = evmCore.HashBytesToBytes(bytes);
                var bn254Result = bn254Core.HashBytesToBytes(bytes);

                Assert.Equal(evmResult, bn254Result);
                _output.WriteLine($"val={val}: evm={Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(evmResult, true)}");
            }
        }

        [Fact]
        public void BN254Montgomery_Matches_PoseidonPairHashProvider()
        {
            var (_, evmCore) = CreateCores(PoseidonParameterPreset.CircomT2);
            var pairProvider = new Nethereum.Util.HashProviders.BN254PoseidonPairHashProvider();

            var rng = new System.Random(54321);
            for (int i = 0; i < 50; i++)
            {
                var data64 = new byte[64];
                rng.NextBytes(data64);

                var left = new byte[32];
                var right = new byte[32];
                System.Array.Copy(data64, 0, left, 0, 32);
                System.Array.Copy(data64, 32, right, 0, 32);

                var evmResult = evmCore.HashBytesToBytes(left, right);
                var pairResult = pairProvider.ComputeHash(data64);

                Assert.Equal(evmResult, pairResult);
            }

            for (int i = 0; i < 50; i++)
            {
                var data32 = new byte[32];
                rng.NextBytes(data32);

                var evmResult = evmCore.HashBytesToBytes(data32);
                var pairResult = pairProvider.ComputeHash(data32);

                Assert.Equal(evmResult, pairResult);
            }
        }

        [Fact]
        public void BN254Provider_Matches_OriginalProvider_RandomInputs()
        {
            var original = new Nethereum.Util.HashProviders.PoseidonPairHashProvider();
            var fast = new Nethereum.Util.HashProviders.BN254PoseidonPairHashProvider();
            var rng = new System.Random(99999);

            for (int i = 0; i < 200; i++)
            {
                var data64 = new byte[64];
                rng.NextBytes(data64);

                var origResult = original.ComputeHash(data64);
                var fastResult = fast.ComputeHash(data64);
                Assert.Equal(origResult, fastResult);
            }

            for (int i = 0; i < 200; i++)
            {
                var data32 = new byte[32];
                rng.NextBytes(data32);

                var origResult = original.ComputeHash(data32);
                var fastResult = fast.ComputeHash(data32);
                Assert.Equal(origResult, fastResult);
            }
        }

        [Fact]
        public void BN254Provider_Matches_OriginalProvider_Boundaries()
        {
            var original = new Nethereum.Util.HashProviders.PoseidonPairHashProvider();
            var fast = new Nethereum.Util.HashProviders.BN254PoseidonPairHashProvider();

            var allZero32 = new byte[32];
            Assert.Equal(original.ComputeHash(allZero32), fast.ComputeHash(allZero32));

            var allZero64 = new byte[64];
            Assert.Equal(original.ComputeHash(allZero64), fast.ComputeHash(allZero64));

            var allFF32 = new byte[32];
            for (int i = 0; i < 32; i++) allFF32[i] = 0xFF;
            Assert.Equal(original.ComputeHash(allFF32), fast.ComputeHash(allFF32));

            var allFF64 = new byte[64];
            for (int i = 0; i < 64; i++) allFF64[i] = 0xFF;
            Assert.Equal(original.ComputeHash(allFF64), fast.ComputeHash(allFF64));

            var one32 = new byte[32];
            one32[31] = 1;
            Assert.Equal(original.ComputeHash(one32), fast.ComputeHash(one32));

            // Near-prime value
            var nearPrime = new byte[32];
            nearPrime[0] = 0x30; nearPrime[1] = 0x64; nearPrime[2] = 0x4E;
            nearPrime[3] = 0x72; nearPrime[4] = 0xE1; nearPrime[5] = 0x31;
            nearPrime[6] = 0xA0; nearPrime[7] = 0x29;
            nearPrime[31] = 0x01;
            Assert.Equal(original.ComputeHash(nearPrime), fast.ComputeHash(nearPrime));
        }

        private static BN254PoseidonCore CreateBN254Core()
        {
            var preset = PoseidonPrecomputedConstants.GetPreset(PoseidonParameterPreset.CircomT2);

            var rc = new BN254FieldElement[preset.RoundConstants.GetLength(0), preset.RoundConstants.GetLength(1)];
            for (int r = 0; r < rc.GetLength(0); r++)
                for (int c = 0; c < rc.GetLength(1); c++)
                    rc[r, c] = BN254FieldElement.FromEvmUInt256(preset.RoundConstants[r, c]);

            var mds = new BN254FieldElement[preset.MdsMatrix.GetLength(0), preset.MdsMatrix.GetLength(1)];
            for (int r = 0; r < mds.GetLength(0); r++)
                for (int c = 0; c < mds.GetLength(1); c++)
                    mds[r, c] = BN254FieldElement.FromEvmUInt256(preset.MdsMatrix[r, c]);

            return new BN254PoseidonCore(rc, mds,
                preset.StateWidth, preset.Rate, preset.FullRounds, preset.PartialRounds);
        }

        private static byte[] ToBytes32(ulong value)
        {
            var bytes = new byte[32];
            bytes[24] = (byte)(value >> 56);
            bytes[25] = (byte)(value >> 48);
            bytes[26] = (byte)(value >> 40);
            bytes[27] = (byte)(value >> 32);
            bytes[28] = (byte)(value >> 24);
            bytes[29] = (byte)(value >> 16);
            bytes[30] = (byte)(value >> 8);
            bytes[31] = (byte)value;
            return bytes;
        }

        private static BigInteger GetPrime()
        {
            return BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");
        }
    }
}
