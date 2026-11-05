using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ZkProofs;
using Xunit;

namespace Nethereum.PrivacyPools.Tests.SecurityAudit71.S12.Round4
{
    public class PublicSignalsRound4Tests
    {
        private static string ArrayOf(int n) => "[" + string.Join(",", Enumerable.Repeat("\"1\"", n)) + "]";

        [Fact]
        [Trait("Rule", "S12-R4-30")]
        public void Given_a_public_signals_array_over_the_cap_then_it_is_refused_with_bounded_allocation()
        {
            var json = ArrayOf(1_000_000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => ZkProofResult.ParsePublicSignals(json));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 20L * json.Length, "allocated " + allocated);
        }

        [Fact]
        [Trait("Rule", "S12-R4-30")]
        public void Given_signal_arrays_at_the_cap_then_they_parse_and_junk_documents_throw_cleanly()
        {
            Assert.Equal(1024, ZkProofResult.ParsePublicSignals(ArrayOf(1024)).Length);
            Assert.Empty(ZkProofResult.ParsePublicSignals(""));
            foreach (var junk in new[] { "{}", "null", "[[\"1\"]]", "[\"1\"] x", "[1]", new string('[', 100000), "[\"" + new string('9', 79) + "\"]" })
                Assert.ThrowsAny<Exception>(() => ZkProofResult.ParsePublicSignals(junk));
        }
    }
}
