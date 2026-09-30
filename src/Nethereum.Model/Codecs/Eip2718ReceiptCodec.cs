namespace Nethereum.Model.Codecs
{
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
