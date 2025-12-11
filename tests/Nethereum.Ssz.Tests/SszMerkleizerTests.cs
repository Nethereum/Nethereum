using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace Nethereum.Ssz.Tests
{
    public class SszMerkleizerTests
    {
        [Fact]
        public void Merkleize_TwoChunks_MatchesSha256()
        {
            var chunkA = Enumerable.Repeat((byte)0x11, 32).ToArray();
            var chunkB = Enumerable.Repeat((byte)0x22, 32).ToArray();
            var chunks = new List<byte[]> { chunkA, chunkB };

            byte[] expected;
            using (var sha = SHA256.Create())
            {
                var concat = new byte[64];
                Buffer.BlockCopy(chunkA, 0, concat, 0, 32);
                Buffer.BlockCopy(chunkB, 0, concat, 32, 32);
                expected = sha.ComputeHash(concat);
            }

            var root = SszMerkleizer.Merkleize(chunks);

            Assert.Equal(expected, root);
        }

        [Fact]
        public void Merkleize_SingleChunk_PadsWithZero()
        {
            var chunk = Enumerable.Repeat((byte)0x42, 32).ToArray();
            var zero = new byte[32];
            byte[] expected;
            using (var sha = SHA256.Create())
            {
                var concat = new byte[64];
                Buffer.BlockCopy(chunk, 0, concat, 0, 32);
                Buffer.BlockCopy(zero, 0, concat, 32, 32);
                expected = sha.ComputeHash(concat);
            }

            var root = SszMerkleizer.Merkleize(new List<byte[]> { chunk });

            Assert.Equal(expected, root);
        }

        [Fact]
        public void HashTreeRootVector_Mixes_Length()
        {
            var chunkA = new byte[32];
            var chunkB = new byte[32];
            chunkB[0] = 0x01;
            var rootLen1 = SszMerkleizer.HashTreeRootVector(new List<byte[]> { chunkA, chunkB }, 1);
            var rootLen2 = SszMerkleizer.HashTreeRootVector(new List<byte[]> { chunkA, chunkB }, 2);

            Assert.NotEqual(rootLen1, rootLen2);
        }

        [Fact]
        public void Chunkify_Pads_PartialChunk()
        {
            var data = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();
            var chunks = SszMerkleizer.Chunkify(data);

            Assert.Equal(2, chunks.Count);
            Assert.Equal(32, chunks[0].Length);
            Assert.Equal(32, chunks[1].Length);
            Assert.Equal(data.Take(32), chunks[0]);
            Assert.Equal(data.Skip(32), chunks[1].Take(8));
            Assert.All(chunks[1].Skip(8), b => Assert.Equal(0, b));
        }
    }
}
