using Nethereum.EVM;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    /// <summary>
    /// EIP-8024 gives DUPN and SWAPN the forbidden range "90 &lt; x &lt; 128" and EXCHANGE the wider
    /// "81 &lt; x &lt; 128". The execution-level tests for both use immediate 0x60, which is forbidden
    /// under either range and is rejected on an empty stack - so they pass equally against an
    /// implementation that never checks the range at all, and against one that gives EXCHANGE
    /// DUPN's bounds. These pin the boundary itself.
    /// </summary>
    public class Eip8024ImmediateRangeBoundaryTests
    {
        [Theory]
        [InlineData(90)]
        [InlineData(128)]
        public void Given_AByteAtTheEdgeOfTheDupnForbiddenRange_When_Decoded_Then_ItIsAccepted(byte x)
        {
            Assert.True(Eip8024ImmediateCodec.TryDecodeSingle(x, out _));
        }

        [Theory]
        [InlineData(91)]
        [InlineData(127)]
        public void Given_AByteInsideTheDupnForbiddenRange_When_Decoded_Then_ItIsRejected(byte x)
        {
            Assert.False(Eip8024ImmediateCodec.TryDecodeSingle(x, out _));
        }

        [Theory]
        [InlineData(81)]
        [InlineData(128)]
        public void Given_AByteAtTheEdgeOfTheExchangeForbiddenRange_When_Decoded_Then_ItIsAccepted(byte x)
        {
            Assert.True(Eip8024ImmediateCodec.TryDecodePair(x, out _, out _));
        }

        [Theory]
        [InlineData(82)]
        [InlineData(127)]
        public void Given_AByteInsideTheExchangeForbiddenRange_When_Decoded_Then_ItIsRejected(byte x)
        {
            Assert.False(Eip8024ImmediateCodec.TryDecodePair(x, out _, out _));
        }

        [Theory]
        [InlineData(82)]
        [InlineData(85)]
        [InlineData(90)]
        public void Given_AByteLegalForDupnButNotForExchange_When_Decoded_Then_OnlyDupnAcceptsIt(byte x)
        {
            Assert.True(Eip8024ImmediateCodec.TryDecodeSingle(x, out _));
            Assert.False(Eip8024ImmediateCodec.TryDecodePair(x, out _, out _));
        }

        /// <summary>
        /// EIP-8024: decode_single "Returns n with 17 &lt;= n &lt;= 235"; decode_pair "Returns (n, m)
        /// with 1 &lt;= n &lt;= 14 and n &lt; m &lt;= 30 - n".
        /// </summary>
        [Fact]
        public void Given_EveryByte_When_Decoded_Then_TheResultsStayInsideTheRangesTheEipStates()
        {
            for (var x = 0; x <= 255; x++)
            {
                if (Eip8024ImmediateCodec.TryDecodeSingle((byte)x, out var n))
                {
                    Assert.InRange(n, 17, 235);
                }

                if (Eip8024ImmediateCodec.TryDecodePair((byte)x, out var pn, out var pm))
                {
                    Assert.InRange(pn, 1, 14);
                    Assert.True(pn < pm, $"0x{x:x2} decoded to n={pn}, m={pm}: the EIP requires n < m");
                    Assert.True(pn + pm <= 30, $"0x{x:x2} decoded to n={pn}, m={pm}: the EIP requires m <= 30 - n");
                }
            }
        }

        /// <summary>EIP-8024 §Test Cases, quoted immediates and their decoded values.</summary>
        [Theory]
        [InlineData(0x80, 17)]
        [InlineData(0xdb, 108)]
        public void Given_AnImmediateFromTheEipTestCases_When_DecodedAsSingle_Then_ItMatches(byte x, int expected)
        {
            Assert.True(Eip8024ImmediateCodec.TryDecodeSingle(x, out var n));
            Assert.Equal(expected, n);
        }

        [Theory]
        [InlineData(0x9d, 2, 3)]
        [InlineData(0x2f, 1, 19)]
        [InlineData(0x50, 14, 16)]
        [InlineData(0x51, 14, 15)]
        [InlineData(0x8e, 1, 2)]
        [InlineData(0x8f, 1, 29)]
        public void Given_AnImmediateFromTheEipTestCases_When_DecodedAsPair_Then_ItMatches(byte x, int n, int m)
        {
            Assert.True(Eip8024ImmediateCodec.TryDecodePair(x, out var actualN, out var actualM));
            Assert.Equal(n, actualN);
            Assert.Equal(m, actualM);
        }
    }
}
