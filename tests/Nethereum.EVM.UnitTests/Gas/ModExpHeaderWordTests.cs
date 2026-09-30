using System;
using System.Numerics;
using Nethereum.EVM.Precompiles;
using Nethereum.EVM.Execution.Precompiles.GasCalculators;
using Nethereum.EVM.Execution.Precompiles.Handlers;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class ModExpHeaderWordTests
    {
        private static BigInteger ExpectedRightPadded(byte[] input, int offset)
        {
            BigInteger value = BigInteger.Zero;
            for (var i = 0; i < 32; i++)
            {
                var idx = offset + i;
                var b = idx < input.Length ? input[idx] : (byte)0;
                value = (value << 8) | b;
            }
            return value;
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(31)]
        [InlineData(32)]
        [InlineData(33)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(95)]
        [InlineData(96)]
        public void Given_TruncatedInput_When_HeaderWordsAreRead_Then_MissingBytesPadOnTheRight(int inputLength)
        {
            var input = new byte[inputLength];
            for (var i = 0; i < inputLength; i++) input[i] = (byte)(i + 1);

            var header = ModExpHeaderParser.Parse(input);

            Assert.Equal(ExpectedRightPadded(input, 0), header.BaseLen.ToBigInteger());
            Assert.Equal(ExpectedRightPadded(input, 32), header.ExpLen.ToBigInteger());
            Assert.Equal(ExpectedRightPadded(input, 64), header.ModLen.ToBigInteger());
        }

        [Fact]
        public void Given_APartialWord_When_Read_Then_ItIsTheHighOrderBytes_NotTheLow()
        {
            var oneBytePresent = new byte[] { 0x01 };

            var actual = ModExpHeaderParser.Parse(oneBytePresent).BaseLen.ToBigInteger();

            Assert.Equal(BigInteger.One << 248, actual);
            Assert.NotEqual(BigInteger.One, actual);
        }

        [Fact]
        public void Given_OffsetsPastTheInput_When_Read_Then_TheWordIsZero()
        {
            Assert.Equal(EvmUInt256.Zero, ModExpHeaderParser.Parse(new byte[10]).ExpLen);
            Assert.Equal(EvmUInt256.Zero, ModExpHeaderParser.Parse(Array.Empty<byte>()).BaseLen);
            Assert.Equal(EvmUInt256.Zero, ModExpHeaderParser.Parse(null).BaseLen);
        }

        [Fact]
        public void Given_ATruncatedModulusLength_When_ThePrecompileRuns_Then_ItReadsThePaddingTheSameWayCostingDoes()
        {
            var input = new byte[65];
            input[31] = 0x01;
            input[63] = 0x01;
            input[64] = 0x01;

            var precompile = new ModExpPrecompile(DefaultPrecompileBackends.Instance.ModExp);

            var ex = Assert.Throws<ArgumentException>(() => precompile.Execute(input));
            Assert.Contains("length too large", ex.Message);
        }

    }
}
