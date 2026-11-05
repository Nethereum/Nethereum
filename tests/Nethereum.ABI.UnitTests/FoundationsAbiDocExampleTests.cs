using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.ABIDeserialisation;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class FoundationsAbiDocExampleTests
    {
        private static string Word(BigInteger value) => value.ToString("x").PadLeft(64, '0');

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-nesting-limit", "ABI type nesting is limited to 32 levels")]
        public void Given_ASignatureOrArrayTypeNestedPastThirtyTwoLevels_When_Parsed_Then_ArgumentExceptionIsThrown()
        {
            var tuple = new string('(', 40) + "uint256" + new string(')', 40);
            var signature = "function f(" + tuple + ")";
            var arrays = "uint256" + string.Concat(Enumerable.Repeat("[]", 40));

            Assert.Throws<ArgumentException>(() => new ABIStringSignatureDeserialiser().ExtractContractABI(signature));
            Assert.Throws<ArgumentException>(() => ABIType.CreateABIType(arrays));
            Assert.NotNull(new ABIStringSignatureDeserialiser().ExtractContractABI("function f((uint256,(address,bool)))"));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-json-depth", "A JSON ABI nested past the limit is refused")]
        public void Given_AJsonAbiNestedFarPastTheLimit_When_Deserialised_Then_AJsonExceptionIsThrown()
        {
            var nested = new string('[', 200) + new string(']', 200);
            var abi = "[{\"type\":\"function\",\"name\":\"f\",\"inputs\":" + nested + "}]";

            Assert.ThrowsAny<Newtonsoft.Json.JsonException>(() => new ABIJsonDeserialiser().DeserialiseContract(abi));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-declared-size", "A declared array size larger than the data is refused")]
        public void Given_AnArrayDeclaring67MillionElementsInTwoWords_When_Decoded_Then_TheDecoderRefusesBeforeAllocating()
        {
            var encoded = (Word(1 << 26) + Word(1)).HexToByteArray();

            var exception = Assert.ThrowsAny<Exception>(() => ABIType.CreateABIType("uint256[]").Decode<List<BigInteger>>(encoded));

            Assert.Contains("Insufficient data length", exception.Message);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-duplicate-offsets", "Duplicate head offsets are refused")]
        public void Given_ADynamicArrayWhoseTwoHeadOffsetsPointAtTheSameElement_When_Decoded_Then_TheDuplicateOffsetIsRefused()
        {
            var encoded = (Word(2) + Word(0x40) + Word(0x40) + Word(1) + "61".PadRight(64, '0')).HexToByteArray();

            var exception = Assert.ThrowsAny<Exception>(() => ABIType.CreateABIType("string[]").Decode<List<string>>(encoded));

            Assert.Contains("Duplicate offset", exception.Message);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "abi-well-formed-dynamic-array", "A well-formed dynamic array still decodes")]
        public void Given_AWellFormedStringArray_When_Decoded_Then_TheElementsAreReturned()
        {
            var encoded = (Word(2) + Word(0x40) + Word(0x80) + Word(1) + "61".PadRight(64, '0') + Word(1) + "62".PadRight(64, '0')).HexToByteArray();

            var decoded = ABIType.CreateABIType("string[]").Decode<List<string>>(encoded);

            Assert.Equal(new[] { "a", "b" }, decoded);
        }
    }
}
