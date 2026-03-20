using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Ssz.Tests
{
    public class SszMerkleizerProgressiveTests
    {
        private static byte[] MakeChunk(byte fill)
        {
            return Enumerable.Repeat(fill, 32).ToArray();
        }

        private static byte[] Sha256(byte[] left, byte[] right)
        {
            using var sha = SHA256.Create();
            var concat = new byte[64];
            Buffer.BlockCopy(left, 0, concat, 0, 32);
            Buffer.BlockCopy(right, 0, concat, 32, 32);
            return sha.ComputeHash(concat);
        }

        private static readonly byte[] Zero = new byte[32];

        // --- Merkleize with limit ---

        [Fact]
        public void Merkleize_WithLimit_SingleChunkLimit1_ReturnsChunkDirectly()
        {
            // merkleize([chunk], limit=1): next_pow2(1)=1, single leaf → returns chunk itself
            var chunk = MakeChunk(0xAA);
            var result = SszMerkleizer.Merkleize(new List<byte[]> { chunk }, 1);
            Assert.Equal(chunk, result);
        }

        [Fact]
        public void Merkleize_WithLimit_SingleChunkLimit4_PadsToFourLeaves()
        {
            var chunk = MakeChunk(0xBB);
            var result = SszMerkleizer.Merkleize(new List<byte[]> { chunk }, 4);

            // 4 leaves padded to next power of 2 (4 is already power of 2)
            // Tree: hash(hash(chunk, zero), hash(zero, zero))
            var left = Sha256(chunk, Zero);
            var right = Sha256(Zero, Zero);
            var expected = Sha256(left, right);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Merkleize_WithLimit_EmptyChunksLimit0_ReturnsZero()
        {
            var result = SszMerkleizer.Merkleize(new List<byte[]>(), 0);
            Assert.Equal(Zero, result);
        }

        [Fact]
        public void Merkleize_WithLimit_ThrowsIfChunksExceedLimit()
        {
            var chunks = new List<byte[]> { MakeChunk(0x01), MakeChunk(0x02), MakeChunk(0x03) };
            Assert.Throws<ArgumentException>(() => SszMerkleizer.Merkleize(chunks, 2));
        }

        // --- MerkleizeProgressive ---

        [Fact]
        public void MerkleizeProgressive_EmptyChunks_ReturnsZero()
        {
            var result = SszMerkleizer.MerkleizeProgressive(new List<byte[]>());
            Assert.Equal(Zero, result);
        }

        [Fact]
        public void MerkleizeProgressive_SingleChunk_HashRestAndSubtree()
        {
            // merkleize_progressive([f0], num_leaves=1):
            //   a = merkleize([f0], 1) = f0  (single leaf, no hashing)
            //   b = merkleize_progressive([], 4) = zero
            //   result = hash(zero, f0)
            var f0 = MakeChunk(0x42);
            var result = SszMerkleizer.MerkleizeProgressive(new List<byte[]> { f0 });

            var expected = Sha256(Zero, f0);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MerkleizeProgressive_TwoChunks_CorrectStructure()
        {
            // merkleize_progressive([f0, f1], num_leaves=1):
            //   a = merkleize([f0], 1) = f0
            //   rest = merkleize_progressive([f1], num_leaves=4):
            //     a2 = merkleize([f1], 4) = hash(hash(f1, zero), hash(zero, zero))
            //     rest2 = merkleize_progressive([], 16) = zero
            //     inner = hash(zero, a2)
            //   result = hash(inner, f0)
            var f0 = MakeChunk(0x11);
            var f1 = MakeChunk(0x22);
            var result = SszMerkleizer.MerkleizeProgressive(new List<byte[]> { f0, f1 });

            // a2 = merkleize([f1], limit=4)
            // 4 leaves: [f1, zero, zero, zero], tree: hash(hash(f1, zero), hash(zero, zero))
            var a2 = Sha256(Sha256(f1, Zero), Sha256(Zero, Zero));
            // inner = hash(rest2=zero, a2)
            var inner = Sha256(Zero, a2);
            // result = hash(inner, f0)
            var expected = Sha256(inner, f0);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MerkleizeProgressive_FiveChunks_UsesProgressiveSubtrees()
        {
            // 5 chunks: subtree sizes 1, 4 → takes [f0] then [f1,f2,f3,f4]
            // merkleize_progressive([f0..f4], 1):
            //   a = merkleize([f0], 1) = f0
            //   rest = merkleize_progressive([f1,f2,f3,f4], 4):
            //     a2 = merkleize([f1,f2,f3,f4], 4) = hash(hash(f1,f2), hash(f3,f4))
            //     rest2 = merkleize_progressive([], 16) = zero
            //     inner = hash(zero, a2)
            //   result = hash(inner, f0)
            var chunks = Enumerable.Range(1, 5).Select(i => MakeChunk((byte)i)).ToList();

            var result = SszMerkleizer.MerkleizeProgressive(chunks);

            var a2 = Sha256(Sha256(chunks[1], chunks[2]), Sha256(chunks[3], chunks[4]));
            var inner = Sha256(Zero, a2);
            var expected = Sha256(inner, chunks[0]);
            Assert.Equal(expected, result);
        }

        // --- PackActiveFields ---

        [Fact]
        public void PackActiveFields_AllOnes_PacksCorrectly()
        {
            var fields = new[] { true, true, true, true, true, true, true, true };
            var packed = SszMerkleizer.PackActiveFields(fields);

            Assert.Equal(32, packed.Length);
            Assert.Equal(0xFF, packed[0]);
            Assert.All(packed.Skip(1), b => Assert.Equal(0, b));
        }

        [Fact]
        public void PackActiveFields_MixedBits_LsbFirst()
        {
            // active_fields = [1, 0, 1, 1, 0, 0, 0, 0] → byte[0] = 0b00001101 = 0x0D
            var fields = new[] { true, false, true, true, false, false, false, false };
            var packed = SszMerkleizer.PackActiveFields(fields);

            Assert.Equal(0x0D, packed[0]);
        }

        [Fact]
        public void PackActiveFields_TwoBits_PacksFirstByte()
        {
            var fields = new[] { true, true };
            var packed = SszMerkleizer.PackActiveFields(fields);

            Assert.Equal(0x03, packed[0]);
            Assert.All(packed.Skip(1), b => Assert.Equal(0, b));
        }

        [Fact]
        public void PackActiveFields_SpansMultipleBytes()
        {
            // 9 bits: [1,1,1,1,1,1,1,1, 1] → byte[0]=0xFF, byte[1]=0x01
            var fields = Enumerable.Repeat(true, 9).ToArray();
            var packed = SszMerkleizer.PackActiveFields(fields);

            Assert.Equal(0xFF, packed[0]);
            Assert.Equal(0x01, packed[1]);
        }

        [Fact]
        public void PackActiveFields_Empty_Throws()
        {
            Assert.Throws<ArgumentException>(() => SszMerkleizer.PackActiveFields(new bool[0]));
        }

        [Fact]
        public void PackActiveFields_Exceeds256_Throws()
        {
            Assert.Throws<ArgumentException>(() => SszMerkleizer.PackActiveFields(new bool[257]));
        }

        // --- MixInActiveFields ---

        [Fact]
        public void MixInActiveFields_ProducesHashOfRootAndPackedFields()
        {
            var root = MakeChunk(0xAA);
            var fields = new[] { true, true, false, true };
            var result = SszMerkleizer.MixInActiveFields(root, fields);

            var packed = SszMerkleizer.PackActiveFields(fields);
            var expected = Sha256(root, packed);
            Assert.Equal(expected, result);
        }

        // --- HashTreeRootProgressiveContainer ---

        [Fact]
        public void HashTreeRootProgressiveContainer_SingleField_Correct()
        {
            var f0 = MakeChunk(0x42);
            var activeFields = new[] { true };
            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { f0 }, activeFields);

            // progressive root = merkleize_progressive([f0]) = hash(zero, f0)
            var progressiveRoot = Sha256(Zero, f0);
            // mix in active fields: hash(progressiveRoot, pack_bits([1]))
            var packed = new byte[32];
            packed[0] = 0x01;
            var expected = Sha256(progressiveRoot, packed);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void HashTreeRootProgressiveContainer_TwoFields_MatchesManualComputation()
        {
            var f0 = MakeChunk(0x11);
            var f1 = MakeChunk(0x22);
            var activeFields = new[] { true, true };
            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { f0, f1 }, activeFields);

            // Same as TwoChunks test but with mix-in
            var a2 = Sha256(Sha256(f1, Zero), Sha256(Zero, Zero));
            var inner = Sha256(Zero, a2);
            var progressiveRoot = Sha256(inner, f0);
            var packed = new byte[32];
            packed[0] = 0x03; // bits: 11
            var expected = Sha256(progressiveRoot, packed);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void HashTreeRootProgressiveContainer_WithInactiveFields_IncludesZeroForInactive()
        {
            // active_fields = [1, 0, 1] → 2 active fields, 3 total positions
            // Expanded: [f0, zero, f2]
            var f0 = MakeChunk(0x11);
            var f2 = MakeChunk(0x33);
            var activeFields = new[] { true, false, true };
            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { f0, f2 }, activeFields);

            // Progressive merkleization over [f0, zero, f2] (3 positions, inactive = zero)
            // merkleize_progressive([f0, zero, f2], 1):
            //   subtree = merkleize([f0], 1) = f0
            //   rest = merkleize_progressive([zero, f2], 4):
            //     subtree2 = merkleize([zero, f2], 4) = hash(hash(zero, f2), hash(zero, zero))
            //     rest2 = merkleize_progressive([], 16) = zero
            //     inner = hash(zero, subtree2)
            //   result = hash(inner, f0)
            var subtree2 = Sha256(Sha256(Zero, f2), Sha256(Zero, Zero));
            var inner = Sha256(Zero, subtree2);
            var progressiveRoot = Sha256(inner, f0);
            // Pack: [1,0,1] → byte[0] = 0b00000101 = 0x05
            var packed = new byte[32];
            packed[0] = 0x05;
            var expected = Sha256(progressiveRoot, packed);
            Assert.Equal(expected, result);
        }

        // --- Ethereum consensus-spec-tests vectors ---
        // Source: ethereum/consensus-spec-tests/tests/general/phase0/ssz_generic/progressive_containers/valid/
        // Type: ProgressiveSingleFieldContainerTestStruct = ProgressiveContainer(active_fields=[1]) { A: byte }

        [Fact]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveSingleListContainer_Nil_ConsensusSpecVector()
        {
            // ProgressiveSingleListContainerTestStruct(active_fields=[0, 0, 0, 0, 1])
            // Single field C: ProgressiveBitlist, value '0x01' (0 bits)
            // Expected: 0x67c31e76fa13596ee5b7775ec7b123e36ef345979da1c5c0cf843b836c9e750c
            var fieldC = SszMerkleizer.HashTreeRootProgressiveBitlist(new bool[0]);
            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { fieldC },
                new[] { false, false, false, false, true });
            var expected = "0x67c31e76fa13596ee5b7775ec7b123e36ef345979da1c5c0cf843b836c9e750c".HexToByteArray();
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, "0xe832d263aaa8f9417d9f45a702834f6961ee7b15ad4d3d27f2b0f4fe79d33031")]
        [InlineData(3, "0x6393713d769f8045e143f18f3335bb594a34a1f789d013d2dc194be89225224f")]
        [InlineData(225, "0x27194c58e5f086d5e17cd009d75f5620901e68e467a4b04abbf2129eaa600bc8")]
        [InlineData(255, "0x4ca4929a058bf05661f6c9b898a8ea5b5448bd78ddd9be0989d7c8944a1bcc2d")]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveSingleFieldContainer_ConsensusSpecVectors(byte fieldA, string expectedRootHex)
        {
            // hash_tree_root(byte) = byte value in first position of 32-byte chunk
            var fieldRoot = new byte[32];
            fieldRoot[0] = fieldA;

            var activeFields = new[] { true };
            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { fieldRoot }, activeFields);

            var expectedRoot = expectedRootHex.HexToByteArray();
            Assert.Equal(expectedRoot, result);
        }

        // --- CompatibleUnion ---

        [Fact]
        public void MixInSelector_ProducesHashOfRootAndSelectorChunk()
        {
            var root = MakeChunk(0xAA);
            var result = SszMerkleizer.MixInSelector(root, 1);

            var selectorChunk = new byte[32];
            selectorChunk[0] = 1;
            var expected = Sha256(root, selectorChunk);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void HashTreeRootCompatibleUnion_WrapsContainerRoot()
        {
            // CompatibleUnionA = CompatibleUnion({1: ProgressiveSingleFieldContainerTestStruct})
            // For A=225: container root = 0x27194c58...
            // Union root = mix_in_selector(container_root, 1)
            var containerRoot = "0x27194c58e5f086d5e17cd009d75f5620901e68e467a4b04abbf2129eaa600bc8".HexToByteArray();
            var unionRoot = SszMerkleizer.HashTreeRootCompatibleUnion(containerRoot, 1);

            var selectorChunk = new byte[32];
            selectorChunk[0] = 1;
            var expected = Sha256(containerRoot, selectorChunk);
            Assert.Equal(expected, unionRoot);
            Assert.NotEqual(containerRoot, unionRoot);
        }

        [Fact]
        public void HashTreeRootCompatibleUnion_DifferentSelectors_DifferentRoots()
        {
            var containerRoot = MakeChunk(0x42);
            var root1 = SszMerkleizer.HashTreeRootCompatibleUnion(containerRoot, 1);
            var root2 = SszMerkleizer.HashTreeRootCompatibleUnion(containerRoot, 2);
            Assert.NotEqual(root1, root2);
        }

        // --- ProgressiveBitlist consensus-spec-tests vectors ---
        // Source: ethereum/consensus-spec-tests/tests/general/phase0/ssz_generic/progressive_bitlist/valid/
        // ProgressiveBitlist serialization: bits packed LSB-first, with a delimiter 1-bit appended

        [Theory]
        [InlineData("0x01", 0, "0xf5a5fd42d16a20302798ef6ed309979b43003d2320d9f0e8ea9831a92759fb4b")]
        [InlineData("0x03", 1, "0x573a032da7e6b5f5e90253e1ac50ca352bc39203f863f391e8171082e1e48840")]
        [InlineData("0xbf01", 8, "0x30a3fd6aa427161a3ac2936a594925359deed17ca4bdc04f1f94a9a5e4328d1f")]
        [InlineData("0xbfc7b70b01", 32, "0xee28e1a51192c7986f8bfed56c577abea8cb0d6a8e74fc379e872e892a32f05b")]
        [InlineData("0xbfc7b70bbbdc3759050a335ab6a356adf8fd110c929441629a8106678f68f3a901", 256,
            "0xc2e13c893d40c93b4f23abfced12ca25acaa6bf843c640c94be1a25c5cfd3474")]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveBitlist_ConsensusSpecVectors(string serializedHex, int bitCount, string expectedRootHex)
        {
            var serialized = serializedHex.HexToByteArray();
            var bits = DeserializeBitlist(serialized);
            Assert.Equal(bitCount, bits.Length);

            var result = SszMerkleizer.HashTreeRootProgressiveBitlist(bits);
            var expected = expectedRootHex.HexToByteArray();
            Assert.Equal(expected, result);
        }

        // --- ProgressiveList[uint64] consensus-spec-tests vectors ---
        // Source: ethereum/consensus-spec-tests/tests/general/phase0/ssz_generic/basic_progressive_list/valid/

        [Fact]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveList_Uint64_Empty_ConsensusSpecVector()
        {
            // proglist_uint64_zero_0: [] → 0xf5a5fd42...
            var result = SszMerkleizer.HashTreeRootProgressiveList(new List<byte[]>());
            var expected = "0xf5a5fd42d16a20302798ef6ed309979b43003d2320d9f0e8ea9831a92759fb4b".HexToByteArray();
            Assert.Equal(expected, result);
        }

        [Fact]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveList_Uint64_SingleZero_ConsensusSpecVector()
        {
            // proglist_uint64_zero_1: [0] → 0xe832d263...
            var elementRoot = new byte[32]; // hash_tree_root(uint64(0)) = 0-padded chunk
            var result = SszMerkleizer.HashTreeRootProgressiveList(new List<byte[]> { elementRoot });
            var expected = "0xe832d263aaa8f9417d9f45a702834f6961ee7b15ad4d3d27f2b0f4fe79d33031".HexToByteArray();
            Assert.Equal(expected, result);
        }

        [Fact]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveList_Uint64_SingleMax_ConsensusSpecVector()
        {
            // proglist_uint64_max_1: [18446744073709551615] → 0x64b8306d...
            var elementRoot = new byte[32];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(elementRoot, ulong.MaxValue);
            var result = SszMerkleizer.HashTreeRootProgressiveList(new List<byte[]> { elementRoot });
            var expected = "0x64b8306dca107991dec26097d406a2addbd06000698ea0aea580eb2507d3f311".HexToByteArray();
            Assert.Equal(expected, result);
        }

        private static bool[] DeserializeBitlist(byte[] serialized)
        {
            if (serialized == null || serialized.Length == 0)
                throw new ArgumentException("Bitlist must have at least the delimiter byte.");

            // Find the delimiter bit (highest set bit in the last byte)
            var lastByte = serialized[serialized.Length - 1];
            if (lastByte == 0) throw new ArgumentException("Invalid bitlist: last byte is zero.");

            var delimiterBitPosition = 7;
            while ((lastByte & (1 << delimiterBitPosition)) == 0) delimiterBitPosition--;

            // Total bits = (serialized.Length - 1) * 8 + delimiterBitPosition
            var totalBits = (serialized.Length - 1) * 8 + delimiterBitPosition;

            var bits = new bool[totalBits];
            for (var i = 0; i < totalBits; i++)
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                bits[i] = (serialized[byteIndex] & (1 << bitIndex)) != 0;
            }

            return bits;
        }

        // --- ProgressiveVarTestStruct consensus-spec-tests vectors ---
        // ProgressiveVarTestStruct = ProgressiveContainer(active_fields=[1, 0, 1, 0, 1])
        // Fields: A: byte, B: List[uint16, 123], C: ProgressiveBitlist

        [Fact]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveVarTestStruct_Nil0_ConsensusSpecVector()
        {
            // A=225, B=[], C='0x01' (0 bits)
            // Expected root: 0x7e8b602bdc62618f1891316ac57ffc8c6132bca5705a0c778f658bbc346feebd

            // Field A: hash_tree_root(byte 225)
            var fieldA = new byte[32];
            fieldA[0] = 225;

            // Field B: hash_tree_root(List[uint16, 123] with 0 elements)
            // pack([]) = no data → merkleize with limit = chunk_count(uint16, 123) = 8
            var emptyListRoot = SszMerkleizer.Merkleize(new List<byte[]>(), 8);
            var fieldB = SszMerkleizer.MixInLength(emptyListRoot, 0);

            // Field C: hash_tree_root(ProgressiveBitlist with 0 bits)
            var fieldC = SszMerkleizer.HashTreeRootProgressiveBitlist(new bool[0]);

            // Verify fieldC matches independently validated bitlist root
            var expectedFieldC = "0xf5a5fd42d16a20302798ef6ed309979b43003d2320d9f0e8ea9831a92759fb4b".HexToByteArray();
            Assert.Equal(expectedFieldC, fieldC);

            // Test with only fieldA to verify that part works
            // ProgressiveSingleFieldContainerTestStruct with A=225 should give known result
            var singleResult = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { fieldA }, new[] { true });
            var expectedSingle = "0x27194c58e5f086d5e17cd009d75f5620901e68e467a4b04abbf2129eaa600bc8".HexToByteArray();
            Assert.Equal(expectedSingle, singleResult);

            var activeFields = new[] { true, false, true, false, true };
            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { fieldA, fieldB, fieldC }, activeFields);

            var expected = "0x7e8b602bdc62618f1891316ac57ffc8c6132bca5705a0c778f658bbc346feebd".HexToByteArray();
            Assert.Equal(expected, result);
        }

        [Fact]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveVarTestStruct_One0_ConsensusSpecVector()
        {
            // A=225, B=[981], C='0x03' (1 bit: true)
            var fieldA = new byte[32];
            fieldA[0] = 225;

            // List[uint16, 123] with [981]: pack uint16 LE, merkleize with chunk_count limit=8
            var packedB = new byte[32];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(packedB, 981);
            var listRoot = SszMerkleizer.Merkleize(new List<byte[]> { packedB }, 8);
            var fieldB = SszMerkleizer.MixInLength(listRoot, 1);

            // ProgressiveBitlist '0x03': 1 bit (true) + delimiter
            var fieldC = SszMerkleizer.HashTreeRootProgressiveBitlist(new[] { true });

            var result = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { fieldA, fieldB, fieldC },
                new[] { true, false, true, false, true });

            var expected = "0x832fb57902a303b8e03923e1e603edc80e514c2596c9c352e73cc15f4e9c0000".HexToByteArray();
            Assert.Equal(expected, result);
        }

        // 261 progressive_containers + 700 progressive_bitlist + 320 basic_progressive_list vectors available
        // at tests/LightClientVectors/ssz/consensus-spec-tests/phase0/ssz_generic/

        [Fact]
        public void HashTreeRootProgressiveContainer_FieldCountMismatch_Throws()
        {
            var activeFields = new[] { true, true, false };
            // 2 active fields but providing 3 roots
            Assert.Throws<ArgumentException>(() =>
                SszMerkleizer.HashTreeRootProgressiveContainer(
                    new List<byte[]> { MakeChunk(1), MakeChunk(2), MakeChunk(3) },
                    activeFields));
        }

        [Fact]
        public void HashTreeRootProgressiveContainer_DifferentActiveFields_DifferentRoots()
        {
            var f0 = MakeChunk(0x11);
            var f1 = MakeChunk(0x22);

            // Same field roots, different active_fields bitvectors
            var result1 = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { f0, f1 }, new[] { true, true });
            var result2 = SszMerkleizer.HashTreeRootProgressiveContainer(
                new List<byte[]> { f0, f1 }, new[] { true, false, true });

            // Different active_fields → different mix-in → different root
            Assert.NotEqual(result1, result2);
        }
    }
}
