using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class ProofGeneratorPathProofTests
    {
        private static readonly byte[] Key10 = KeyStartingWith(0x10);
        private static readonly byte[] Key11 = KeyStartingWith(0x11);
        private static readonly byte[] Key20 = KeyStartingWith(0x20);
        private static readonly byte[] Key555500 = KeyStartingWith(0x55, 0x55, 0x00);
        private static readonly byte[] Key555511 = KeyStartingWith(0x55, 0x55, 0x11);

        private static readonly byte[] AbsentAtEmptyBranchSlot = KeyStartingWith(0x12);
        private static readonly byte[] AbsentAtRootSlot = KeyStartingWith(0x30);
        private static readonly byte[] AbsentAtLeaf = KeyStartingWith(0x2F);
        private static readonly byte[] AbsentInsideExtension = KeyStartingWith(0x56);

        private static byte[] KeyStartingWith(params byte[] prefix)
        {
            var key = new byte[32];
            prefix.CopyTo(key, 0);
            return key;
        }

        private static byte[] ValueFor(byte[] key) => new byte[] { key[0], key[1], key[2], 0xAB, 0xCD };

        private static (PatriciaTrie trie, InMemoryContentNodeStore store, byte[] root) Build()
        {
            var store = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(store);
            foreach (var key in new[] { Key10, Key11, Key20, Key555500, Key555511 })
                trie.Put(key, ValueFor(key));
            trie.SaveNodesToStorage();
            return (trie, store, trie.Root.GetHash());
        }

        private static List<string> Hex(IEnumerable<byte[]> nodes) => nodes.Select(n => n.ToHex()).ToList();

        [Fact]
        public void Given_AKeyWhosePathEndsAtAnEmptyBranchSlot_When_ThePathProofIsGenerated_Then_ItIsTheRootToThatBranch_AndVerifiesTheKeyAbsent()
        {
            var (trie, _, root) = Build();
            var inclusionOfSibling = ProofGenerator.GenerateProof(trie, Key10);

            var proof = ProofGenerator.GeneratePathProof(trie, AbsentAtEmptyBranchSlot);

            Assert.Equal(3, inclusionOfSibling.Count);
            Assert.Equal(Hex(inclusionOfSibling.Take(2)), Hex(proof));
            Assert.Equal(root, Sha3KeccackHashProvider.Instance.ComputeHash(proof[0]));
            Assert.True(PatriciaProofVerifier.TryVerify(root, AbsentAtEmptyBranchSlot, proof, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void Given_AKeyAbsentAtTheRootBranch_When_ThePathProofIsGenerated_Then_ItIsTheRootAlone_AndVerifiesTheKeyAbsent()
        {
            var (trie, _, root) = Build();

            var proof = ProofGenerator.GeneratePathProof(trie, AbsentAtRootSlot);

            Assert.Single(proof);
            Assert.Equal(root, Sha3KeccackHashProvider.Instance.ComputeHash(proof[0]));
            Assert.True(PatriciaProofVerifier.TryVerify(root, AbsentAtRootSlot, proof, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void Given_AKeyWhosePathDivergesInsideALeaf_When_ThePathProofIsGenerated_Then_ItEndsWithThatLeaf_AndVerifiesTheKeyAbsent()
        {
            var (trie, _, root) = Build();
            var inclusionOfLeafOwner = ProofGenerator.GenerateProof(trie, Key20);

            var proof = ProofGenerator.GeneratePathProof(trie, AbsentAtLeaf);

            Assert.Equal(Hex(inclusionOfLeafOwner), Hex(proof));
            Assert.True(PatriciaProofVerifier.TryVerify(root, AbsentAtLeaf, proof, out var absent));
            Assert.Null(absent);
            Assert.True(PatriciaProofVerifier.TryVerify(root, Key20, proof, out var present));
            Assert.Equal(ValueFor(Key20), present);
        }

        [Fact]
        public void Given_AKeyWhosePathDivergesInsideAnExtension_When_ThePathProofIsGenerated_Then_ItEndsWithThatExtension_AndVerifiesTheKeyAbsent()
        {
            var (trie, _, root) = Build();
            var inclusionThroughExtension = ProofGenerator.GenerateProof(trie, Key555500);

            var proof = ProofGenerator.GeneratePathProof(trie, AbsentInsideExtension);

            Assert.Equal(4, inclusionThroughExtension.Count);
            Assert.Equal(Hex(inclusionThroughExtension.Take(2)), Hex(proof));
            Assert.True(PatriciaProofVerifier.TryVerify(root, AbsentInsideExtension, proof, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void Given_APresentKey_When_ThePathProofIsGenerated_Then_ItEqualsTheInclusionProof_AndVerifiesTheStoredValue()
        {
            var (trie, _, root) = Build();

            foreach (var key in new[] { Key10, Key11, Key20, Key555500, Key555511 })
            {
                var proof = ProofGenerator.GeneratePathProof(trie, key);

                Assert.Equal(Hex(ProofGenerator.GenerateProof(trie, key)), Hex(proof));
                Assert.True(PatriciaProofVerifier.TryVerify(root, key, proof, out var value));
                Assert.Equal(ValueFor(key), value);
            }
        }

        [Fact]
        public void Given_AnEmptyTrie_When_ThePathProofIsGenerated_Then_ItIsEmpty_AndTheEmptyRootVerifiesTheKeyAbsent()
        {
            var store = new InMemoryContentNodeStore();

            var fromFreshTrie = ProofGenerator.GeneratePathProof(new PatriciaTrie(store), Key10);
            var fromEmptyRoot = ProofGenerator.GeneratePathProof(
                PatriciaTrie.LoadFromStorage(DefaultValues.EMPTY_TRIE_HASH, store), Key10);

            Assert.Empty(fromFreshTrie);
            Assert.Empty(fromEmptyRoot);
            Assert.True(PatriciaProofVerifier.TryVerify(DefaultValues.EMPTY_TRIE_HASH, Key10, fromEmptyRoot, out var value));
            Assert.Null(value);
        }

        [Fact]
        public void Given_ANodeOnThePathMissingFromTheStore_When_ThePathProofIsGenerated_Then_ItIsNull()
        {
            var (trie, store, root) = Build();
            var rootOnly = new InMemoryContentNodeStore();
            rootOnly.Put(root, trie.Root.GetEncodedData());

            var proof = ProofGenerator.GeneratePathProof(
                PatriciaTrie.LoadFromStorage(root, rootOnly), AbsentAtEmptyBranchSlot);

            Assert.Null(proof);
        }

        [Fact]
        public void Given_AnotherKeysProof_When_VerifiedForTheQueriedKey_Then_ItIsRejectedBecauseTheQueriedPathIsNotCovered()
        {
            var (trie, _, root) = Build();
            var neighbourProof = ProofGenerator.GenerateProof(trie, Key20);
            var exclusionProof = ProofGenerator.GeneratePathProof(trie, AbsentAtEmptyBranchSlot);

            Assert.False(PatriciaProofVerifier.TryVerify(root, AbsentAtEmptyBranchSlot, neighbourProof, out _));
            Assert.False(PatriciaProofVerifier.TryVerify(root, Key10, exclusionProof, out _));
            Assert.False(PatriciaProofVerifier.TryVerify(root, AbsentAtEmptyBranchSlot, exclusionProof.Take(1).ToList(), out _));
        }

        [Fact]
        public void Given_AnExclusionProof_When_ANodeIsTamperedOrTheRootIsWrong_Then_ItIsRejected()
        {
            var (trie, _, root) = Build();
            var proof = ProofGenerator.GeneratePathProof(trie, AbsentAtEmptyBranchSlot);

            var tampered = proof.Select(n => (byte[])n.Clone()).ToList();
            tampered[tampered.Count - 1][tampered[tampered.Count - 1].Length - 1] ^= 0xFF;
            var wrongRoot = (byte[])root.Clone();
            wrongRoot[0] ^= 0xFF;

            Assert.False(PatriciaProofVerifier.TryVerify(root, AbsentAtEmptyBranchSlot, tampered, out _));
            Assert.False(PatriciaProofVerifier.TryVerify(wrongRoot, AbsentAtEmptyBranchSlot, proof, out _));
            Assert.False(PatriciaProofVerifier.TryVerify(root, AbsentAtEmptyBranchSlot, new List<byte[]>(), out _));
        }

        [Fact]
        public void Given_AnAbsentAccount_When_ItsPathProofIsVerified_Then_ItIsAbsent_AndTheAccountVerifierAcceptsOnlyTheEmptyAccount()
        {
            const string present = "0x12890d2cce102216644c59dae5baed380d84830c";
            const string absent = "0x00000000000000000000000000000000000000aa";
            var hashProvider = Sha3KeccackHashProvider.Instance;
            var accountTrie = new PatriciaTrie(new InMemoryContentNodeStore());
            accountTrie.Put(hashProvider.ComputeHash(present.HexToByteArray()),
                            AccountEncoder.Current.Encode(new Account { Nonce = 1, Balance = 5 }));
            for (var i = 0; i < 64; i++)
                accountTrie.Put(hashProvider.ComputeHash(new[] { (byte)i, (byte)0xAA }),
                                AccountEncoder.Current.Encode(new Account { Nonce = (ulong)i }));
            accountTrie.SaveNodesToStorage();
            var stateRoot = accountTrie.Root.GetHash();
            var absentKey = hashProvider.ComputeHash(absent.HexToByteArray());

            var proof = ProofGenerator.GeneratePathProof(accountTrie, absentKey);

            Assert.NotEmpty(proof);
            Assert.Null(ProofGenerator.GenerateProof(accountTrie, absentKey));
            Assert.True(PatriciaProofVerifier.TryVerify(stateRoot, absentKey, proof, out var value));
            Assert.Null(value);
            var empty = new Account { CodeHash = DefaultValues.EMPTY_DATA_HASH, StateRoot = DefaultValues.EMPTY_TRIE_HASH };
            Assert.True(Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Account.Verify(
                stateRoot, proof, absent, empty));
            empty.Balance = 1;
            Assert.False(Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Account.Verify(
                stateRoot, proof, absent, empty));
        }

        [Fact]
        public void Given_ATrieWhoseLeavesAreShorterThan32Bytes_When_APathProofIsGenerated_Then_TheEmbeddedLeavesAreNotListed_AsInGeth()
        {
            var trie = new PatriciaTrie(new InMemoryContentNodeStore());
            foreach (var key in new byte[] { 0x01, 0x12, 0x23 })
                trie.Put(new[] { key }, new[] { key });
            trie.SaveNodesToStorage();
            var root = trie.Root.GetHash();

            var present = ProofGenerator.GeneratePathProof(trie, new byte[] { 0x12 });
            var absent = ProofGenerator.GeneratePathProof(trie, new byte[] { 0x14 });

            Assert.Equal(2, ProofGenerator.GenerateProof(trie, new byte[] { 0x12 }).Count);
            Assert.Equal(Hex(new[] { trie.Root.GetEncodedData() }), Hex(present));
            Assert.Equal(Hex(new[] { trie.Root.GetEncodedData() }), Hex(absent));
            Assert.True(PatriciaProofVerifier.TryVerify(root, new byte[] { 0x12 }, present, out var value));
            Assert.Equal(new byte[] { 0x12 }, value);
            Assert.True(PatriciaProofVerifier.TryVerify(root, new byte[] { 0x14 }, absent, out var none));
            Assert.Null(none);
        }
    }
}
