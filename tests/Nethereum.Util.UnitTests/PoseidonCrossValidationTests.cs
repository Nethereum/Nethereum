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

        private static BigInteger GetPrime()
        {
            return BigInteger.Parse("21888242871839275222246405745257275088548364400416034343698204186575808495617");
        }
    }
}
