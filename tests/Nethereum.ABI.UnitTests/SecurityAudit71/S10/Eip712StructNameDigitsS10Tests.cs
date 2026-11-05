using System.Linq;
using System.Text;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S10
{
    public class Eip712StructNameDigitsS10Tests
    {
        private static byte[] Keccak(params byte[][] parts)
        {
            return Sha3Keccack.Current.CalculateHash(parts.SelectMany(x => x).ToArray());
        }

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

        private static byte[] One() => new byte[32].Take(31).Concat(new byte[] { 1 }).ToArray();

        [Theory]
        [InlineData("Point1")]
        [InlineData("Mybytes4")]
        [InlineData("Myuint8")]
        [InlineData("int2x")]
        public void S10_E10_Given_AStructNameContainingAPrimitiveNameAndDigits_When_EncodedAndHashed_Then_ItIsEncodedAsAStruct(string structName)
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"Line\":[{\"name\":\"a\",\"type\":\"" + structName + "\"}],\"" + structName + "\":[{\"name\":\"x\",\"type\":\"uint256\"}]},\"primaryType\":\"Line\",\"domain\":{\"name\":\"d\"},\"message\":{\"a\":{\"x\":1}}}";
            var inner = Keccak(Keccak(Utf8(structName + "(uint256 x)")), One());
            var line = Keccak(Keccak(Utf8("Line(" + structName + " a)" + structName + "(uint256 x)")), inner);
            var domain = Keccak(Keccak(Utf8("EIP712Domain(string name)")), Keccak(Utf8("d")));
            var expected = Keccak(new byte[] { 0x19, 0x01 }, domain, line);

            Assert.Equal(expected.ToHex(), Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex());
        }
    }
}
