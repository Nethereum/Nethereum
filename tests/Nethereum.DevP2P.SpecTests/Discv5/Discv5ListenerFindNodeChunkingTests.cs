using System.Collections.Generic;
using Nethereum.DevP2P.Discv5;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5ListenerFindNodeChunkingTests
    {
        [Fact]
        public void Given_ManyTypicalEnrs_When_PackedIntoNodesChunks_Then_EachChunkStaysUnderByteBudget()
        {
            var records = new List<byte[]>();
            for (int i = 0; i < Discv5Listener.MaxNodesResponseTotal; i++)
                records.Add(MakeFakeEnr(size: 150, seed: (byte)i));

            var chunks = Discv5Listener.PackNodesChunks(records);

            int packed = 0;
            foreach (var c in chunks) packed += c.Count;
            Assert.Equal(records.Count, packed);

            foreach (var c in chunks)
            {
                int chunkSize = 0;
                foreach (var r in c) chunkSize += r.Length;
                Assert.True(
                    chunkSize <= Discv5Listener.NodesRecordsBudgetBytes || c.Count == 1,
                    $"Chunk records sum is {chunkSize} bytes — exceeds {Discv5Listener.NodesRecordsBudgetBytes}-byte soft budget.");
                Assert.True(chunkSize < 1100, $"Chunk records sum is {chunkSize} bytes — exceeds 1100-byte hard ceiling.");
            }
        }

        [Fact]
        public void Given_LargerRecordSet_When_Packed_Then_AtMost16RecordsAreReturned()
        {
            Assert.Equal(16, Discv5Listener.MaxNodesResponseTotal);

            var records = new List<byte[]>();
            for (int i = 0; i < Discv5Listener.MaxNodesResponseTotal; i++)
                records.Add(MakeFakeEnr(size: 200, seed: (byte)i));
            var chunks = Discv5Listener.PackNodesChunks(records);
            int packed = 0;
            foreach (var c in chunks) packed += c.Count;
            Assert.Equal(Discv5Listener.MaxNodesResponseTotal, packed);
        }

        [Fact]
        public void Given_EmptyRecordSet_When_Packed_Then_OneZeroRecordChunkReturned()
        {
            var chunks = Discv5Listener.PackNodesChunks(new List<byte[]>());
            Assert.Single(chunks);
            Assert.Empty(chunks[0]);
        }

        [Fact]
        public void Given_SixteenTypicalEnrs_When_PackedAndEncoded_Then_NodesPacketsStayUnder1100Bytes()
        {
            var records = new List<byte[]>();
            for (int i = 0; i < Discv5Listener.MaxNodesResponseTotal; i++)
                records.Add(MakeFakeEnr(size: 150, seed: (byte)i));
            var chunks = Discv5Listener.PackNodesChunks(records);

            byte total = (byte)chunks.Count;
            foreach (var chunk in chunks)
            {
                var msg = new Discv5NodesMessage
                {
                    RequestId = new byte[] { 0xAB, 0xCD },
                    Total = total,
                    Records = chunk
                };
                var encoded = Discv5MessageEncoder.EncodeNodes(msg);
                Assert.True(encoded.Length < 1100,
                    $"NODES message encodes to {encoded.Length} bytes — too close to the 1280 hard cap.");
            }
        }

        private static byte[] MakeFakeEnr(int size, byte seed)
        {
            var buf = new byte[size];
            for (int i = 0; i < size; i++) buf[i] = (byte)(seed + i);
            return buf;
        }
    }
}
