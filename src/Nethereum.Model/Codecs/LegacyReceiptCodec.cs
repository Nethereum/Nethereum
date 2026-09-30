using System;

namespace Nethereum.Model.Codecs
{
    public sealed class LegacyReceiptCodec : IReceiptCodec
    {
        public static readonly LegacyReceiptCodec Instance = new LegacyReceiptCodec();

        public byte[] Encode(Receipt receipt)
        {
            // At pre-EIP-2718 forks no typed envelope can exist; the
            // executor's construction rule never sets TransactionType > 0
            // here. Plain RLP list only.
            return ReceiptEncoder.Current.Encode(receipt);
        }

        public Receipt Decode(byte[] rawBytes)
        {
            if (rawBytes == null || rawBytes.Length == 0) return null;

            // EIP-2718 typed envelope: first byte is the type discriminator
            // in [0x01, 0x7f]. Forbidden at this fork — typed transactions
            // (and therefore typed receipts) didn't exist yet.
            if (rawBytes[0] <= 0x7f)
                throw new InvalidOperationException(
                    "Typed receipt envelope (EIP-2718) is not valid at a pre-Berlin fork.");

            return ReceiptEncoder.Current.Decode(rawBytes);
        }
    }
}
