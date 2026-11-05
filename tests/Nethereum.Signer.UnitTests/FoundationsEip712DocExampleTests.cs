using System;
using Nethereum.ABI.EIP712;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.EIP712;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class FoundationsEip712DocExampleTests
    {
        private static string Json(string person, string contents, string types)
        {
            return @"{
  ""types"": {
    ""EIP712Domain"": [{ ""name"": ""name"", ""type"": ""string"" }, { ""name"": ""chainId"", ""type"": ""uint256"" }],
    " + types + @"
  },
  ""primaryType"": ""Mail"",
  ""domain"": { ""name"": ""Ether Mail"", ""chainId"": 1 },
  ""message"": { ""from"": " + person + @", ""contents"": """ + contents + @""" }
}";
        }

        private const string RecursiveTypes = @"""Mail"": [{ ""name"": ""from"", ""type"": ""Person"" }, { ""name"": ""contents"", ""type"": ""string"" }],
    ""Person"": [{ ""name"": ""name"", ""type"": ""string"" }, { ""name"": ""friends"", ""type"": ""Person[]"" }]";

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-recursive-types", "Recursive struct types encode")]
        public void Given_ATypeThatReferencesItselfThroughAnArray_When_Encoded_Then_TheHashIsStable()
        {
            var json = Json(@"{ ""name"": ""Cow"", ""friends"": [{ ""name"": ""Bob"", ""friends"": [] }] }", "Hello", RecursiveTypes);

            var first = Eip712TypedDataSigner.Current.EncodeTypedData(json);
            var second = Eip712TypedDataSigner.Current.EncodeTypedData(json);

            Assert.Equal(first.ToHex(), second.ToHex());
            Assert.Equal(0x19, first[0]);
            Assert.Equal(0x01, first[1]);
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-refused-types", "Self-referencing and unknown member types are refused")]
        public void Given_ATypeThatReferencesItselfDirectlyOrAnUnknownType_When_Encoded_Then_ArgumentExceptionIsThrown()
        {
            var direct = Json(@"{ ""name"": ""Cow"" }", "Hello",
                @"""Mail"": [{ ""name"": ""from"", ""type"": ""Person"" }, { ""name"": ""contents"", ""type"": ""string"" }],
    ""Person"": [{ ""name"": ""name"", ""type"": ""string"" }, { ""name"": ""parent"", ""type"": ""Person"" }]");
            var unknown = Json(@"{ ""name"": ""Cow"" }", "Hello",
                @"""Mail"": [{ ""name"": ""from"", ""type"": ""Person"" }, { ""name"": ""contents"", ""type"": ""string"" }],
    ""Person"": [{ ""name"": ""name"", ""type"": ""strng"" }]");

            Assert.Throws<ArgumentException>(() => Eip712TypedDataSigner.Current.EncodeTypedData(direct));
            Assert.Throws<ArgumentException>(() => Eip712TypedDataSigner.Current.EncodeTypedData(unknown));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-value-forms", "EIP-712 values follow the declared type")]
        public void Given_AnAddressWithAndWithoutThePrefixAndAnOutOfRangeInteger_When_Encoded_Then_TheAddressFormsMatchAndTheIntegerIsRefused()
        {
            string Typed(string owner, string amount) => @"{
  ""types"": {
    ""EIP712Domain"": [{ ""name"": ""name"", ""type"": ""string"" }],
    ""Mail"": [{ ""name"": ""owner"", ""type"": ""address"" }, { ""name"": ""amount"", ""type"": ""uint8"" }]
  },
  ""primaryType"": ""Mail"",
  ""domain"": { ""name"": ""Ether Mail"" },
  ""message"": { ""owner"": """ + owner + @""", ""amount"": " + amount + @" }
}";

            var withPrefix = Eip712TypedDataSigner.Current.EncodeTypedData(Typed("0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "255"));
            var withoutPrefix = Eip712TypedDataSigner.Current.EncodeTypedData(Typed("CD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "255"));

            Assert.Equal(withPrefix.ToHex(), withoutPrefix.ToHex());
            Assert.Throws<ArgumentException>(() => Eip712TypedDataSigner.Current.EncodeTypedData(Typed("0xCD2a3d9F938E13CD947Ec05AbC7FE734Df8DD826", "256")));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-undeclared-and-missing", "Undeclared message keys are ignored and missing declared keys are refused")]
        public void Given_AMessageWithAnExtraKeyAndAMessageMissingADeclaredKey_When_Encoded_Then_TheExtraKeyIsIgnoredAndTheMissingKeyIsRefused()
        {
            string Typed(string message) => @"{
  ""types"": {
    ""EIP712Domain"": [{ ""name"": ""name"", ""type"": ""string"" }],
    ""Mail"": [{ ""name"": ""contents"", ""type"": ""string"" }]
  },
  ""primaryType"": ""Mail"",
  ""domain"": { ""name"": ""Ether Mail"" },
  ""message"": " + message + @"
}";

            var plain = Eip712TypedDataSigner.Current.EncodeTypedData(Typed(@"{ ""contents"": ""Hello"" }"));
            var withExtra = Eip712TypedDataSigner.Current.EncodeTypedData(Typed(@"{ ""contents"": ""Hello"", ""unused"": 7 }"));

            Assert.Equal(plain.ToHex(), withExtra.ToHex());
            Assert.Throws<ArgumentException>(() => Eip712TypedDataSigner.Current.EncodeTypedData(Typed("{}")));
        }

        [Fact]
        [NethereumDocExample(DocSection.Signing, "eip712-chain-id-from-domain", "GetChainIdFromDomain reads number and string chain ids")]
        public void Given_DomainsWithNumberDecimalAndHexChainIds_When_TheChainIdIsRead_Then_EachFormGivesTheSameChainId()
        {
            string Typed(string chainId) => @"{
  ""types"": {
    ""EIP712Domain"": [{ ""name"": ""chainId"", ""type"": ""uint256"" }],
    ""Mail"": [{ ""name"": ""contents"", ""type"": ""string"" }]
  },
  ""primaryType"": ""Mail"",
  ""domain"": { ""chainId"": " + chainId + @" },
  ""message"": { ""contents"": ""Hello"" }
}";

            foreach (var form in new[] { "137", "\"137\"", "\"0x89\"" })
            {
                var raw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(Typed(form));
                Assert.Equal(new System.Numerics.BigInteger(137), raw.GetChainIdFromDomain());
            }
        }
    }
}
