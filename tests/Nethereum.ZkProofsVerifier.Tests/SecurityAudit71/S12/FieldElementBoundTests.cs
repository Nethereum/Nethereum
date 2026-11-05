using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ZkProofsVerifier.Circom;
using Xunit;

namespace Nethereum.ZkProofsVerifier.Tests.SecurityAudit71.S12
{
    public class FieldElementBoundTests
    {
        private static string Read(string f) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", f));

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_huge_public_input_when_parsed_directly_then_it_is_rejected_with_bounded_allocation()
        {
            var json = "[\"" + new string('9', 200000) + "\"]";
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => SnarkjsPublicInputParser.Parse(json));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 16L * 1024 * 1024);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("+1")]
        [InlineData(" 1")]
        [InlineData("1 ")]
        [InlineData("0x10")]
        [InlineData("1e3")]
        [InlineData("")]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_public_input_that_is_not_plain_decimal_digits_then_it_is_rejected(string text)
        {
            Assert.ThrowsAny<Exception>(() => SnarkjsPublicInputParser.Parse("[\"" + text + "\"]"));
        }

        [Fact]
        [Trait("Rule", "S12-R2-1")]
        public void Given_a_78_digit_public_input_then_it_is_parsed_and_a_79_digit_one_is_rejected()
        {
            Assert.Single(SnarkjsPublicInputParser.Parse("[\"" + new string('9', 78) + "\"]"));
            Assert.ThrowsAny<Exception>(() => SnarkjsPublicInputParser.Parse("[\"" + new string('9', 79) + "\"]"));
        }

        private static string Many(int n)
        {
            return "[" + string.Join(",", Enumerable.Repeat("\"1\"", n)) + "]";
        }

        [Fact]
        [Trait("Rule", "S12-R3-1")]
        public void Given_a_one_million_entry_array_when_the_count_differs_from_the_vk_then_both_adapter_overloads_return_invalid_with_bounded_allocation()
        {
            var json = Many(1_000_000);
            var vkJson = Read("verification_key.json");
            var vk = SnarkjsVerificationKeyParser.Parse(vkJson);
            var proof = Read("proof.json");

            var before = GC.GetAllocatedBytesForCurrentThread();
            var a = CircomGroth16Adapter.Verify(proof, vkJson, json);
            var b = CircomGroth16Adapter.Verify(proof, vk, json);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.False(a.IsValid);
            Assert.False(b.IsValid);
            Assert.True(allocated < 40L * 2 * json.Length, "allocated " + allocated);
        }

        [Fact]
        [Trait("Rule", "S12-R3-1")]
        public void Given_the_standalone_parser_then_more_than_1024_elements_are_rejected_before_any_element_is_parsed()
        {
            Assert.Equal(1024, SnarkjsPublicInputParser.Parse(Many(1024)).Length);
            var json = Many(1_000_000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.ThrowsAny<Exception>(() => SnarkjsPublicInputParser.Parse(json));
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 40L * json.Length);
            Assert.ThrowsAny<Exception>(() => SnarkjsPublicInputParser.Parse(Many(1025)));
        }

        [Fact]
        [Trait("Rule", "S12-R2-PERF")]
        public void Given_a_parsed_verification_key_when_verifying_repeatedly_and_concurrently_then_every_valid_proof_verifies()
        {
            var vk = SnarkjsVerificationKeyParser.Parse(Read("verification_key.json"));
            var proof = Read("proof.json");
            var pub = Read("public.json");

            for (int i = 0; i < 3; i++)
                Assert.True(CircomGroth16Adapter.Verify(proof, vk, pub).IsValid);

            var results = Enumerable.Range(0, 6).Select(_ => Task.Run(() => CircomGroth16Adapter.Verify(proof, vk, pub).IsValid)).ToArray();
            Task.WaitAll(results);
            Assert.All(results, r => Assert.True(r.Result));
        }
    }
}
