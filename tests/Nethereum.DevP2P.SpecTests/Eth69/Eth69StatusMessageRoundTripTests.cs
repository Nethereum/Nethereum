using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth69
{
    /// <summary>
    /// eth/69 Status round-trip. Validates the wire shape per
    /// github.com/ethereum/devp2p/blob/master/caps/eth.md#status-0x00
    /// for protocol version 69: TotalDifficulty removed, BestHash replaced
    /// with [EarliestBlock, LatestBlock, LatestBlockHash].
    /// </summary>
    public class Eth69StatusMessageRoundTripTests
    {
        [Fact]
        public void RoundTrip_PreservesAllFields()
        {
            var genesis = Make32(0xAA);
            var latestHash = Make32(0xBB);

            var msg = new Eth69StatusMessage
            {
                ProtocolVersion = 69,
                NetworkId = 3503995874084926UL,
                GenesisHash = genesis,
                ForkHash = 0xdeadbeef,
                ForkNext = 1000UL,
                EarliestBlock = 0UL,
                LatestBlock = 100UL,
                LatestBlockHash = latestHash
            };

            var bytes = Eth69StatusMessageEncoder.Encode(msg);
            var decoded = Eth69StatusMessageEncoder.Decode(bytes);

            Assert.Equal(69, decoded.ProtocolVersion);
            Assert.Equal(3503995874084926UL, decoded.NetworkId);
            Assert.Equal(genesis.ToHex(), decoded.GenesisHash.ToHex());
            Assert.Equal(0xdeadbeefU, decoded.ForkHash);
            Assert.Equal(1000UL, decoded.ForkNext);
            Assert.Equal(0UL, decoded.EarliestBlock);
            Assert.Equal(100UL, decoded.LatestBlock);
            Assert.Equal(latestHash.ToHex(), decoded.LatestBlockHash.ToHex());
        }

        [Fact]
        public void RoundTrip_HighRangeValues()
        {
            var msg = new Eth69StatusMessage
            {
                ProtocolVersion = 69,
                NetworkId = ulong.MaxValue / 2,
                GenesisHash = Make32(1),
                ForkHash = 0,
                ForkNext = 0,
                EarliestBlock = 0,
                LatestBlock = ulong.MaxValue / 2,
                LatestBlockHash = Make32(2)
            };

            var decoded = Eth69StatusMessageEncoder.Decode(Eth69StatusMessageEncoder.Encode(msg));
            Assert.Equal(msg.NetworkId, decoded.NetworkId);
            Assert.Equal(msg.LatestBlock, decoded.LatestBlock);
        }

        private static byte[] Make32(byte fill)
        {
            var b = new byte[32];
            for (int i = 0; i < 32; i++) b[i] = fill;
            return b;
        }
    }
}
