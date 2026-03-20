using System;
using System.Collections.Generic;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class ProofTests
    {
        private static byte[] MakeKey(byte prefix, byte leafIdx)
        {
            var k = new byte[32];
            k[0] = prefix;
            k[31] = leafIdx;
            return k;
        }

        private static byte[] MakeVal(byte b)
        {
            var v = new byte[32];
            v[0] = b;
            return v;
        }

        private BinaryTrie CreateTrie(IHashProvider hp = null)
        {
            return new BinaryTrie(hp ?? new Sha256HashProvider());
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_SingleEntry_Verifies()
        {
            var trie = CreateTrie();
            var key = MakeKey(0x00, 1);
            var val = MakeVal(0xAA);
            trie.Put(key, val);

            var prover = new BinaryTrieProver(trie);
            var proof = prover.BuildProof(key);
            Assert.NotNull(proof);
            Assert.NotEmpty(proof.Nodes);

            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
            Assert.NotNull(result);
            Assert.Equal(val, result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_MultiLevel_AllVerify()
        {
            var trie = CreateTrie();
            var keys = new[] { MakeKey(0x00, 1), MakeKey(0x80, 2), MakeKey(0x40, 3) };
            var vals = new[] { MakeVal(0x11), MakeVal(0x22), MakeVal(0x33) };
            for (int i = 0; i < 3; i++) trie.Put(keys[i], vals[i]);

            var root = trie.ComputeRoot();
            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            for (int i = 0; i < 3; i++)
            {
                var proof = prover.BuildProof(keys[i]);
                var result = verifier.VerifyProof(root, keys[i], proof);
                Assert.NotNull(result);
                Assert.Equal(vals[i], result);
            }
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_MissingKey_ReturnsNull()
        {
            var trie = CreateTrie();
            trie.Put(MakeKey(0x00, 1), MakeVal(0xDD));

            var missing = MakeKey(0x80, 1);
            var prover = new BinaryTrieProver(trie);
            var proof = prover.BuildProof(missing);

            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(trie.ComputeRoot(), missing, proof);
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_TamperedNode_FailsVerification()
        {
            var trie = CreateTrie();
            trie.Put(MakeKey(0x00, 1), MakeVal(0xFF));
            trie.Put(MakeKey(0x80, 2), MakeVal(0xFF));

            var prover = new BinaryTrieProver(trie);
            var key = MakeKey(0x00, 1);
            var proof = prover.BuildProof(key);

            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
            Assert.NotNull(result);

            if (proof.Nodes.Length > 0)
            {
                proof.Nodes[0][1] ^= 0x01;
                var tampered = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
                Assert.Null(tampered);
            }
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_EmptyTrie_ReturnsNull()
        {
            var trie = CreateTrie();
            var prover = new BinaryTrieProver(trie);
            var key = MakeKey(0x42, 5);
            var proof = prover.BuildProof(key);

            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_SingleElement_VerifiesDirectly()
        {
            var trie = CreateTrie();
            var key = MakeKey(0x10, 7);
            var val = MakeVal(0xBB);
            trie.Put(key, val);

            var prover = new BinaryTrieProver(trie);
            var proof = prover.BuildProof(key);
            Assert.Single(proof.Nodes);

            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(trie.ComputeRoot(), key, proof);
            Assert.NotNull(result);
            Assert.Equal(val, result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_DeepPath_HasMultipleNodes()
        {
            var trie = CreateTrie();
            var k1 = new byte[32]; k1[0] = 0x00; k1[31] = 1;
            var k2 = new byte[32]; k2[0] = 0x01; k2[31] = 2;
            trie.Put(k1, MakeVal(0xCC));
            trie.Put(k2, MakeVal(0xCC));

            var prover = new BinaryTrieProver(trie);
            var proof = prover.BuildProof(k1);
            Assert.True(proof.Nodes.Length > 1);

            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(trie.ComputeRoot(), k1, proof);
            Assert.NotNull(result);
            Assert.Equal(MakeVal(0xCC), result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_MultipleKeys_SameRoot_AllVerify()
        {
            var trie = CreateTrie();
            var keys = new byte[5][];
            var vals = new byte[5][];
            for (int i = 0; i < 5; i++)
            {
                keys[i] = MakeKey((byte)(i * 0x30), (byte)i);
                vals[i] = MakeVal((byte)(i + 1));
                trie.Put(keys[i], vals[i]);
            }

            var root = trie.ComputeRoot();
            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            for (int i = 0; i < 5; i++)
            {
                var proof = prover.BuildProof(keys[i]);
                var result = verifier.VerifyProof(root, keys[i], proof);
                Assert.NotNull(result);
                Assert.Equal(vals[i], result);
            }
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_WrongRoot_FailsVerification()
        {
            var trie = CreateTrie();
            var key = MakeKey(0xAB, 3);
            trie.Put(key, MakeVal(0xCD));

            var prover = new BinaryTrieProver(trie);
            var proof = prover.BuildProof(key);

            var wrongRoot = new Sha256HashProvider().ComputeHash(new byte[] { 0x01 });
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var result = verifier.VerifyProof(wrongRoot, key, proof);
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_NullInputs_ReturnsNull()
        {
            var verifier = new BinaryTrieProofVerifier(new Sha256HashProvider());
            var root = new Sha256HashProvider().ComputeHash(new byte[] { 0x01 });
            Assert.Null(verifier.VerifyProof(root, MakeKey(0, 0), null));
            Assert.Null(verifier.VerifyProof(root, MakeKey(0, 0), new BinaryTrieProof { Nodes = Array.Empty<byte[]>() }));
            Assert.Null(verifier.VerifyProof(null, MakeKey(0, 0), new BinaryTrieProof { Nodes = new byte[][] { new byte[1] } }));
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_Blake3_Verifies()
        {
            var trie = new BinaryTrie(new Blake3HashProvider());
            var key1 = MakeKey(0x00, 1);
            var key2 = MakeKey(0x80, 2);
            trie.Put(key1, MakeVal(0xAA));
            trie.Put(key2, MakeVal(0xBB));

            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var root = trie.ComputeRoot();

            var proof1 = prover.BuildProof(key1);
            Assert.Equal(MakeVal(0xAA), verifier.VerifyProof(root, key1, proof1));

            var proof2 = prover.BuildProof(key2);
            Assert.Equal(MakeVal(0xBB), verifier.VerifyProof(root, key2, proof2));
        }

        [Fact]
        [Trait("Category", "Proofs-ethereumjs")]
        public void Proof_100Entries_Blake3_AllVerify()
        {
            var hp = new Blake3HashProvider();
            var trie = new BinaryTrie(hp);

            var keys = new byte[100][];
            var vals = new byte[100][];

            for (int i = 0; i < 100; i++)
            {
                keys[i] = hp.ComputeHash(new byte[] { (byte)i });
                vals[i] = new byte[32];
                vals[i][0] = (byte)(i + 1);
                trie.Put(keys[i], vals[i]);
            }

            var root = trie.ComputeRoot();
            Assert.False(BinaryTrieConstants.IsZeroHash(root));

            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(hp);

            var proof0 = prover.BuildProof(keys[0]);
            Assert.True(proof0.Nodes.Length > 0);

            var firstNodeDecoded = CompactBinaryNodeCodec.Decode(proof0.Nodes[0], 0);
            var firstNodeHash = firstNodeDecoded.ComputeHash(hp);
            Assert.Equal(root, firstNodeHash);

            var lastNodeDecoded = CompactBinaryNodeCodec.Decode(proof0.Nodes[proof0.Nodes.Length - 1], 0);
            Assert.IsType<StemBinaryNode>(lastNodeDecoded);

            var result0 = verifier.VerifyProof(root, keys[0], proof0);
            Assert.NotNull(result0);
            Assert.Equal(vals[0], result0);

            for (int i = 1; i < 100; i++)
            {
                var proof = prover.BuildProof(keys[i]);
                var result = verifier.VerifyProof(root, keys[i], proof);
                Assert.NotNull(result);
                Assert.Equal(vals[i], result);
            }
        }

        [Fact]
        [Trait("Category", "Proofs-ethereumjs")]
        public void Proof_NonExistence_FakeKey_ReturnsNull()
        {
            var hp = new Blake3HashProvider();
            var trie = new BinaryTrie(hp);

            for (int i = 0; i < 100; i++)
            {
                var key = hp.ComputeHash(new byte[] { (byte)i });
                var val = new byte[32];
                val[0] = (byte)(i + 1);
                trie.Put(key, val);
            }

            var root = trie.ComputeRoot();
            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(hp);

            var fakeKey = new byte[32];
            for (int i = 0; i < 32; i++) fakeKey[i] = 0x05;

            var proof = prover.BuildProof(fakeKey);
            var result = verifier.VerifyProof(root, fakeKey, proof);
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "Proofs-ethereumjs")]
        public void Proof_MultiValueSameStem_ContainsFullStemNode()
        {
            var trie = CreateTrie();

            var stem = new byte[BinaryTrieConstants.StemSize];
            byte[] suffixes = { 0x03, 0x04, 0x09, 0xFF };
            var values = new byte[suffixes.Length][];

            for (int i = 0; i < suffixes.Length; i++)
            {
                var key = new byte[32];
                Array.Copy(stem, 0, key, 0, BinaryTrieConstants.StemSize);
                key[31] = suffixes[i];
                values[i] = MakeVal((byte)(0x10 + i));
                trie.Put(key, values[i]);
            }

            var root = trie.ComputeRoot();
            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);

            var proofKey = new byte[32];
            Array.Copy(stem, 0, proofKey, 0, BinaryTrieConstants.StemSize);
            proofKey[31] = 0x03;

            var proof = prover.BuildProof(proofKey);

            var lastNode = CompactBinaryNodeCodec.Decode(proof.Nodes[proof.Nodes.Length - 1], 0);
            Assert.IsType<StemBinaryNode>(lastNode);
            var stemNode = (StemBinaryNode)lastNode;
            Assert.Equal(stem, stemNode.Stem);

            for (int i = 0; i < suffixes.Length; i++)
            {
                Assert.NotNull(stemNode.Values[suffixes[i]]);
                Assert.Equal(values[i], stemNode.Values[suffixes[i]]);
            }

            var result = verifier.VerifyProof(root, proofKey, proof);
            Assert.NotNull(result);
            Assert.Equal(values[0], result);

            for (int i = 1; i < suffixes.Length; i++)
            {
                var otherKey = new byte[32];
                Array.Copy(stem, 0, otherKey, 0, BinaryTrieConstants.StemSize);
                otherKey[31] = suffixes[i];
                var otherResult = verifier.VerifyProof(root, otherKey, proof);
                Assert.NotNull(otherResult);
                Assert.Equal(values[i], otherResult);
            }
        }

        [Fact]
        [Trait("Category", "Proofs-ethereumjs")]
        public void Proof_ColocatedBlake3Keys_AllVerify()
        {
            var hp = new Blake3HashProvider();
            var trie = new BinaryTrie(hp);

            var keys = new byte[100][];
            var vals = new byte[100][];

            for (int i = 0; i < 100; i++)
            {
                keys[i] = hp.ComputeHash(new byte[] { (byte)i });
                vals[i] = new byte[32];
                vals[i][0] = (byte)(i + 1);
                trie.Put(keys[i], vals[i]);
            }

            var root = trie.ComputeRoot();
            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(hp);

            var stemGroups = new Dictionary<string, List<int>>();
            for (int i = 0; i < 100; i++)
            {
                var stemHex = BitConverter.ToString(keys[i], 0, BinaryTrieConstants.StemSize);
                if (!stemGroups.ContainsKey(stemHex))
                    stemGroups[stemHex] = new List<int>();
                stemGroups[stemHex].Add(i);
            }

            for (int i = 0; i < 100; i++)
            {
                var proof = prover.BuildProof(keys[i]);
                var result = verifier.VerifyProof(root, keys[i], proof);
                Assert.NotNull(result);
                Assert.Equal(vals[i], result);
            }
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void Proof_SuffixValueNull_ReturnsNull()
        {
            var trie = CreateTrie();
            var stem = new byte[BinaryTrieConstants.StemSize];
            var key1 = new byte[32];
            Array.Copy(stem, 0, key1, 0, BinaryTrieConstants.StemSize);
            key1[31] = 0x05;
            trie.Put(key1, MakeVal(0xAA));

            var prover = new BinaryTrieProver(trie);
            var verifier = new BinaryTrieProofVerifier(trie.HashProvider);
            var root = trie.ComputeRoot();

            var key2 = new byte[32];
            Array.Copy(stem, 0, key2, 0, BinaryTrieConstants.StemSize);
            key2[31] = 0x06;

            var proof = prover.BuildProof(key2);
            var result = verifier.VerifyProof(root, key2, proof);
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void FindPath_ReturnsCorrectNodes()
        {
            var trie = CreateTrie();
            var key1 = MakeKey(0x00, 1);
            var key2 = MakeKey(0x80, 2);
            trie.Put(key1, MakeVal(0x11));
            trie.Put(key2, MakeVal(0x22));

            var stem = new byte[BinaryTrieConstants.StemSize];
            Array.Copy(key1, 0, stem, 0, BinaryTrieConstants.StemSize);
            var path = trie.FindPath(stem);

            Assert.True(path.Count >= 2);
            Assert.IsType<InternalBinaryNode>(path[0]);
            Assert.IsType<StemBinaryNode>(path[path.Count - 1]);
        }

        [Fact]
        [Trait("Category", "Proofs")]
        public void FindPath_EmptyTrie_ReturnsEmptyNode()
        {
            var trie = CreateTrie();
            var stem = new byte[BinaryTrieConstants.StemSize];
            var path = trie.FindPath(stem);

            Assert.Single(path);
            Assert.IsType<EmptyBinaryNode>(path[0]);
        }
    }
}
