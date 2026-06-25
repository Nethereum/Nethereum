using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    // Real-world fidelity check against geth/erigon-served canonical bytes.
    // Captured from Erigon mainnet via debug_getRawReceipts for Frontier
    // block 51921, plus the header's receiptsRoot. If our ReceiptEncoder
    // round-trips byte-identically AND the computed root matches the canonical
    // header, the receipt-encoding contract holds at this fork.
    public class ReceiptEncoderGethFidelityTests
    {
        // Block 51921 (Frontier, 2 receipts) — header.receiptsRoot
        private const string CanonicalReceiptsRoot =
            "0x81e504c829cf0b3d0dbea50f93a6591de3b0f30e49ae7b89b01fc7e281c9131a";

        // Receipt RLP as served by Erigon's debug_getRawReceipts. Both are
        // STATUS-form (first byte 0x01) even though 51921 is pre-Byzantium.
        // That observation alone is significant — it confirms geth/erigon
        // store + emit status-form receipts uniformly, and the canonical
        // header.receiptsRoot for Frontier blocks was likewise computed
        // over status-form (this is geth's "DeriveReceipts" historical
        // canonicalisation, not the original Frontier yellowpaper form).
        private const string Block51921Receipt0Hex =
            "0xf9010801825208b9010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c0";

        private const string Block51921Receipt1Hex =
            "0xf9010801827c28b9010000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000c0";

        [Fact]
        public void Decode_Block51921Receipts_RoundTripsByteForByte_AgainstGeth()
        {
            var rawWire0 = Block51921Receipt0Hex.HexToByteArray();
            var rawWire1 = Block51921Receipt1Hex.HexToByteArray();

            var receipt0 = ReceiptEncoder.Current.Decode(rawWire0);
            var receipt1 = ReceiptEncoder.Current.Decode(rawWire1);

            var reEncoded0 = ReceiptEncoder.Current.Encode(receipt0);
            var reEncoded1 = ReceiptEncoder.Current.Encode(receipt1);

            Assert.Equal(rawWire0, reEncoded0);
            Assert.Equal(rawWire1, reEncoded1);
        }

        [Fact]
        public void ComputedReceiptsRoot_FromGethBytes_MatchesCanonicalHeader()
        {
            var rawWire0 = Block51921Receipt0Hex.HexToByteArray();
            var rawWire1 = Block51921Receipt1Hex.HexToByteArray();

            var receipt0 = ReceiptEncoder.Current.Decode(rawWire0);
            var receipt1 = ReceiptEncoder.Current.Decode(rawWire1);

            var calculator = new RootCalculator();
            var computedRoot = calculator.CalculateReceiptsRoot(new List<Receipt> { receipt0, receipt1 });

            var canonicalRoot = CanonicalReceiptsRoot.HexToByteArray();
            Assert.Equal(canonicalRoot, computedRoot);
        }

        [Fact]
        public void Decoded_Receipt0_HasExpectedFields()
        {
            var receipt0 = ReceiptEncoder.Current.Decode(Block51921Receipt0Hex.HexToByteArray());

            Assert.True(receipt0.IsStatusReceipt);
            Assert.True(receipt0.HasSucceeded);
            Assert.Equal(21000, receipt0.CumulativeGasUsed);
            Assert.Equal(256, receipt0.Bloom.Length);
            Assert.Empty(receipt0.Logs);
        }
    }
}
