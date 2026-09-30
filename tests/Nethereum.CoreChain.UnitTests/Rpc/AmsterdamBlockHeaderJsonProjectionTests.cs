using System.Numerics;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class AmsterdamBlockHeaderJsonProjectionTests
    {
        private const ulong Slot = 424_242;
        private static readonly byte[] AccessListHash =
            "00aabbccddeeff00112233445566778899aabbccddeeff001122334455667788".HexToByteArray();

        private static BlockHeader HeaderWithAmsterdamFields() => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = new byte[256],
            Difficulty = 0,
            BlockNumber = 1,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1_700_000_000,
            ExtraData = new byte[0],
            MixHash = new byte[32],
            Nonce = new byte[8],
            BlockAccessListHash = AccessListHash,
            SlotNumber = Slot
        };

        private static Block Project(BlockHeader header) =>
            header.ToBlockWithTransactionHashes(new byte[32], new string[0], blockSize: 0);

        [Fact]
        public void Given_AnAmsterdamHeader_When_ProjectedToJson_Then_BothAmsterdamFieldsCarryTheHeadersValues()
        {
            var block = Project(HeaderWithAmsterdamFields());

            Assert.Equal("0x" + AccessListHash.ToHex(), block.BlockAccessListHash);
            Assert.Equal((BigInteger)Slot, block.SlotNumber.Value);
        }

        [Fact]
        public void Given_AHeaderWithoutTheAmsterdamFields_When_Projected_Then_NeitherIsEmitted()
        {
            var header = HeaderWithAmsterdamFields();
            header.BlockAccessListHash = null;
            header.SlotNumber = null;

            var block = Project(header);

            Assert.Null(block.BlockAccessListHash);
            Assert.Null(block.SlotNumber);
        }

        [Fact]
        public void Given_ABlockCarryingBothFields_When_RoundTrippedThroughNewtonsoft_Then_BothSurviveWithTheirWireNames()
        {
            var json = JsonConvert.SerializeObject(Project(HeaderWithAmsterdamFields()));

            Assert.Contains("\"blockAccessListHash\"", json);
            Assert.Contains("\"slotNumber\"", json);

            var back = JsonConvert.DeserializeObject<Block>(json);
            Assert.Equal("0x" + AccessListHash.ToHex(), back.BlockAccessListHash);
            Assert.Equal((BigInteger)Slot, back.SlotNumber.Value);
        }

#if NET6_0_OR_GREATER
        [Fact]
        public void Given_ABlockCarryingBothFields_When_RoundTrippedThroughSystemTextJson_Then_BothSurviveWithTheirWireNames()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(Project(HeaderWithAmsterdamFields()));

            Assert.Contains("\"blockAccessListHash\":\"0x" + AccessListHash.ToHex() + "\"", json);
            Assert.Contains("\"slotNumber\":\"0x" + Slot.ToString("x") + "\"", json);

            var back = System.Text.Json.JsonSerializer.Deserialize<Block>(json);
            Assert.Equal("0x" + AccessListHash.ToHex(), back.BlockAccessListHash);
            Assert.Equal((BigInteger)Slot, back.SlotNumber.Value);
        }
#endif

        [Fact]
        public void Given_ASlotNumberOfZero_When_RoundTripped_Then_ItIsZeroAndNotAbsent()
        {
            var header = HeaderWithAmsterdamFields();
            header.SlotNumber = 0;

            var block = Project(header);
            Assert.NotNull(block.SlotNumber);
            Assert.Equal(BigInteger.Zero, block.SlotNumber.Value);

            var back = JsonConvert.DeserializeObject<Block>(JsonConvert.SerializeObject(block));
            Assert.NotNull(back.SlotNumber);
            Assert.Equal(BigInteger.Zero, back.SlotNumber.Value);
        }

        [Fact]
        public void Given_ABlockAccessListHashWithALeadingZeroByte_When_RoundTripped_Then_ItKeepsItsFullWidth()
        {
            var block = Project(HeaderWithAmsterdamFields());

            Assert.StartsWith("0x00", block.BlockAccessListHash);
            Assert.Equal(66, block.BlockAccessListHash.Length);

            var back = JsonConvert.DeserializeObject<Block>(JsonConvert.SerializeObject(block));
            Assert.Equal(66, back.BlockAccessListHash.Length);
        }
    }
}
