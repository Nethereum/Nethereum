using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.PrivacyPools.Tests.SecurityAudit71.S12.Round3
{
    public class CachedVerificationKeyRound3Tests
    {
        private static string FixturePath(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Nethereum.ZkProofsVerifier.Tests", "TestData", name);
                if (File.Exists(candidate)) return candidate;
                var sibling = Path.Combine(dir.FullName, "tests", "Nethereum.ZkProofsVerifier.Tests", "TestData", name);
                if (File.Exists(sibling)) return sibling;
                dir = dir.Parent;
            }
            throw new FileNotFoundException(name);
        }

        private static string Read(string name) => File.ReadAllText(FixturePath(name));

        private const string Tampered = "[\"34\",\"3\",\"11\"]";

        [Fact]
        [Trait("Rule", "S12-R3-10")]
        public void Given_fresh_verifiers_when_many_threads_make_the_first_call_together_then_every_verdict_is_correct()
        {
            var vk = Read("verification_key.json");
            var proof = Read("proof.json");
            var good = Read("public.json");

            for (int round = 0; round < 12; round++)
            {
                var verifier = new PrivacyPoolProofVerifier(vk);
                using var barrier = new Barrier(12);
                var results = new bool[12];
                var threads = Enumerable.Range(0, 12).Select(i => new Thread(() =>
                {
                    barrier.SignalAndWait();
                    var r = verifier.VerifyWithdrawalProof(proof, i % 2 == 0 ? good : Tampered);
                    results[i] = r.IsValid == (i % 2 == 0);
                })).ToList();
                threads.ForEach(t => t.Start());
                threads.ForEach(t => t.Join());
                Assert.All(results, Assert.True);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R3-11")]
        public void Given_a_verifier_with_an_unparseable_vk_then_every_call_is_invalid_and_never_throws()
        {
            var verifier = new PrivacyPoolProofVerifier("{ \"not\": \"a vk\" }");
            for (int i = 0; i < 5; i++)
            {
                var ex = Record.Exception(() => Assert.False(verifier.VerifyWithdrawalProof(Read("proof.json"), Read("public.json")).IsValid));
                Assert.Null(ex);
            }
        }

        [Fact]
        [Trait("Rule", "S12-R3-11")]
        public void Given_a_verifier_with_a_vk_whose_point_is_outside_the_subgroup_then_it_never_accepts()
        {
            var vk = Read("verification_key.json");
            var corrupted = System.Text.RegularExpressions.Regex.Replace(vk, "\"vk_alpha_1\"\\s*:\\s*\\[\\s*\"(\\d+)\"", m => m.Value.Replace(m.Groups[1].Value, "1"));
            Assert.NotEqual(vk, corrupted);

            var verifier = new PrivacyPoolProofVerifier(corrupted);
            for (int i = 0; i < 3; i++)
                Assert.False(verifier.VerifyWithdrawalProof(Read("proof.json"), Read("public.json")).IsValid);
        }

        [Fact]
        [Trait("Rule", "S12-R3-12")]
        public void Given_a_verifier_without_a_ragequit_vk_then_ragequit_verification_still_throws_invalid_operation()
        {
            var verifier = new PrivacyPoolProofVerifier(Read("verification_key.json"));
            Assert.Throws<InvalidOperationException>(() => verifier.VerifyRagequitProof(Read("proof.json"), Read("public.json")));
        }

        [Fact]
        [Trait("Rule", "S12-R3-13")]
        public void Given_one_verifier_when_used_repeatedly_and_concurrently_then_the_cached_key_gives_the_same_verdicts_as_the_uncached_path()
        {
            var vk = Read("verification_key.json");
            var proof = Read("proof.json");
            var good = Read("public.json");
            var verifier = new PrivacyPoolProofVerifier(vk);

            var uncachedGood = Nethereum.ZkProofsVerifier.Circom.CircomGroth16Adapter.Verify(proof, vk, good).IsValid;
            var uncachedBad = Nethereum.ZkProofsVerifier.Circom.CircomGroth16Adapter.Verify(proof, vk, Tampered).IsValid;

            var results = new bool[40];
            Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 10 }, i =>
            {
                var cached = verifier.VerifyWithdrawalProof(proof, i % 2 == 0 ? good : Tampered).IsValid;
                results[i] = cached == (i % 2 == 0 ? uncachedGood : uncachedBad);
            });

            Assert.All(results, Assert.True);
        }
    }
}
