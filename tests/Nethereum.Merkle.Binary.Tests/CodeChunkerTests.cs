using Nethereum.Merkle.Binary.Keys;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class CodeChunkerTests
    {
        [Fact]
        [Trait("Category", "CodeChunker")]
        public void EmptyCode_ReturnsEmpty()
        {
            Assert.Empty(CodeChunker.ChunkifyCode(null));
            Assert.Empty(CodeChunker.ChunkifyCode(new byte[0]));
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void SingleChunk_WithPush1()
        {
            var code = new byte[] { 0x60, 0x60, 0x01 };
            var chunks = CodeChunker.ChunkifyCode(code);
            Assert.Single(chunks);
            Assert.Equal(32, chunks[0].Length);
            Assert.Equal(0, chunks[0][0]);
            Assert.Equal(0x60, chunks[0][1]);
            Assert.Equal(0x60, chunks[0][2]);
            Assert.Equal(0x01, chunks[0][3]);
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void Push32_SpansBoundary()
        {
            var code = new byte[33];
            code[0] = 0x7f; // PUSH32
            for (int i = 1; i <= 32; i++) code[i] = (byte)i;

            var chunks = CodeChunker.ChunkifyCode(code);
            Assert.True(chunks.Length >= 2);
            Assert.Equal(2, chunks[1][0]);
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void Exactly31Bytes_OneChunk()
        {
            var code = new byte[31];
            var chunks = CodeChunker.ChunkifyCode(code);
            Assert.Single(chunks);
            Assert.Equal(0, chunks[0][0]);
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void ThirtyTwoBytes_TwoChunks()
        {
            var code = new byte[32];
            var chunks = CodeChunker.ChunkifyCode(code);
            Assert.Equal(2, chunks.Length);
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void Deterministic()
        {
            var code = new byte[100];
            for (int i = 0; i < code.Length; i++) code[i] = (byte)(i % 256);
            var c1 = CodeChunker.ChunkifyCode(code);
            var c2 = CodeChunker.ChunkifyCode(code);
            Assert.Equal(c1.Length, c2.Length);
            for (int i = 0; i < c1.Length; i++)
                Assert.Equal(c1[i], c2[i]);
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void AllChunks_Are32Bytes()
        {
            var code = new byte[200];
            var chunks = CodeChunker.ChunkifyCode(code);
            foreach (var chunk in chunks)
                Assert.Equal(32, chunk.Length);
        }

        [Fact]
        [Trait("Category", "CodeChunker")]
        public void ChunkCount_CeilDivision()
        {
            var code = new byte[100];
            var chunks = CodeChunker.ChunkifyCode(code);
            int expected = (100 + 30) / 31; // ceil(100/31) = 4
            Assert.Equal(expected, chunks.Length);
        }
    }
}
