using System;
using System.Diagnostics;
using System.Linq;
using Nethereum.DID;
using Xunit;

namespace Nethereum.DID.Tests.SecurityAudit71.S14.Round4
{
    [Trait("Category", "TimingSensitive")]
    public class DidUrlParserLinearityS14Tests
    {
        private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

        public static TheoryData<string> HostileUrls() => new TheoryData<string>
        {
            "did:ex:" + Repeat("a:", 20_000) + " ",
            "did:ex:" + new string('a', 40_000) + " ",
            "did:ex:" + Repeat("%41", 20_000) + " ",
            "did:ex:a" + Repeat(";a=b", 20_000) + " ",
            "did:ex:" + Repeat("a.:", 20_000) + Repeat(";a=b", 10) + "? ",
            "did:" + new string('a', 40_000) + ":x",
            "did:ex:a" + Repeat("/a", 20_000) + "?" + Repeat("q", 20_000) + "#" + Repeat("f", 20_000) + "\u0000"
        };

        [Theory]
        [MemberData(nameof(HostileUrls))]
        [Trait("Rule", "S14-R4-4")]
        public void Given_AHostileDidUrl_When_Parsed_Then_ItFinishesQuickly(string url)
        {
            var clock = Stopwatch.StartNew();

            Record.Exception(() => DidUrlParser.Parse(url));

            Assert.True(clock.ElapsedMilliseconds < 15000, $"{url.Length} chars took {clock.ElapsedMilliseconds} ms");
        }
    }
}
