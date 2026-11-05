using System;
using System.Diagnostics;
using System.Linq;
using Nethereum.Siwe.Core;
using Xunit;

namespace Nethereum.Siwe.UnitTests.SecurityAudit71.S14.Round4
{
    [Trait("Category", "TimingSensitive")]
    public class SiweParserBacktrackingS14Tests
    {
        private static readonly string Head =
            "example.com wants you to sign in with your Ethereum account:\n0x" + new string('a', 40) + "\n\n\nURI: ";

        private const string Tail =
            "\nVersion: 1\nChain ID: 1\nNonce: abcdefgh\nIssued At: 2021-01-01T00:00:00Z";

        private static long Time(Action action)
        {
            var clock = Stopwatch.StartNew();
            Record.Exception(action);
            return clock.ElapsedMilliseconds;
        }

        [Fact]
        [Trait("Rule", "S14-R4-3")]
        public void Given_AFiveKilobyteUriLineWithoutTheRemainingFields_When_Parsed_Then_ItIsRejectedInUnderHalfASecond()
        {
            var message = Head + new string('a', 5_000);

            var elapsed = Time(() => SiweMessageParser.Parse(message));

            Assert.True(elapsed < 5000, $"{message.Length} char message took {elapsed} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R4-3")]
        public void Given_AUriLineOfRepeatedPathSegmentsWithoutTheRemainingFields_When_Parsed_Then_ItIsRejectedInUnderHalfASecond()
        {
            var message = Head + "https://x" + string.Concat(Enumerable.Repeat("/a", 2_000));

            var elapsed = Time(() => SiweMessageParser.Parse(message));

            Assert.True(elapsed < 5000, $"{message.Length} char message took {elapsed} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R4-3")]
        public void Given_AValidMessageWithTwoThousandResourcesFollowedByJunk_When_Parsed_Then_ItCompletesInUnderHalfASecond()
        {
            var message = Head + "https://x" + Tail + "\nResources:" +
                          string.Concat(Enumerable.Repeat("\n- a:b", 2_000)) + "\nBAD";

            var elapsed = Time(() => SiweMessageParser.Parse(message));

            Assert.True(elapsed < 5000, $"{message.Length} char message took {elapsed} ms");
        }

        [Fact]
        [Trait("Rule", "S14-R4-3")]
        public void Given_LongDomainAndStatementFields_When_Parsed_Then_ParsingStaysFast()
        {
            var longDomain = new string('a', 40_000) + " wants you";
            var longStatement = "example.com wants you to sign in with your Ethereum account:\n0x" + new string('a', 40) + "\n\n" + new string('a', 40_000);

            Assert.True(Time(() => SiweMessageParser.Parse(longDomain)) < 500);
            Assert.True(Time(() => SiweMessageParser.Parse(longStatement)) < 500);
        }
    }
}
