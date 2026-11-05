using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S10
{
    public class Eip712AcceptedFormsS10Tests
    {
        private static string Single(string type, string valueJson)
        {
            return "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"M\":[{\"name\":\"x\",\"type\":\"" + type + "\"}]},\"primaryType\":\"M\",\"domain\":{\"name\":\"d\"},\"message\":{\"x\":" + valueJson + "}}";
        }

        private static string Hash(string json) => Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex();

        [Theory]
        [InlineData("\"0663\"", "663")]
        [InlineData("\"007\"", "7")]
        [InlineData("\"-0663\"", "-663")]
        [InlineData("\"0\"", "0")]
        public void S10_F03_Given_ADecimalStringWithLeadingZeros_When_EncodedAsAnInteger_Then_ItEqualsTheDecimalWithoutThem(string padded, string plain)
        {
            Assert.Equal(Hash(Single("int256", plain)), Hash(Single("int256", padded)));
        }

        [Fact]
        public void S10_F02_Given_TheAddress0x0_When_Encoded_Then_ItEqualsTheZeroAddress()
        {
            Assert.Equal(
                Hash(Single("address", "\"0x0000000000000000000000000000000000000000\"")),
                Hash(Single("address", "\"0x0\"")));
        }

        [Theory]
        [InlineData("\"0x00\"")]
        [InlineData("\"0x\"")]
        [InlineData("\"0\"")]
        [InlineData("\"0x1\"")]
        public void S10_F02_Given_AShortAddressOtherThan0x0_When_Encoded_Then_ItIsRefused(string value)
        {
            Assert.ThrowsAny<System.Exception>(() => Hash(Single("address", value)));
        }
    }
}
