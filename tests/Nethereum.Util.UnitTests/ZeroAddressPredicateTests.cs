using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class ZeroAddressPredicateTests
    {
        private const string ZeroAddress = "0x0000000000000000000000000000000000000000";
        private const string UnprefixedZeros = "0000000000000000000000000000000000000000";
        private const string ZeroAsA32ByteTopic = "0x0000000000000000000000000000000000000000000000000000000000000000";
        private const string RealAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", false)]
        [InlineData("0x", true)]
        [InlineData("0x0", true)]
        [InlineData("0x00", true)]
        [InlineData(ZeroAddress, true)]
        [InlineData(UnprefixedZeros, true)]
        [InlineData(ZeroAsA32ByteTopic, false)]
        [InlineData(RealAddress, false)]
        public void Given_EachShape_When_AskedIfNullEmptyOrZero_Then_ItNormalisesToTwentyBytesBeforeComparing(
            string address, bool expected)
        {
            Assert.Equal(expected, AddressUtil.Current.IsNullEmptyOrZeroAddress(address));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", false)]
        [InlineData("0x", true)]
        [InlineData("0x0", true)]
        [InlineData("0x00", true)]
        [InlineData(ZeroAddress, true)]
        [InlineData(UnprefixedZeros, true)]
        [InlineData(ZeroAsA32ByteTopic, true)]
        [InlineData(RealAddress, false)]
        public void Given_EachShape_When_AskedIfNullEmptyOrAllZeroHex_Then_WidthDoesNotMatter(
            string address, bool expected)
        {
            Assert.Equal(expected, AddressUtil.Current.IsNullEmptyOrAllZeroHex(address));
        }

        [Fact]
        public void Given_AZeroAddressWrittenAsA32ByteTopic_When_Asked_Then_OnlyTheWidthAgnosticPredicateSeesIt()
        {
            Assert.False(AddressUtil.Current.IsNullEmptyOrZeroAddress(ZeroAsA32ByteTopic),
                "left-padding to twenty bytes cannot shorten a thirty-two byte string, so a padded " +
                "topic is not the zero address to a predicate that normalises");

            Assert.True(AddressUtil.Current.IsNullEmptyOrAllZeroHex(ZeroAsA32ByteTopic),
                "a token feed that stores the raw topic still has to recognise the mint and burn address");
        }

        [Fact]
        public void Given_TheZeroAddress_When_AskedIfAbsent_Then_No_BecauseAbsentAndZeroAreDifferentQuestions()
        {
            Assert.False(ZeroAddress.IsAnEmptyAddress());
            Assert.True(AddressUtil.Current.IsZeroAddress(ZeroAddress));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Given_AnAbsentAddress_When_AskedIfZero_Then_No_BecauseAbsentIsNotZero(string address)
        {
            Assert.False(AddressUtil.Current.IsZeroAddress(address));
            Assert.True(address.IsAnEmptyAddress());
        }
    }
}
