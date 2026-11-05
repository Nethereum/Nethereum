using System.Linq;
using System.Text;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.ABI.UnitTests.SecurityAudit71.S10
{
    public class Eip712RecursiveTypeS10Tests
    {
        private static byte[] Keccak(params byte[][] parts)
        {
            return Sha3Keccack.Current.CalculateHash(parts.SelectMany(x => x).ToArray());
        }

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

        private static byte[] Final(byte[] messageStruct)
        {
            var domainStruct = Keccak(Keccak(Utf8("EIP712Domain(string name)")), Keccak(Utf8("d")));
            return Keccak(new byte[] { 0x19, 0x01 }, domainStruct, messageStruct);
        }

        [Fact]
        public void S10_E01_Given_ASelfReferencingTypeMWithChildrenOfMArray_When_EncodedWithOneNestedChild_Then_TheHashIsKeccakOfTypeHashAndKeccakOfTheChildHash()
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"M\":[{\"name\":\"children\",\"type\":\"M[]\"}]},\"primaryType\":\"M\",\"domain\":{\"name\":\"d\"},\"message\":{\"children\":[{\"children\":[]}]}}";
            var typeHash = Keccak(Utf8("M(M[] children)"));
            var childHash = Keccak(typeHash, Keccak());
            var expected = Final(Keccak(typeHash, Keccak(childHash)));

            Assert.Equal(expected.ToHex(), Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex());
            Assert.Equal("50967cd795c6b57e7d3fa0fa171423321858a1167f21c047a7de7e5bf4692681", expected.ToHex());
        }

        [Fact]
        public void S10_E01_Given_ASelfReferencingTypeMWithEmptyChildren_When_Encoded_Then_TheHashIsKeccakOfTypeHashAndKeccakOfNothing()
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"M\":[{\"name\":\"children\",\"type\":\"M[]\"}]},\"primaryType\":\"M\",\"domain\":{\"name\":\"d\"},\"message\":{\"children\":[]}}";
            var expected = Final(Keccak(Keccak(Utf8("M(M[] children)")), Keccak()));

            Assert.Equal(expected.ToHex(), Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex());
            Assert.Equal("186bead6cc7e18b58b1d11ab9772215cfe548a176b6bbf8c9705f09df42aed02", expected.ToHex());
        }

        [Fact]
        public void S10_E01_Given_MutuallyRecursiveTypesAAndB_When_Encoded_Then_TheTypeStringListsAThenBAndTheHashMatchesTheSpecDerivation()
        {
            var json = "{\"types\":{\"EIP712Domain\":[{\"name\":\"name\",\"type\":\"string\"}],\"A\":[{\"name\":\"b\",\"type\":\"B[]\"}],\"B\":[{\"name\":\"a\",\"type\":\"A[]\"}]},\"primaryType\":\"A\",\"domain\":{\"name\":\"d\"},\"message\":{\"b\":[{\"a\":[]}]}}";
            var typeHashA = Keccak(Utf8("A(B[] b)B(A[] a)"));
            var typeHashB = Keccak(Utf8("B(A[] a)A(B[] b)"));
            var bHash = Keccak(typeHashB, Keccak());
            var expected = Final(Keccak(typeHashA, Keccak(bHash)));

            Assert.Equal(expected.ToHex(), Eip712TypedDataEncoder.Current.EncodeAndHashTypedData(json).ToHex());
            Assert.Equal("3f3454aa3e5deaa6ecd223420f1d2718c27cfe44942222c4025537a0d94c754c", expected.ToHex());
        }
    }
}
