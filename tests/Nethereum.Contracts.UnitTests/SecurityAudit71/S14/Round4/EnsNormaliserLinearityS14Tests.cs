using System;
using System.Diagnostics;
using System.Linq;
using Nethereum.Contracts.Standards.ENS;
using Xunit;

namespace Nethereum.Contracts.UnitTests.SecurityAudit71.S14.Round4
{
    [Trait("Category", "TimingSensitive")]
    public class EnsNormaliserLinearityS14Tests
    {
        private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

        public static TheoryData<string> HostileNames() => new TheoryData<string>
        {
            Repeat("a", 200_000) + ".eth",
            Repeat("a.", 100_000) + "eth",
            "e" + Repeat("́", 50_000) + ".eth",
            Repeat("\U0001F468‍\U0001F469‍\U0001F467", 20_000) + ".eth",
            Repeat("aа", 50_000) + ".eth",
            Repeat("‍", 50_000) + "a.eth",
            Repeat("❤️", 50_000) + ".eth",
            Repeat(".", 200_000),
            Repeat("_", 200_000) + "a.eth",
            Repeat("Ａ", 100_000) + ".eth"
        };

        [Theory]
        [MemberData(nameof(HostileNames))]
        [Trait("Rule", "S14-R4-4")]
        public void Given_AHostileLongEnsName_When_NormalisedAndHashed_Then_EachCallFinishesQuickly(string name)
        {
            var util = new EnsUtil();
            var clock = Stopwatch.StartNew();

            Record.Exception(() => util.Normalise(name));
            Record.Exception(() => util.GetNameHash(name));

            Assert.True(clock.ElapsedMilliseconds < 30000, $"{name.Length} chars took {clock.ElapsedMilliseconds} ms");
        }
    }
}
