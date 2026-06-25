using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth68
{
    /// <summary>
    /// Round-trip tests for every eth/68 message encoder/decoder. Each test
    /// constructs a representative payload, encodes it, decodes the bytes back,
    /// and verifies field-level equality. Catches any RLP shape regression.
    /// </summary>
    public class Eth68MessageRoundTripTests
    {
        [Fact]
        public void NewBlockHashes_RoundTrip_PreservesEntries()
        {
            var msg = new NewBlockHashesMessage
            {
                Entries =
                {
                    new NewBlockHashesMessage.BlockHashEntry { Hash = Make32(0x11), Number = 1 },
                    new NewBlockHashesMessage.BlockHashEntry { Hash = Make32(0x22), Number = 12345 },
                    new NewBlockHashesMessage.BlockHashEntry { Hash = Make32(0x33), Number = ulong.MaxValue / 2 }
                }
            };

            var bytes = NewBlockHashesMessageEncoder.Encode(msg);
            var decoded = NewBlockHashesMessageEncoder.Decode(bytes);

            Assert.Equal(msg.Entries.Count, decoded.Entries.Count);
            for (int i = 0; i < msg.Entries.Count; i++)
            {
                Assert.Equal(msg.Entries[i].Hash.ToHex(), decoded.Entries[i].Hash.ToHex());
                Assert.Equal(msg.Entries[i].Number, decoded.Entries[i].Number);
            }
        }

        [Fact]
        public void NewBlockHashes_Empty_RoundTrip()
        {
            var msg = new NewBlockHashesMessage();
            var bytes = NewBlockHashesMessageEncoder.Encode(msg);
            var decoded = NewBlockHashesMessageEncoder.Decode(bytes);
            Assert.Empty(decoded.Entries);
        }

        [Fact]
        public void NewPooledTransactionHashes_RoundTrip_PreservesAllFields()
        {
            var msg = new NewPooledTransactionHashesMessage
            {
                Types = new byte[] { 0x00, 0x02, 0x03 },
                Sizes = new List<long> { 100, 250, 1024 },
                Hashes = new List<byte[]> { Make32(0xaa), Make32(0xbb), Make32(0xcc) }
            };

            var bytes = NewPooledTransactionHashesMessageEncoder.Encode(msg);
            var decoded = NewPooledTransactionHashesMessageEncoder.Decode(bytes);

            Assert.Equal(msg.Types.ToHex(), decoded.Types.ToHex());
            Assert.Equal(msg.Sizes, decoded.Sizes);
            Assert.Equal(msg.Hashes.Count, decoded.Hashes.Count);
            for (int i = 0; i < msg.Hashes.Count; i++)
                Assert.Equal(msg.Hashes[i].ToHex(), decoded.Hashes[i].ToHex());
        }

        [Fact]
        public void NewPooledTransactionHashes_AllZeroTypes_RoundTrip()
        {
            var msg = new NewPooledTransactionHashesMessage
            {
                Types = new byte[] { 0x00, 0x00 },
                Sizes = new List<long> { 50, 60 },
                Hashes = new List<byte[]> { Make32(0x01), Make32(0x02) }
            };
            var bytes = NewPooledTransactionHashesMessageEncoder.Encode(msg);
            var decoded = NewPooledTransactionHashesMessageEncoder.Decode(bytes);
            Assert.Equal(2, decoded.Hashes.Count);
            Assert.Equal(msg.Sizes, decoded.Sizes);
        }

        [Fact]
        public void GetPooledTransactions_RoundTrip_PreservesRequestIdAndHashes()
        {
            var msg = new GetPooledTransactionsMessage
            {
                RequestId = 0xDEADBEEFul,
                Hashes = new List<byte[]> { Make32(0x12), Make32(0x34) }
            };
            var bytes = GetPooledTransactionsMessageEncoder.Encode(msg);
            var decoded = GetPooledTransactionsMessageEncoder.Decode(bytes);
            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(msg.Hashes.Count, decoded.Hashes.Count);
            Assert.Equal(msg.Hashes[0].ToHex(), decoded.Hashes[0].ToHex());
        }

        [Fact]
        public void BlockRangeUpdate_RoundTrip_PreservesRange()
        {
            var msg = new BlockRangeUpdateMessage
            {
                EarliestBlock = 1000,
                LatestBlock = 20_000_000,
                LatestBlockHash = Make32(0xff)
            };

            var bytes = BlockRangeUpdateMessageEncoder.Encode(msg);
            var decoded = BlockRangeUpdateMessageEncoder.Decode(bytes);

            Assert.Equal(msg.EarliestBlock, decoded.EarliestBlock);
            Assert.Equal(msg.LatestBlock, decoded.LatestBlock);
            Assert.Equal(msg.LatestBlockHash.ToHex(), decoded.LatestBlockHash.ToHex());
        }

        [Fact]
        public void BlockRangeUpdate_GenesisOnly_RoundTrip()
        {
            var msg = new BlockRangeUpdateMessage
            {
                EarliestBlock = 0,
                LatestBlock = 0,
                LatestBlockHash = Make32(0xa5)
            };
            var bytes = BlockRangeUpdateMessageEncoder.Encode(msg);
            var decoded = BlockRangeUpdateMessageEncoder.Decode(bytes);
            Assert.Equal(0ul, decoded.EarliestBlock);
            Assert.Equal(0ul, decoded.LatestBlock);
        }

        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }
    }
}
