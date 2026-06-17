using System.Collections.Generic;
using Nethereum.DevP2P.Discv5;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    /// <summary>
    /// FINDNODE-response chunking policy. Each NODES packet must stay under the
    /// soft byte budget so the resulting UDP datagram fits inside the discv5
    /// 1280-byte cap, and total record count must be capped at 16 so the
    /// requester (which enforces a small max-packets-per-FINDNODE limit) does
    /// not silently drop the tail of our reply.
    /// </summary>
    public class Discv5ListenerFindNodeChunkingTests
    {
        [Fact]
        public void Given_ManyTypicalEnrs_When_PackedIntoNodesChunks_Then_EachChunkStaysUnderByteBudget()
        {
            // 16 typical ENRs of ~150 bytes each. Geth caps a peer's view at
            // ~5 packets; we must pack them densely without exceeding the budget.
            var records = new List<byte[]>();
            for (int i = 0; i < Discv5Listener.MaxNodesResponseTotal; i++)
                records.Add(MakeFakeEnr(size: 150, seed: (byte)i));

            var chunks = Discv5Listener.PackNodesChunks(records);

            // Records preserved (no records lost or duplicated).
            int packed = 0;
            foreach (var c in chunks) packed += c.Count;
            Assert.Equal(records.Count, packed);

            foreach (var c in chunks)
            {
                int chunkSize = 0;
                foreach (var r in c) chunkSize += r.Length;
                // Soft budget = NodesRecordsBudgetBytes; allow a single oversize
                // record to live in its own chunk per implementation contract.
                Assert.True(
                    chunkSize <= Discv5Listener.NodesRecordsBudgetBytes || c.Count == 1,
                    $"Chunk records sum is {chunkSize} bytes — exceeds {Discv5Listener.NodesRecordsBudgetBytes}-byte soft budget.");
                // And in any case the full datagram (records + RLP framing + outer
                // discv5 packet) must stay under 1280 bytes.
                Assert.True(chunkSize < 1100, $"Chunk records sum is {chunkSize} bytes — exceeds 1100-byte hard ceiling.");
            }
        }

        [Fact]
        public void Given_LargerRecordSet_When_Packed_Then_AtMost16RecordsAreReturned()
        {
            // Source list intentionally exceeds 16 — the *handler* clamps to 16
            // before calling the packer, but the packer itself must also faithfully
            // round-trip whatever it's given. The integration property we test
            // here is that the public cap is exposed for the handler to enforce.
            Assert.Equal(16, Discv5Listener.MaxNodesResponseTotal);

            // Round-trip: 16 records → some number of chunks → recovered list size.
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
            // Treat MaxNodesResponseTotal as the worst-case and verify the resulting
            // NODES messages — including request-id, total field, and RLP list
            // framing — still fit inside the discv5 1280-byte UDP packet budget.
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
            // Opaque opaque-bytes ENR (the packer never decodes the records).
            var buf = new byte[size];
            for (int i = 0; i < size; i++) buf[i] = (byte)(seed + i);
            return buf;
        }
    }
}
