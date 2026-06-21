namespace Nethereum.Model.Codecs
{
    /// <summary>
    /// Receipt codec for EIP-2718 forks (Berlin onward). Receipts inherit
    /// the typed-envelope rule from transactions: a receipt for a typed
    /// transaction (<see cref="Receipt.TransactionType"/> &gt; 0) is
    /// encoded as <c>typeByte || RLP(payload)</c>; a receipt for a legacy
    /// transaction is encoded as a bare RLP list.
    ///
    /// <para>This codec does NOT restrict which type bytes are valid —
    /// gating lives on the <see cref="ITransactionDecoder"/> side per fork
    /// (Berlin gates 0x01, London 0x02, Cancun 0x03, Prague 0x04). By the
    /// time a receipt is decoded, the corresponding transaction has already
    /// been validated by the fork's tx decoder; the receipt type is
    /// implicitly gated upstream. If you ever need standalone receipt-type
    /// gating (e.g. wire receipts arriving without corresponding txs)
    /// register a fork-specific subclass that filters
    /// <see cref="Receipt.TransactionType"/> on decode.</para>
    /// </summary>
    public sealed class Eip2718ReceiptCodec : IReceiptCodec
    {
        public static readonly Eip2718ReceiptCodec Instance = new Eip2718ReceiptCodec();

        public byte[] Encode(Receipt receipt)
        {
            return receipt.TransactionType > 0
                ? ReceiptEncoder.Current.EncodeTyped(receipt, receipt.TransactionType)
                : ReceiptEncoder.Current.Encode(receipt);
        }

        public Receipt Decode(byte[] rawBytes)
        {
            return ReceiptEncoder.Current.Decode(rawBytes);
        }
    }
}
