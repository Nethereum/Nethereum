using System;
using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.ProofVerification;
using Nethereum.Merkle.Patricia.Proofs;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class TrieOperationsDocExampleTests
    {
        private static byte[] Key(byte first)
        {
            var key = new byte[32];
            key[0] = first;
            return key;
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "stack-trie", "Hash a sorted key stream without holding the trie")]
        public void StackTrie_HashesASortedStreamToThePatriciaRoot()
        {
            var emitted = new List<byte[]>();
            var stack = new StackTrie(null, (path, blob) => emitted.Add(blob));
            var trie = new PatriciaTrie();
            for (byte i = 1; i <= 20; i++)
            {
                var value = new byte[] { i, 0xAB };
                stack.Update(Key(i), value);
                trie.Put(Key(i), value);
            }

            var root = stack.Hash();

            Assert.Equal(trie.Root.GetHash(), root);
            Assert.Equal(root, stack.Reference());
            Assert.NotEmpty(emitted);
            Assert.Throws<ArgumentException>(() => new StackTrie().Update(new byte[31], new byte[] { 1 }));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "missing-trie-node", "A hash reference with no node in the store throws")]
        public void MissingTrieNode_IsThrownForAHashReferenceTheStoreCannotResolve()
        {
            var full = new InMemoryContentNodeStore();
            var builder = new PatriciaTrie(full);
            for (byte i = 0; i < 40; i++)
                builder.Put(Key((byte)(i * 6)), new byte[40]);
            builder.SaveDirtyNodesToStorage();
            var root = builder.Root.GetHash();

            var rootOnly = new InMemoryContentNodeStore();
            rootOnly.Put(root, full.Get(root));
            var trie = PatriciaTrie.LoadFromStorage(root, rootOnly);

            var missing = Assert.Throws<MissingTrieNodeException>(() => trie.Get(Key(0)));

            Assert.Equal(32, missing.Hash.Length);
            Assert.Null(rootOnly.Get(missing.Hash));
            Assert.NotNull(missing.Path);
            Assert.NotNull(missing.Owner);
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Refuse a blob that does not hash to its reference", Order = 2)]
        public void TrieNodeVerification_RequireAuthenticAndRootBlobHashesTo()
        {
            var blob = Nethereum.RLP.RLP.EncodeList(
                Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x20 }),
                Nethereum.RLP.RLP.EncodeElement(new byte[40]));
            var hash = new Sha3Keccack().CalculateHash(blob);

            TrieNodeVerification.RequireAuthentic(new HashNode { Hash = hash }, blob);
            Assert.Throws<InvalidOperationException>(
                () => TrieNodeVerification.RequireAuthentic(new HashNode { Hash = new byte[32] }, blob));
            Assert.True(TrieNodeVerification.RootBlobHashesTo(blob, hash));
            Assert.False(TrieNodeVerification.RootBlobHashesTo(blob, new byte[32]));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "compact-encoding", "Hex-prefix encode a nibble path as extension or leaf")]
        public void NibblesToCompact_SetsTheLeafAndOddFlags()
        {
            var nibbles = new byte[] { 1, 2, 3 };

            Assert.Equal(new byte[] { 0x11, 0x23 }, PatriciaPathWalker.NibblesToCompact(nibbles, false));
            Assert.Equal(new byte[] { 0x31, 0x23 }, PatriciaPathWalker.NibblesToCompact(nibbles, true));
            Assert.Equal(new byte[] { 0x20 }, PatriciaPathWalker.NibblesToCompact(new byte[0], true));
            Assert.Equal(PatriciaPathWalker.NibblesToCompact(nibbles, false), PatriciaPathWalker.NibblesToCompact(nibbles));
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "proof-verification", "Malformed proofs are refused, not thrown")]
        public void Verifiers_ReturnFalseForMalformedProofs()
        {
            var root = new byte[32];
            var proof = new List<byte[]> { new byte[] { 0xC0 } };

            Assert.False(Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Range.VerifyEntry(root, Key(1), new byte[] { 1 }, proof));
            Assert.False(Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Storage.Verify(root, proof, new byte[32], new byte[] { 1 }));
            Assert.False(Nethereum.Merkle.Patricia.ProofVerification.ProofVerification.Current.Range.Verify(root, Key(1), new List<byte[]> { Key(1) }, new List<byte[]> { new byte[] { 1 } }, proof).Valid);
        }
    }
}
