using System.Linq;
using Nethereum.CoreChain.Storage.History;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Storage.History
{
    public class BlockMetaCodecTests
    {
        [Fact]
        public void RoundTrips_AllFields()
        {
            var m = new BlockMeta
            {
                BlockHash = Enumerable.Repeat((byte)0xAB, 32).ToArray(),
                TxCount = 273,
                Uncles = new byte[] { 0x01, 0x02, 0x03 },
                Withdrawals = new byte[] { 0x04, 0x05 },
                Bloom = Enumerable.Repeat((byte)0x11, 256).ToArray(),
            };

            var back = BlockMetaCodec.Decode(BlockMetaCodec.Encode(m));

            Assert.Equal(m.BlockHash, back.BlockHash);
            Assert.Equal(273, back.TxCount);
            Assert.Equal(m.Uncles, back.Uncles);
            Assert.Equal(m.Withdrawals, back.Withdrawals);
            Assert.Equal(m.Bloom, back.Bloom);
        }

        [Fact]
        public void RoundTrips_Empty_Uncles_Withdrawals_And_ZeroTxCount()
        {
            var m = new BlockMeta
            {
                BlockHash = Enumerable.Repeat((byte)0x01, 32).ToArray(),
                TxCount = 0,
                Uncles = System.Array.Empty<byte>(),
                Withdrawals = null,
                Bloom = new byte[256],
            };

            var back = BlockMetaCodec.Decode(BlockMetaCodec.Encode(m));

            Assert.Equal(m.BlockHash, back.BlockHash);
            Assert.Equal(0, back.TxCount);
            Assert.Empty(back.Uncles);
            Assert.Empty(back.Withdrawals);
            Assert.Equal(256, back.Bloom.Length);
        }
    }
}
