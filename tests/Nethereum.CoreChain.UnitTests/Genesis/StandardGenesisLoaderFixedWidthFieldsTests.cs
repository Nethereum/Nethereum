using System.Linq;
using Nethereum.CoreChain.Genesis;
using Newtonsoft.Json.Linq;
using Xunit;
using CoreChainConfig = Nethereum.CoreChain.ChainConfig;

namespace Nethereum.CoreChain.UnitTests.Genesis
{
    public class StandardGenesisLoaderFixedWidthFieldsTests
    {
        [Fact]
        public void Given_AGenesisJsonNonceShorterThanEightBytes_When_Parsed_Then_ItIsLeftPaddedToEightBytes()
        {
            var root = JObject.Parse(@"{ ""nonce"": ""0x0"", ""mixHash"": ""0x0"" }");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal(8, document.Nonce.Length);
            Assert.All(document.Nonce, b => Assert.Equal((byte)0, b));
            Assert.Equal(32, document.MixHash.Length);
            Assert.All(document.MixHash, b => Assert.Equal((byte)0, b));
        }

        [Fact]
        public void Given_AGenesisJsonNonceAlreadyEightBytesWide_When_Parsed_Then_ItIsUnchanged()
        {
            var root = JObject.Parse(@"{ ""nonce"": ""0x0102030405060708"" }");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal(
                new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
                document.Nonce);
        }

        [Fact]
        public void Given_AGenesisJsonMixHashShorterThanThirtyTwoBytes_When_Parsed_Then_ItIsLeftPaddedToThirtyTwoBytes()
        {
            var root = JObject.Parse(@"{ ""mixHash"": ""0xabcd"" }");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal(32, document.MixHash.Length);
            Assert.Equal(new byte[] { 0xab, 0xcd }, document.MixHash.Skip(30).ToArray());
            Assert.All(document.MixHash.Take(30), b => Assert.Equal((byte)0, b));
        }

        [Fact]
        public void Given_AGenesisJsonMixHashAlreadyThirtyTwoBytesWide_When_Parsed_Then_ItIsUnchanged()
        {
            var fullWidthHex = "0x" + string.Concat(Enumerable.Repeat("ab", 32));
            var root = JObject.Parse($@"{{ ""mixHash"": ""{fullWidthHex}"" }}");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal(32, document.MixHash.Length);
            Assert.All(document.MixHash, b => Assert.Equal((byte)0xab, b));
        }

        [Fact]
        public void Given_AnAbsentGenesisJsonNonce_When_Parsed_Then_ItIsStillEightZeroBytes()
        {
            var root = JObject.Parse(@"{}");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal(8, document.Nonce.Length);
            Assert.All(document.Nonce, b => Assert.Equal((byte)0, b));
            Assert.Equal(32, document.MixHash.Length);
            Assert.All(document.MixHash, b => Assert.Equal((byte)0, b));
        }

        [Fact]
        public void Given_AGenesisJsonExtraDataOfVariableLength_When_Parsed_Then_ItIsNotPadded()
        {
            var root = JObject.Parse(@"{ ""extraData"": ""0x01"" }");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal(new byte[] { 0x01 }, document.ExtraData);
        }

        [Fact]
        public void Given_AGenesisJsonConfigWithADepositContractAddress_When_Parsed_Then_ItIsCarriedOnTheConfig()
        {
            var root = JObject.Parse(@"{ ""config"": { ""depositContractAddress"": ""0x00000000219ab540356cbb839cbe05303d7705fa"" } }");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Equal("0x00000000219ab540356cbb839cbe05303d7705fa", document.Config.DepositContractAddress);
        }

        [Fact]
        public void Given_AGenesisJsonConfigWithNoDepositContractAddress_When_Parsed_Then_ItIsNull()
        {
            var root = JObject.Parse(@"{ ""config"": {} }");

            var document = StandardGenesisLoader.Parse(root);

            Assert.Null(document.Config.DepositContractAddress);
        }

        [Fact]
        public void Given_AParsedDepositContractAddress_When_AppliedToAChainConfig_Then_TheChainConfigCarriesIt()
        {
            var root = JObject.Parse(@"{ ""config"": { ""depositContractAddress"": ""0x00000000219ab540356cbb839cbe05303d7705fa"" } }");
            var document = StandardGenesisLoader.Parse(root);
            var target = new CoreChainConfig();

            StandardGenesisLoader.ApplyToChainConfig(target, document);

            Assert.Equal("0x00000000219ab540356cbb839cbe05303d7705fa", target.DepositContractAddress);
        }

        [Fact]
        public void Given_AGenesisWithNoDepositContractAddress_When_AppliedToAChainConfig_Then_TheChainConfigsExistingValueIsKept()
        {
            var root = JObject.Parse(@"{ ""config"": {} }");
            var document = StandardGenesisLoader.Parse(root);
            var target = new CoreChainConfig { DepositContractAddress = "0x00000000219ab540356cbb839cbe05303d7705fa" };

            StandardGenesisLoader.ApplyToChainConfig(target, document);

            Assert.Equal("0x00000000219ab540356cbb839cbe05303d7705fa", target.DepositContractAddress);
        }
    }
}
