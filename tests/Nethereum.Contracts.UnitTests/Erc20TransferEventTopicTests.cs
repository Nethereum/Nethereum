using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    public class Erc20TransferEventTopicTests
    {
        [Fact]
        public void Given_TheCanonicalTopic_When_ComparedToTheUnprefixedAbiForm_Then_TheyAgree()
        {
            Assert.Equal(
                ABITypedRegistry.GetEvent<TransferEventDTO>().Sha3Signature,
                Erc20TransferEventTopic.Unprefixed);
        }

        [Fact]
        public void Given_TheCanonicalTopic_When_ComparedToThePrefixedAbiForm_Then_TheyAgree()
        {
            Assert.Equal(
                (string)ABITypedRegistry.GetEvent<TransferEventDTO>().GetTopicBuilder().GetSignatureTopic(),
                Erc20TransferEventTopic.Prefixed);
        }

        [Fact]
        public void Given_TheTwoSpellings_When_Compared_Then_TheyDifferByExactlyTheOxPrefix()
        {
            Assert.Equal("0x" + Erc20TransferEventTopic.Unprefixed, Erc20TransferEventTopic.Prefixed);
            Assert.False(Erc20TransferEventTopic.Unprefixed.StartsWith("0x"));
        }

        [Fact]
        public void Given_TheCanonicalTopic_When_ItIsUsedWhereACompileTimeConstantIsRequired_Then_ItStillIs()
        {
            const string pinned = Erc20TransferEventTopic.Prefixed;

            Assert.Equal(pinned, Erc20TransferEventTopic.Prefixed);
        }

        [Fact]
        public void Given_TheKeccakOfTheEventSignature_When_Computed_Then_ItEqualsTheCanonicalTopic()
        {
            Assert.Equal(
                new Sha3Keccack().CalculateHash("Transfer(address,address,uint256)"),
                Erc20TransferEventTopic.Unprefixed);
        }
    }
}
