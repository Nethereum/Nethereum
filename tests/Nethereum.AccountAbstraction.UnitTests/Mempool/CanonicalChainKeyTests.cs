using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Structs;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Mempool
{
    public class CanonicalChainKeyTests
    {
        private const string Sender = "0x1111111111111111111111111111111111111111";
        private const string EntryPointLower = "0x0000000000000000000000000000000000000007";
        private const string OtherEntryPoint = "0x0000000000000000000000000000000000000008";

        [Fact]
        public void Of_SameSenderAndNonceKey_EntryPointDifferingOnlyByCase_ProducesEqualChainKey()
        {
            var lower = CreateEntry(Sender, EntryPointLower.ToLowerInvariant(), nonce: 0);
            var upper = CreateEntry(Sender, EntryPointLower.ToUpperInvariant(), nonce: 0);

            var keyLower = MempoolChainKey.Of(lower);
            var keyUpper = MempoolChainKey.Of(upper);

            Assert.Equal(keyLower, keyUpper);
            Assert.Equal(keyLower.EntryPoint, keyUpper.EntryPoint);
        }

        [Fact]
        public void Of_SameSenderAndNonceKey_DifferentEntryPointAddress_ProducesDifferentChainKey()
        {
            var onFirstEntryPoint = CreateEntry(Sender, EntryPointLower, nonce: 0);
            var onSecondEntryPoint = CreateEntry(Sender, OtherEntryPoint, nonce: 0);

            var keyFirst = MempoolChainKey.Of(onFirstEntryPoint);
            var keySecond = MempoolChainKey.Of(onSecondEntryPoint);

            Assert.NotEqual(keyFirst, keySecond);
            Assert.Equal(keyFirst.Sender, keySecond.Sender);
            Assert.Equal(keyFirst.NonceKey, keySecond.NonceKey);
            Assert.NotEqual(keyFirst.EntryPoint, keySecond.EntryPoint);
        }

        [Fact]
        public void Of_DifferentEntryPointAddress_NeverCollidesRegardlessOfCasing()
        {
            var evicted = CreateEntry(Sender, EntryPointLower, nonce: 0);
            var onOtherEntryPointUpperCased = CreateEntry(Sender, OtherEntryPoint.ToUpperInvariant(), nonce: 1);

            Assert.NotEqual(MempoolChainKey.Of(evicted), MempoolChainKey.Of(onOtherEntryPointUpperCased));
        }

        [Fact]
        public void Of_DifferentSender_SameNonceKeyAndEntryPoint_ProducesDifferentChainKey()
        {
            var senderA = CreateEntry(Sender, EntryPointLower, nonce: 0);
            var senderB = CreateEntry("0x2222222222222222222222222222222222222222", EntryPointLower, nonce: 0);

            var keyA = MempoolChainKey.Of(senderA);
            var keyB = MempoolChainKey.Of(senderB);

            Assert.NotEqual(keyA, keyB);
            Assert.NotEqual(keyA.Sender, keyB.Sender);
            Assert.Equal(keyA.NonceKey, keyB.NonceKey);
            Assert.Equal(keyA.EntryPoint, keyB.EntryPoint);
        }

        [Fact]
        public void Of_NullSenderAndEntryPoint_DoesNotThrowAndYieldsEmptyAddressFields()
        {
            var entry = CreateEntry(sender: null, entryPoint: null, nonce: 0);

            var key = MempoolChainKey.Of(entry);

            Assert.Equal(string.Empty, key.Sender);
            Assert.Equal(string.Empty, key.EntryPoint);
            Assert.Equal(BigInteger.Zero, key.NonceKey);
        }

        [Fact]
        public void BuildContiguousChains_MixedCaseEntryPointOnContiguousRun_GroupsAsOneChain()
        {
            var entries = new[]
            {
                CreateEntry(Sender, EntryPointLower.ToLowerInvariant(), nonce: 0),
                CreateEntry(Sender, EntryPointLower.ToUpperInvariant(), nonce: 1),
                CreateEntry(Sender, EntryPointLower.ToLowerInvariant(), nonce: 2),
            };

            var chains = MempoolPendingChains.BuildContiguousChains(entries);

            var chain = Assert.Single(chains);
            Assert.Equal(3, chain.Value.Length);
            Assert.Equal(new BigInteger[] { 0, 1, 2 }, chain.Value.Select(e => e.UserOperation.Nonce));
        }

        [Fact]
        public void BuildContiguousChains_SameSenderAndNonceKeyOnDifferentEntryPoints_KeepsSeparateChains()
        {
            var entries = new[]
            {
                CreateEntry(Sender, EntryPointLower, nonce: 0),
                CreateEntry(Sender, OtherEntryPoint, nonce: 0),
            };

            var chains = MempoolPendingChains.BuildContiguousChains(entries);

            Assert.Equal(2, chains.Count);
            foreach (var chain in chains.Values)
            {
                Assert.Single(chain);
            }
        }

        private static MempoolEntry CreateEntry(string sender, string entryPoint, int nonce)
        {
            return new MempoolEntry
            {
                UserOpHash = $"0x{sender}-{entryPoint}-{nonce}",
                EntryPoint = entryPoint,
                Priority = 1,
                UserOperation = new PackedUserOperation
                {
                    Sender = sender,
                    Nonce = nonce
                }
            };
        }
    }
}
