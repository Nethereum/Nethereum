using System;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class TrieNodeVerificationTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private static readonly byte[] Blob = Nethereum.RLP.RLP.EncodeList(
            Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x20 }),
            Nethereum.RLP.RLP.EncodeElement(new byte[40]));

        [Fact]
        public void Given_ABlobThatHashesToTheReference_When_RequireAuthenticIsCalled_Then_ItDoesNotThrow()
        {
            var reference = new HashNode { Hash = Keccak.CalculateHash(Blob) };

            TrieNodeVerification.RequireAuthentic(reference, Blob);
        }

        [Fact]
        public void Given_ABlobThatDoesNotHashToTheReference_When_RequireAuthenticIsCalled_Then_AnInvalidOperationExceptionNamesTheHashMismatch()
        {
            var reference = new HashNode { Hash = new byte[32] };

            var refusal = Assert.Throws<InvalidOperationException>(() => TrieNodeVerification.RequireAuthentic(reference, Blob));
            Assert.Equal(
                "Path-keyed trie node hash mismatch: stored blob does not match the parent-referenced hash (tampered or corrupt).",
                refusal.Message);
        }

        [Fact]
        public void Given_ANullOrEmptyBlob_When_RequireAuthenticIsCalled_Then_ItDoesNotThrow()
        {
            var reference = new HashNode { Hash = new byte[32] };

            TrieNodeVerification.RequireAuthentic(reference, null);
            TrieNodeVerification.RequireAuthentic(reference, new byte[0]);
        }

        [Fact]
        public void Given_ARootBlobAndItsKeccak_When_RootBlobHashesToIsAsked_Then_ItIsTrueOnlyForThatRoot()
        {
            var root = Keccak.CalculateHash(Blob);

            Assert.True(TrieNodeVerification.RootBlobHashesTo(Blob, root));
            Assert.False(TrieNodeVerification.RootBlobHashesTo(Blob, new byte[32]));
        }

        [Fact]
        public void Given_ANullOrEmptyBlob_When_RootBlobHashesToIsAsked_Then_ItIsFalse()
        {
            var root = Keccak.CalculateHash(Blob);

            Assert.False(TrieNodeVerification.RootBlobHashesTo(null, root));
            Assert.False(TrieNodeVerification.RootBlobHashesTo(new byte[0], root));
        }

        [Fact]
        public void Given_ARootThatIsNullOr31Or33Bytes_When_RootBlobHashesToIsAsked_Then_ItIsFalse()
        {
            Assert.False(TrieNodeVerification.RootBlobHashesTo(Blob, null));
            Assert.False(TrieNodeVerification.RootBlobHashesTo(Blob, new byte[31]));
            Assert.False(TrieNodeVerification.RootBlobHashesTo(Blob, new byte[33]));
        }
    }
}
