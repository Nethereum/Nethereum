using System.Linq;
using Nethereum.Util;

namespace Nethereum.Model.UnitTests
{
    internal static class BlockHeaderShapes
    {
        internal static byte[] Bytes(byte fill, int length = 32)
            => Enumerable.Repeat(fill, length).ToArray();

        internal static BlockHeader For(string shape)
        {
            var h = new BlockHeader
            {
                ParentHash = Bytes(0x11),
                UnclesHash = Bytes(0x22),
                Coinbase = "0x3333333333333333333333333333333333333333",
                StateRoot = Bytes(0x44),
                TransactionsHash = Bytes(0x55),
                ReceiptHash = Bytes(0x66),
                LogsBloom = Bytes(0x77, 256),
                Difficulty = new EvmUInt256(7UL),
                BlockNumber = new EvmUInt256(1234UL),
                GasLimit = 30_000_000,
                GasUsed = 21_000,
                Timestamp = 1_700_000_000,
                ExtraData = Bytes(0x88, 4),
                MixHash = Bytes(0x99),
                Nonce = Bytes(0xAA, 8)
            };
            if (shape == "frontier") return h;

            h.BaseFee = new EvmUInt256(1_000_000_000UL);
            if (shape == "london") return h;

            h.WithdrawalsRoot = Bytes(0xBB);
            if (shape == "shanghai") return h;

            h.BlobGasUsed = 131_072;
            h.ExcessBlobGas = 262_144;
            h.ParentBeaconBlockRoot = Bytes(0xCC);
            if (shape == "cancun") return h;

            h.RequestsHash = Bytes(0xDD);
            if (shape == "prague") return h;

            h.BlockAccessListHash = Bytes(0xEE);
            h.SlotNumber = 9_876_543;
            return h;
        }
    }
}
