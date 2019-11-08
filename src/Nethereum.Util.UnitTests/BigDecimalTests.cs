using System;
using System.Globalization;

using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class BigDecimalTests
    {
        [Theory]
        [InlineData("1234567812345678.12345678", "12345678.12345678", "15241576832799933607683.3208352565279684")]
        [InlineData("12345678.12345678", "12345678.12345678", "152415768327999.3208352565279684")]
        [InlineData("0.12345678", "0.12345678", "0.0152415765279684")]
        [InlineData("8.12345678", "8.12345678", "65.9905500565279684")]
        [InlineData("78.12345678", "78.12345678", "6103.2744992565279684")]
        [InlineData("678.12345678", "678.12345678", "459851.4226352565279684")]
        [InlineData("5678.12345678", "5678.12345678", "32241085.9904352565279684")]
        [InlineData("45678.12345678", "45678.12345678", "2086490962.5328352565279684")]
        [InlineData("345678.12345678", "345678.12345678", "119493365036.6008352565279684")]
        [InlineData("2345678.12345678", "2345678.12345678", "5502205858863.7208352565279684")]
        [InlineData("10", "10", "100")]
        [InlineData("0.0020", "0.11", "0.00022")]
        public void ShouldMultiply(string first, string second, string expected)
        {
            Assert.Equal(expected, (decimal.Parse(first) * (BigDecimal) decimal.Parse(second)).ToString());
        }


        [Theory]
        [InlineData("1234567812345678.12345678", "12345678.12345678", "15241576832799933607683.3208352565279684")]
        [InlineData("12345678.12345678", "12345678.12345678", "152415768327999.3208352565279684")]
        [InlineData("0.12345678", "0.12345678", "0.0152415765279684")]
        [InlineData("8.12345678", "8.12345678", "65.9905500565279684")]
        [InlineData("78.12345678", "78.12345678", "6103.2744992565279684")]
        [InlineData("678.12345678", "678.12345678", "459851.4226352565279684")]
        [InlineData("5678.12345678", "5678.12345678", "32241085.9904352565279684")]
        [InlineData("45678.12345678", "45678.12345678", "2086490962.5328352565279684")]
        [InlineData("345678.12345678", "345678.12345678", "119493365036.6008352565279684")]
        [InlineData("2345678.12345678", "2345678.12345678", "5502205858863.7208352565279684")]
        [InlineData("10", "10", "100")]
        [InlineData("0.002", "0.11", "0.00022")]
        public void ShouldDivide(string expected, string denominator, string numerator)
        {
            Assert.Equal(expected, (BigDecimal.Parse(numerator) / decimal.Parse(denominator)).ToString());
        }

        [Theory]
        [InlineData("15241576832799933607683.3208352565279684")]
        [InlineData("152415768327999.3208352565279684")]
        [InlineData("0.0152415765279684")]
        [InlineData("65.9905500565279684")]
        [InlineData("6103.2744992565279684")]
        [InlineData("459851.4226352565279684")]
        [InlineData("32241085.9904352565279684")]
        [InlineData("2086490962.5328352565279684")]
        [InlineData("119493365036.6008352565279684")]
        [InlineData("5502205858863.7208352565279684")]
        [InlineData("100")]
        [InlineData("0.00022")]
        public void ShouldParse(string value)
        {
            Assert.Equal(value, BigDecimal.Parse(value).ToString());
        }

        [Fact]
        public void ShouldCastToDecimal()
        {
            Assert.Equal(200.002m, (decimal) (BigDecimal) 200.002m);
            Assert.Equal(15241576832799933607683.320835m,
                (decimal) BigDecimal.Parse("15241576832799933607683.3208352565279684"));
            Assert.Equal(152415768327999.32083525652797m,
                (decimal) BigDecimal.Parse("152415768327999.3208352565279684"));
        }

        [Fact]
        public void ShouldCastToDouble()
        {
            Assert.Equal(200.002, (double) (BigDecimal) 200.002m);
            Assert.Equal(15241576832799933607683.320835,
                (double) BigDecimal.Parse("15241576832799933607683.3208352565279684"));
            Assert.Equal(152415768327999.320835, (double) BigDecimal.Parse("152415768327999.3208352565279684"));
        }

        [Fact]
        public void ShouldCastToInt()
        {
            Assert.Equal(200, (int) (BigDecimal) 200.002m);
        }

        [Theory]
        [InlineData("0.5")]
        [InlineData("-0.5")]
        [InlineData("0.51")]
        [InlineData("-0.51")]
        [InlineData("1")]
        [InlineData("0")]
        [InlineData("-1")]
        public void ShouldRoundCorrectly(string value)
        {
            var big = BigDecimal.Parse(value);
            decimal regular = decimal.Parse(value, CultureInfo.InvariantCulture);

            decimal roundedRegular = Math.Round(regular, MidpointRounding.AwayFromZero);
            var roundedBig = big.RoundAwayFromZero(significantDigits: 0);

            Assert.Equal(expected: (BigDecimal)roundedRegular, roundedBig);
        }
    }
}