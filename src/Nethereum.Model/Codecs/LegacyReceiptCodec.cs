using System;

namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Receipt codec for pre-EIP-2718 forks (Frontier through Berlin-1).
    /// Receipts are always a plain RLP list — no typed envelope. Decode
    /// rejects anything whose first byte indicates a typed envelope.
    ///
    /// <para>The content of <see cref="Receipt.PostStateOrStatus"/> is
    /// determined by the receipt-construction rule, not by this codec:
    /// pre-Byzantium = 32-byte intermediate state root;
    /// Byzantium-onward (still pre-EIP-2718) = 1-byte status. Both encode
    /// identically through this codec — the first RLP element is the
    /// bytes you put in <c>PostStateOrStatus</c>.</para>
    /// </summary>
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
