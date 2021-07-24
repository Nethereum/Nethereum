using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Xunit;

namespace Nethereum.Util.UnitTests {
    public class FormattingTests {
        [Theory]
        [InlineData("1", "c", "en-US")]
        [InlineData("1.1", "c", "en-US")]
        [InlineData("-1.1", "c", "en-US")]
        [InlineData("-100000.1", "c", "en-US")]
        [InlineData("-0.0000000000000000000000001", "c", "en-US")]
        [InlineData("0.0000000000000000000000001", "c", "en-US")]
        // should round up
        [InlineData("0.5", "c0", "en-US")]
        [InlineData("0.51", "c0", "en-US")]
        [InlineData("-0.5", "c0", "en-US")]
        // should round down
        [InlineData("-0.51", "c0", "en-US")]
        [InlineData("-0.4", "c0", "en-US")]
        [InlineData("0.4", "c0", "en-US")]
        public void ShouldFormatBigDecimalCurrency(string numberText, string format, string localeName) {
            decimal number = decimal.Parse(numberText, CultureInfo.InvariantCulture);
            BigDecimal bigNumber = number;
            var cultureInfo = CultureInfo.GetCultureInfo(localeName);
            string expected = number.ToString(format, cultureInfo);
            string result = bigNumber.ToString(format, cultureInfo);
            Assert.Equal(expected, result);
        }
        [Theory]
        [InlineData("en-US")]
        [InlineData("en-GB")]
        [InlineData("he-IL")]
        public void ShouldFormatBigDecimalCurrencyWithWildExponent(string localeName) {
            var cultureInfo = CultureInfo.GetCultureInfo(localeName);

            foreach (int mantissa in new[] {1, 100})
            foreach (int exponent in new[] {5, -5}) {
                var bigNumber = new BigDecimal(mantissa: mantissa, exponent: exponent);
                decimal number = (decimal)bigNumber;
                string expected = number.ToString("c", cultureInfo);
                string result = bigNumber.ToString("c", cultureInfo);
                Assert.Equal(expected, result);
            }
        }
    }
}
