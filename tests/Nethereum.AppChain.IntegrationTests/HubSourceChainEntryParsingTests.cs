using Nethereum.AppChain.Server;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class HubSourceChainEntryParsingTests
    {
        [Fact]
        public void Given_AHubSourceChainEntryWithAPipeDelimiter_When_ParseHubSourceChainEntryRuns_Then_ItSplitsChainIdRpcUrlAndHubAddress()
        {
            var parsed = AppChainServerRunner.ParseHubSourceChainEntry("11155111|https://sepolia.example.org|0x1234567890123456789012345678901234567890");

            Assert.NotNull(parsed);
            Assert.Equal((ulong)11155111, parsed!.ChainId);
            Assert.Equal("https://sepolia.example.org", parsed.RpcUrl);
            Assert.Equal("0x1234567890123456789012345678901234567890", parsed.HubContractAddress);
        }

        [Fact]
        public void Given_AColonDelimitedEntryWithAnEmbeddedSchemeSeparator_When_ParseHubSourceChainEntryRuns_Then_ItSplitsChainIdRpcUrlAndHubAddress()
        {
            var parsed = AppChainServerRunner.ParseHubSourceChainEntry("11155111:https://sepolia.example.org:0x1234567890123456789012345678901234567890");

            Assert.NotNull(parsed);
            Assert.Equal((ulong)11155111, parsed!.ChainId);
            Assert.Equal("https://sepolia.example.org", parsed.RpcUrl);
            Assert.Equal("0x1234567890123456789012345678901234567890", parsed.HubContractAddress);
        }

        [Fact]
        public void Given_AMalformedHubSourceChainEntry_When_ParseHubSourceChainEntryRuns_Then_ItReturnsNull()
        {
            var parsed = AppChainServerRunner.ParseHubSourceChainEntry("not-a-valid-entry");

            Assert.Null(parsed);
        }

        [Fact]
        public void Given_AnEntryWithAPipe_When_TrySplitPipeDelimitedRuns_Then_ItSplitsOnThePipeCharacter()
        {
            var parts = AppChainServerRunner.TrySplitPipeDelimited("a|b|c");

            Assert.Equal(new[] { "a", "b", "c" }, parts);
        }

        [Fact]
        public void Given_AColonEntryWithAnEmbeddedSchemeSeparator_When_TrySplitColonDelimitedRuns_Then_ItTreatsTheLastColonBefore0xAsTheFinalSeparator()
        {
            var parts = AppChainServerRunner.TrySplitColonDelimited("11155111:https://sepolia.example.org:0xabc");

            Assert.Equal(new[] { "11155111", "https://sepolia.example.org", "0xabc" }, parts);
        }
    }
}
